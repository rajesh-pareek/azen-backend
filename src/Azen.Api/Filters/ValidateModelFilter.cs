using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Azen.Api.Filters;

/// <summary>
/// Runs FluentValidation against every [FromBody] / [FromForm] action argument
/// for which an IValidator&lt;T&gt; is registered. On failure, short-circuits with
/// the standard error envelope from mvp-design.md §10.
/// </summary>
public class ValidateModelFilter : IAsyncActionFilter
{
    private readonly IServiceProvider services;

    public ValidateModelFilter(IServiceProvider services)
    {
        this.services = services;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        foreach (var arg in context.ActionArguments)
        {
            var value = arg.Value;
            if (value is null) continue;

            // Resolve IValidator<TActualType> from DI. If no validator is registered
            // for this DTO, skip silently - not every parameter needs validation.
            var validatorType = typeof(IValidator<>).MakeGenericType(value.GetType());
            if (services.GetService(validatorType) is not IValidator validator)
                continue;

            var validationContext = new ValidationContext<object>(value);
            var result = await validator.ValidateAsync(validationContext);

            if (!result.IsValid)
            {
                // Surface the FIRST failure in the standard envelope.
                // Frontend gets a deterministic, machine-readable shape.
                var first = result.Errors[0];
                context.Result = new BadRequestObjectResult(new
                {
                    error = "VALIDATION_ERROR",
                    message = first.ErrorMessage,
                    field = ToCamelCase(first.PropertyName)
                });
                return;
            }
        }

        await next();
    }

    private static string ToCamelCase(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName)) return propertyName;
        if (char.IsLower(propertyName[0])) return propertyName;
        return char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
    }
}
