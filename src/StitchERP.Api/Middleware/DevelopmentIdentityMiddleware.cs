using System.Security.Claims;

namespace StitchERP.Api.Middleware;

public sealed class DevelopmentIdentityMiddleware(RequestDelegate next, IWebHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (environment.IsDevelopment() && context.User.Identity?.IsAuthenticated != true)
        {
            var userId = context.Request.Headers["X-User-Id"].FirstOrDefault() ?? "1";
            var organizationId = context.Request.Headers["X-Organization-Id"].FirstOrDefault() ?? "1";
            var permissions = context.Request.Headers["X-Permissions"].FirstOrDefault() ?? "*";
            var roles = context.Request.Headers["X-Roles"].FirstOrDefault() ?? "MANAGER";
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId),
                new("organization_id", organizationId)
            };
            claims.AddRange(permissions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => new Claim("permission", x)));
            claims.AddRange(roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => new Claim("role", x)));
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Development"));
        }

        await next(context);
    }
}
