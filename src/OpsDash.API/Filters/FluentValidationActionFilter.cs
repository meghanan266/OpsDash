using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OpsDash.Application.DTOs.Common;

namespace OpsDash.API.Filters;

public class FluentValidationActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null or CancellationToken)
            {
                continue;
            }

            var argumentType = argument.GetType();
            if (argumentType == typeof(string) || argumentType.IsPrimitive)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argumentType);
            var validator = services.GetService(validatorType) as IValidator;
            if (validator is null)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);
            if (!result.IsValid)
            {
                var errors = result.Errors.Select(e => e.ErrorMessage).ToList();
                context.Result = new BadRequestObjectResult(ApiResponse<object>.Fail(errors));
                return;
            }
        }

        await next();
    }
}
