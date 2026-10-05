namespace DeliveryDemo.Domain.Warehouse;

public sealed class Goods
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt { get; set; }
}

public sealed class Warehouse
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt { get; set; }
}

public enum StockTransactionType { Receipt = 1, Issue = 2 }

public sealed class StockTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GoodsId { get; set; }
    public Guid WarehouseId { get; set; }
    public StockTransactionType Type { get; set; }
    public decimal Quantity { get; set; }
    public DateTime OccurredAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Actor { get; set; } = "";
}

public sealed class InventoryBalance
{
    public Guid GoodsId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal Quantity { get; set; }
    public DateTime UpdatedAt { get; set; }
}
