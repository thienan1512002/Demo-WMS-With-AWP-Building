using System.Diagnostics;
using DeliveryDemo.Domain.Warehouse;
using DeliveryDemo.Infrastructure.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace DeliveryDemo.Api.Tests;

public sealed class WarehouseTests
{
    private static WarehouseDbContext Context(string? connection = null) => new(
        new DbContextOptionsBuilder<WarehouseDbContext>().UseNpgsql(connection).Options);

    [Fact]
    public void MigrationMatchesModelAndSupportsRollback()
    {
        using var db = Context();
        Assert.False(db.Database.HasPendingModelChanges());
        var migrator = db.GetService<IMigrator>();
        var sql = migrator.GenerateScript();
        Assert.Contains("CREATE TABLE inventory_balances", sql);
        Assert.Contains("CREATE TRIGGER stock_post", sql);
        Assert.Contains("quantity >= NEW.quantity", sql);
        Assert.Contains("DROP FUNCTION warehouse_post()", migrator.GenerateScript("InitialWarehouse", "0"));
    }

    [Theory]
    [InlineData(0, StockTransactionType.Receipt)]
    [InlineData(-1, StockTransactionType.Issue)]
    [InlineData(1, (StockTransactionType)99)]
    public async Task InvalidPostingFailsBeforeAccessingDatabase(decimal quantity, StockTransactionType type)
    {
        await using var db = Context();
        await Assert.ThrowsAsync<ArgumentException>(() => new StockPosting(db).PostAsync(
            Guid.NewGuid(), Guid.NewGuid(), type, quantity, DateTime.UtcNow, "test"));
    }

    [Theory]
    [InlineData(0.0000001, true, "test")]
    [InlineData(100000000000000, true, "test")]
    [InlineData(1, false, "test")]
    [InlineData(1, true, " ")]
    public async Task InvalidPrecisionAuditOrRangeFailsBeforeAccessingDatabase(
        decimal quantity, bool utc, string actor)
    {
        await using var db = Context();
        var occurredAt = DateTime.SpecifyKind(new DateTime(2026, 10, 4),
            utc ? DateTimeKind.Utc : DateTimeKind.Unspecified);
        await Assert.ThrowsAsync<ArgumentException>(() => new StockPosting(db).PostAsync(
            Guid.NewGuid(), Guid.NewGuid(), StockTransactionType.Receipt, quantity, occurredAt, actor));
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [PostgresFact]
    public async Task RealProviderMigrationConstraintsAtomicPostingConcurrencyAndRollback()
    {
        // Always provision a new disposable local container; never use an existing connection string.
        var name = "warehouse-test-" + Guid.NewGuid().ToString("N");
        var [REDACTED];
        try
        {
            await Docker("run", "--detach", "--name", name, "-e", "POSTGRES_PASSWORD=" + password,
                "-p", "127.0.0.1::5432", "postgres:17-alpine");
            var port = (await Docker("port", name, "5432/tcp")).Trim().Split(':')[^1];
            var connection = new NpgsqlConnectionStringBuilder
            {
                Host = "127.0.0.1",
                Port = int.Parse(port),
                Database = "postgres",
                Username = "postgres",
                [REDACTED],
                Pooling = false
            }.ConnectionString;
            var ready = false;
            for (var i = 0; i < 60 && !ready; i++)
            {
                try
                {
                    await using var probe = new NpgsqlConnection(connection);
                    await probe.OpenAsync();
                    ready = true;
                }
                catch (NpgsqlException) { await Task.Delay(500); }
            }
            Assert.True(ready, "Disposable PostgreSQL did not become ready.");
            await using var db = Context(connection);
            await db.Database.MigrateAsync();
            var goods = new Goods { Code = "G1", Name = "Goods" };
            var warehouse = new Domain.Warehouse.Warehouse { Code = "W1", Name = "Warehouse" };
            db.AddRange(goods, warehouse);
            await db.SaveChangesAsync();
            async Task<bool> Post(decimal quantity, StockTransactionType type)
            {
                await using var independent = Context(connection);
                try
                {
                    await new StockPosting(independent).PostAsync(goods.Id, warehouse.Id, type,
                        quantity, DateTime.UtcNow, "integration-test");
                    return true;
                }
                catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23514" })
                {
                    return false;
                }
            }
            Assert.False(await Post(1, StockTransactionType.Issue));
            Assert.Empty(await db.InventoryBalances.ToListAsync());
            Assert.True(await Post(10, StockTransactionType.Receipt));
            var competing = await Task.WhenAll(Post(7, StockTransactionType.Issue), Post(7, StockTransactionType.Issue));
            Assert.Single(competing, x => x);
            Assert.Equal(3, (await db.InventoryBalances.SingleAsync()).Quantity);
            Assert.Equal(2, await db.StockTransactions.CountAsync());
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
                "UPDATE inventory_balances SET quantity = 100"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
                "DELETE FROM stock_transactions"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
                "DELETE FROM goods"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
                "INSERT INTO goods SELECT * FROM goods"));
            await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
                "INSERT INTO stock_transactions(id, goods_id, warehouse_id, type, quantity, occurred_at, created_at, actor) VALUES ({0},{1},{2},1,0,now(),now(),'test')",
                Guid.NewGuid(), goods.Id, warehouse.Id));
            await using (var atomic = Context(connection))
            {
                await using var tx = await atomic.Database.BeginTransactionAsync();
                await new StockPosting(atomic).PostAsync(goods.Id, warehouse.Id, StockTransactionType.Receipt,
                    2, DateTime.UtcNow, "test");
                await tx.RollbackAsync();
            }
            Assert.Equal(3, (await db.InventoryBalances.AsNoTracking().SingleAsync()).Quantity);
            warehouse.ArchivedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            Assert.False(await Post(1, StockTransactionType.Receipt));
            Assert.Equal(2, await db.StockTransactions.CountAsync());
            await db.GetService<IMigrator>().MigrateAsync("0");
            await db.Database.MigrateAsync();
            Assert.Empty(await db.StockTransactions.ToListAsync());
        }
        finally
        {
            await Docker("rm", "--force", name);
        }
    }

    private static async Task<string> Docker(params string[] args)
    {
        var start = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, await stderr);
        return await stdout;
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("WAREHOUSE_POSTGRES_TESTS") != "1")
            Skip = "Set WAREHOUSE_POSTGRES_TESTS=1 to provision disposable PostgreSQL via Docker.";
    }
}
