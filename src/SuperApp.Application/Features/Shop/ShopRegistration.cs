using Microsoft.Extensions.DependencyInjection;
using SuperApp.Application.Features.Shop.Catalog;
using SuperApp.Application.Features.Shop.Orders;
using SuperApp.Application.Features.Shop.Pod;
using SuperApp.Application.Features.Shop.Shipping;
using SuperApp.Application.Features.Shop.Stores;

namespace SuperApp.Application.Features.Shop;

/// <summary>
/// Phase 4 (shop, print on demand, factory, shipping, returns) business services. Kept in its own file so the
/// shared <c>DependencyInjection.cs</c> only needs one line: <c>services.AddShop()</c>. Validators are picked up by
/// the assembly scan in <c>AddApplication</c>.
/// </summary>
public static class ShopRegistration
{
    public static IServiceCollection AddShop(this IServiceCollection services)
    {
        services.AddScoped<IShopService, ShopService>();
        services.AddScoped<IStoresService, StoresService>();

        services.AddScoped<CartService>();
        services.AddScoped<ICartService>(sp => sp.GetRequiredService<CartService>());
        services.AddScoped<OrderNotices>();
        services.AddScoped<ICheckoutService, CheckoutService>();
        services.AddScoped<IMyOrdersService, MyOrdersService>();

        services.AddScoped<ShipmentBooker>();
        services.AddScoped<ISellerAccess, SellerAccess>();
        services.AddScoped<IStoreScopes, StoreScopes>();
        services.AddScoped<IStoreProfileService, StoreProfileService>();
        services.AddScoped<IStoreProductsService, StoreProductsService>();
        services.AddScoped<IStoreOrdersService, StoreOrdersService>();
        services.AddScoped<IAdminProductsService, AdminProductsService>();

        services.AddScoped<ISellerDesignsService, SellerDesignsService>();
        services.AddScoped<IBulkDesignsService, BulkDesignsService>();
        services.AddScoped<ISellerInsightsService, SellerInsightsService>();
        services.AddScoped<ISamplesService, SamplesService>();
        services.AddScoped<ISellerCodesService, SellerCodesService>();
        services.AddScoped<ISellerKycService, SellerKycService>();
        services.AddScoped<IDesignReviewService, DesignReviewService>();
        services.AddScoped<IDesignReportsService, DesignReportsService>();
        return services;
    }
}
