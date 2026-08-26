using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace StitchERP.Api.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequirePermissionAttribute(string permission) : Attribute, IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var permissions = context.HttpContext.User.FindAll("permission").Select(x => x.Value).ToArray();
        if (permissions.Length == 0 || (!permissions.Contains("*") && !permissions.Contains(permission, StringComparer.OrdinalIgnoreCase)))
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
        return Task.CompletedTask;
    }
}
