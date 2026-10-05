using DeliveryDemo.Domain.Warehouse;

namespace DeliveryDemo.Application.Warehouse;

public interface IStockPosting
{
    Task<Guid> PostAsync(Guid goodsId, Guid warehouseId, StockTransactionType type,
        decimal quantity, DateTime occurredAt, string actor, CancellationToken cancellationToken = default);
}
