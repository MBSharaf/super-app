using Microsoft.Extensions.DependencyInjection;
using SuperApp.Application.Features.Shop.Catalog;
using SuperApp.Application.Features.Shop.Orders;

namespace SuperApp.Infrastructure.Shop;

/// <summary>
/// Phase 4 repositories, readers and adapters (shop, print on demand, factory, courier, returns). Kept in its own file
/// so the shared <c>DependencyInjection.cs</c> needs one line: <c>services.AddShopInfrastructure()</c>.
/// </summary>
public static class ShopInfrastructure
{
    public static IServiceCollection AddShopInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IShopReader, ShopReader>();
        services.AddScoped<IArtworkStore, ArtworkStore>();
        services.AddMemoryCache();
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<OrderRepository>();
        services.AddScoped<IOrderRepository>(sp => sp.GetRequiredService<OrderRepository>());
        services.AddScoped<IShopSettings, ShopSettings>();
        services.AddSingleton<IIdempotencyCache, IdempotencyCache>();
        services.AddSingleton<IShopDocuments, ShopDocuments>();
        return services;
    }
}
