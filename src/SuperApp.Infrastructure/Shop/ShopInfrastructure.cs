using Microsoft.Extensions.DependencyInjection;
using SuperApp.Application.Features.Shop.Catalog;

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
        return services;
    }
}
