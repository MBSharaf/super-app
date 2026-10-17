using Microsoft.Extensions.DependencyInjection;
using SuperApp.Application.Features.Shop.Catalog;
using SuperApp.Application.Features.Shop.Orders;

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
        return services;
    }
}
