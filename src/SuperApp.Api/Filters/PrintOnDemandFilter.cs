using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using SuperApp.Application.Common.Exceptions;

namespace SuperApp.Api.Filters;

/// <summary>Marks a print-on-demand controller or action (designs, samples, the printable catalogue, factories).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class PrintOnDemandAttribute : Attribute;

/// <summary>
/// The store is back for sellers' own products, but print on demand stays off (owner's decision, 2026-10-27). Everything
/// marked <see cref="PrintOnDemandAttribute"/> answers 423 <c>pod_paused</c> until the setting <c>Features:PodEnabled</c>
/// is true. The CEO's kill switch <c>kill.pod</c> still pauses printing at run time when it is on.
/// </summary>
public sealed class PrintOnDemandFilter(IConfiguration configuration) : IAsyncResourceFilter
{
    public Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var pod = context.ActionDescriptor is ControllerActionDescriptor action
            && (action.MethodInfo.IsDefined(typeof(PrintOnDemandAttribute), true) || action.ControllerTypeInfo.IsDefined(typeof(PrintOnDemandAttribute), true));
        if (pod && !configuration.GetValue("Features:PodEnabled", false))
        {
            throw new FeatureDisabledException("Print on demand is paused for now. Sellers can sell their own products.", "pod_paused");
        }
        return next();
    }
}
