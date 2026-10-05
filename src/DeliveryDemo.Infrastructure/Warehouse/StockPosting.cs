using DeliveryDemo.Application.Warehouse;
using DeliveryDemo.Domain.Warehouse;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DeliveryDemo.Infrastructure.Warehouse;

public sealed class StockPosting(WarehouseDbContext context) : IStockPosting
{
    public Task<Guid> PostAsync(Guid goodsId, Guid warehouseId, StockTransactionType type,
        decimal quantity, DateTime occurredAt, string actor, CancellationToken cancellationToken = default) =>
        PostAsync(Guid.NewGuid(), goodsId, warehouseId, type, quantity, occurredAt, actor, cancellationToken);

    public async Task<Guid> PostAsync(Guid requestId, Guid goodsId, Guid warehouseId, StockTransactionType type,
        decimal quantity, DateTime occurredAt, string actor, CancellationToken cancellationToken = default)
    {
        if (requestId == Guid.Empty || goodsId == Guid.Empty || warehouseId == Guid.Empty ||
            !Enum.IsDefined(type) || quantity <= 0 || decimal.Round(quantity, 6) != quantity ||
            quantity >= 100000000000000m || occurredAt.Kind != DateTimeKind.Utc ||
            string.IsNullOrWhiteSpace(actor) || actor.Length > 256)
            throw new ArgumentException("Posting requires nonempty IDs, a valid type, positive quantity (up to six decimals), UTC time and actor.");

        // PostgreSQL stores microseconds; normalize before persisting and comparing retries.
        occurredAt = new DateTime(occurredAt.Ticks - occurredAt.Ticks % 10, DateTimeKind.Utc);
        await using var transaction = context.Database.CurrentTransaction is null
            ? await BeginTransactionAsync(cancellationToken) : null;
        StockTransaction? entry = null;
        try
        {
            // Serialize the same request across processes before checking the immutable ledger.
            // Hash collisions only serialize unrelated requests; the UUID PK remains authoritative.
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({requestId.ToString()}, 0))", cancellationToken);
            var existing = await context.StockTransactions.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == requestId, cancellationToken);
            if (existing is not null)
            {
                if (existing.GoodsId != goodsId || existing.WarehouseId != warehouseId || existing.Type != type ||
                    existing.Quantity != quantity || existing.OccurredAt != occurredAt || existing.Actor != actor)
                    throw new StockPostingException(StockPostingError.Conflict, "Request ID was already used with different posting data.");
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return existing.Id;
            }
            var goods = await context.Goods.AsNoTracking().SingleOrDefaultAsync(x => x.Id == goodsId, cancellationToken);
            var warehouse = await context.Warehouses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == warehouseId, cancellationToken);
            if (goods is null || warehouse is null)
                throw new StockPostingException(StockPostingError.NotFound, "Goods or warehouse was not found.");
            if (goods.ArchivedAt is not null || warehouse.ArchivedAt is not null)
                throw new StockPostingException(StockPostingError.BusinessRule, "Archived goods or warehouses cannot receive postings.");
            entry = new StockTransaction
            {
                Id = requestId,
                GoodsId = goodsId,
                WarehouseId = warehouseId,
                Type = type,
                Quantity = quantity,
                OccurredAt = occurredAt,
                Actor = actor
            };
            context.StockTransactions.Add(entry);
            await context.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return entry.Id;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23514" or "22003" })
        {
            throw new StockPostingException(StockPostingError.BusinessRule, "Posting violates stock availability, active master data or balance limits.");
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" or "40001" or "40P01" })
        {
            throw new StockPostingException(StockPostingError.Conflict, "Posting conflicted with another operation; retry with the same request ID.");
        }
        catch (NpgsqlException)
        {
            throw new StockPostingException(StockPostingError.Persistence, "Warehouse persistence is unavailable.");
        }
        catch (DbUpdateException)
        {
            throw new StockPostingException(StockPostingError.Persistence, "Posting could not be persisted.");
        }
        finally
        {
            if (entry is not null) context.Entry(entry).State = EntityState.Detached;
        }
    }

    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        try { return await context.Database.BeginTransactionAsync(cancellationToken); }
        catch (NpgsqlException)
        {
            throw new StockPostingException(StockPostingError.Persistence, "Warehouse persistence is unavailable.");
        }
    }
}
