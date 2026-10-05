using DeliveryDemo.Domain.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DeliveryDemo.Infrastructure.Warehouse;

public sealed class WarehouseDbContext(DbContextOptions<WarehouseDbContext> options) : DbContext(options)
{
    public DbSet<Goods> Goods => Set<Goods>();
    public DbSet<Domain.Warehouse.Warehouse> Warehouses => Set<Domain.Warehouse.Warehouse>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();
    public DbSet<InventoryBalance> InventoryBalances => Set<InventoryBalance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureModel(modelBuilder);
    }

    internal static void ConfigureModel(ModelBuilder modelBuilder)
    {
        var goods = modelBuilder.Entity<Goods>();
        goods.ToTable("goods", t => t.HasCheckConstraint("ck_goods_text", "btrim(code) <> '' AND btrim(name) <> ''"));
        goods.HasKey(x => x.Id);
        goods.HasIndex(x => x.Code).IsUnique();
        goods.Property(x => x.Code).HasMaxLength(64);
        goods.Property(x => x.Name).HasMaxLength(256);
        var warehouse = modelBuilder.Entity<Domain.Warehouse.Warehouse>();
        warehouse.ToTable("warehouses", t => t.HasCheckConstraint("ck_warehouses_text", "btrim(code) <> '' AND btrim(name) <> ''"));
        warehouse.HasKey(x => x.Id);
        warehouse.HasIndex(x => x.Code).IsUnique();
        warehouse.Property(x => x.Code).HasMaxLength(64);
        warehouse.Property(x => x.Name).HasMaxLength(256);
        var balance = modelBuilder.Entity<InventoryBalance>();
        balance.ToTable("inventory_balances", t => t.HasCheckConstraint("ck_balance_quantity", "quantity >= 0 AND quantity < 100000000000000"));
        balance.HasKey(x => new { x.GoodsId, x.WarehouseId });
        balance.HasIndex(x => x.WarehouseId);
        balance.Property(x => x.Quantity).HasPrecision(20, 6);
        balance.HasOne<Goods>().WithMany().HasForeignKey(x => x.GoodsId).OnDelete(DeleteBehavior.Restrict);
        balance.HasOne<Domain.Warehouse.Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        var history = modelBuilder.Entity<StockTransaction>();
        history.ToTable("stock_transactions", t =>
        {
            t.HasCheckConstraint("ck_transaction_quantity", "quantity > 0 AND quantity < 100000000000000");
            t.HasCheckConstraint("ck_transaction_type", "type IN (1, 2)");
            t.HasCheckConstraint("ck_transaction_actor", "btrim(actor) <> ''");
        });
        history.HasKey(x => x.Id);
        history.Property(x => x.Quantity).HasPrecision(20, 6);
        history.Property(x => x.Actor).HasMaxLength(256);
        history.HasIndex(x => new { x.GoodsId, x.WarehouseId, x.OccurredAt });
        history.HasIndex(x => new { x.WarehouseId, x.OccurredAt });
        history.HasOne<Goods>().WithMany().HasForeignKey(x => x.GoodsId).OnDelete(DeleteBehavior.Restrict);
        history.HasOne<Domain.Warehouse.Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(string.Concat(property.Name.Select((c, i) => char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString())));
            }
        }
    }
}

public sealed class WarehouseDbContextFactory : IDesignTimeDbContextFactory<WarehouseDbContext>
{
    public WarehouseDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<WarehouseDbContext>().UseNpgsql().Options);
}
