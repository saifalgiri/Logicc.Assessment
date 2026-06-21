using Logicc.Api.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Logicc.Api.Filters;

/// <summary>
/// Restricts an action to callers whose simulated role is "admin" (per the "x-role" header).
/// Returns 403 Forbidden for any other caller.
/// </summary>
public sealed class AdminOnlyAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var currentUserContext = context.HttpContext.RequestServices.GetRequiredService<ICurrentUserContext>();

        if (!currentUserContext.IsAdmin)
        {
            context.Result = new ObjectResult(new
            {
                error = "This action requires administrator privileges.",
                requiredRole = "admin",
                currentRole = currentUserContext.Role,
            })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
            return;
        }

        await next();
    }
}
