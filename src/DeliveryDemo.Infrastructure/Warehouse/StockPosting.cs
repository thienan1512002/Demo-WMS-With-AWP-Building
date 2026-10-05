using DeliveryDemo.Application.Warehouse;
using DeliveryDemo.Domain.Warehouse;

namespace DeliveryDemo.Infrastructure.Warehouse;

public sealed class StockPosting(WarehouseDbContext context) : IStockPosting
{
    public async Task<Guid> PostAsync(Guid goodsId, Guid warehouseId, StockTransactionType type,
        decimal quantity, DateTime occurredAt, string actor, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(type) || quantity <= 0 || decimal.Round(quantity, 6) != quantity ||
            quantity >= 100000000000000m || occurredAt.Kind != DateTimeKind.Utc ||
            string.IsNullOrWhiteSpace(actor) || actor.Length > 256)
        {
            throw new ArgumentException("Posting requires a valid type, positive quantity (up to six decimals), UTC time and actor.");
        }
        var entry = new StockTransaction
        {
            GoodsId = goodsId,
            WarehouseId = warehouseId,
            Type = type,
            Quantity = quantity,
            OccurredAt = occurredAt,
            Actor = actor
        };
        context.StockTransactions.Add(entry);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return entry.Id;
        }
        catch
        {
            context.Entry(entry).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
            throw;
        }
    }
}
