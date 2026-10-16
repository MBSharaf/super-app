using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SuperApp.Api.Filters;

/// <summary>
/// "Never trust user input": every action argument that has a FluentValidation validator is validated before the
/// action runs. Invalid input throws <see cref="ValidationException"/>, which the global handler answers with 400.
/// </summary>
public sealed class ValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }
            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (services.GetService(validatorType) is IValidator validator)
            {
                var result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
                if (!result.IsValid)
                {
                    throw new ValidationException(result.Errors);
                }
            }
        }
        await next();
    }
}
