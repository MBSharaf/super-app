using Microsoft.Extensions.DependencyInjection;
using SuperApp.Application.Features.Shop.Catalog;
using SuperApp.Application.Features.Shop.Factory;
using SuperApp.Application.Features.Shop.Orders;
using SuperApp.Application.Features.Shop.Pod;
using SuperApp.Application.Features.Shop.Shipping;
using SuperApp.Application.Features.Shop.Stores;

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
        services.AddSingleton<IShopDocuments, ShopDocuments>();
        services.AddScoped<IStoreRepository, StoreRepository>();
        services.AddSingleton<SimulatedCourier>();
        services.AddSingleton<IShippingProvider>(sp => sp.GetRequiredService<SimulatedCourier>());
        services.AddScoped<IShippingProviders, ShippingProviders>();
        services.AddScoped<IShipmentRepository, ShipmentRepository>();
        services.AddScoped<ParcelRepository>();
        services.AddScoped<IParcelRepository>(sp => sp.GetRequiredService<ParcelRepository>());
        services.AddScoped<IShipmentReader>(sp => sp.GetRequiredService<ParcelRepository>());
        services.AddScoped<ISellerScopes, SellerScopes>();
        services.AddScoped<DesignRepository>();
        services.AddScoped<IDesignRepository>(sp => sp.GetRequiredService<DesignRepository>());
        services.AddScoped<ISellerLevels>(sp => sp.GetRequiredService<DesignRepository>());
        services.AddScoped<ISellerInsightsRepository, SellerInsightsRepository>();
        services.AddScoped<IPodAdminRepository, PodAdminRepository>();
        services.AddSingleton<IFactoryTokens, FactoryTokens>();
        services.AddScoped<IFactoryHeaders, FactoryHeaders>();
        services.AddSingleton<IPinHasher, PinHasherService>();
        services.AddScoped<FactoryRepository>();
        services.AddScoped<IFactoryRepository>(sp => sp.GetRequiredService<FactoryRepository>());
        services.AddScoped<IFactoryAdminRepository, FactoryAdminRepository>();
        services.AddSingleton<IFactoryDocuments>(sp => (ShopDocuments)sp.GetRequiredService<IShopDocuments>());
        services.AddHttpContextAccessor();
        services.AddScoped<Application.Features.Shop.Returns.IReturnRepository, ReturnRepository>();
        return services;
    }
}
