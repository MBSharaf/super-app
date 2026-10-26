using Microsoft.AspNetCore.Mvc.Filters;
using SuperApp.Application.Common.Exceptions;

namespace SuperApp.Api.Filters;

/// <summary>
/// The store, sellers, print factory and shipping are paused for now (owner's decision, 2026-10-26). Every controller in
/// <c>Controllers/Shop</c> answers 423 <c>store_paused</c> until the setting <c>Features:StoreEnabled</c> is true. The code
/// and data stay as they are, so turning the setting on brings everything back.
/// </summary>
public sealed class StorePausedFilter(IConfiguration configuration) : IAsyncResourceFilter
{
    private const string ShopNamespace = "SuperApp.Api.Controllers.Shop";

    public Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var shop = context.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor action
            && action.ControllerTypeInfo.Namespace == ShopNamespace;
        if (shop && !configuration.GetValue("Features:StoreEnabled", false))
        {
            throw new FeatureDisabledException("The Super App store is coming soon.", "store_paused");
        }
        return next();
    }
}
