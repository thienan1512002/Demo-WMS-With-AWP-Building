using DeliveryDemo.Application.Warehouse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DeliveryDemo.Infrastructure.Warehouse;

public static class WarehouseServices
{
    public static IServiceCollection AddWarehousePersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<WarehouseDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IStockPosting, StockPosting>();
        return services;
    }
}
