using DeliveryDemo.Domain.Warehouse;

namespace DeliveryDemo.Application.Warehouse;

public interface IStockPosting
{
    // Keep requestId and the complete payload stable across retries.
    Task<Guid> PostAsync(Guid requestId, Guid goodsId, Guid warehouseId, StockTransactionType type,
        decimal quantity, DateTime occurredAt, string actor, CancellationToken cancellationToken = default);

    Task<Guid> PostAsync(Guid goodsId, Guid warehouseId, StockTransactionType type,
        decimal quantity, DateTime occurredAt, string actor, CancellationToken cancellationToken = default);
}

public enum StockPostingError { NotFound, Conflict, BusinessRule, Persistence }
public sealed class StockPostingException(StockPostingError error, string message) : Exception(message)
{
    public StockPostingError Error { get; } = error;
}
