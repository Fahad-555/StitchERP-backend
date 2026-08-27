using System.Security.Claims;
using StitchERP.Application.Identity;

namespace StitchERP.Api.Middleware;

public sealed class SessionAuthenticationMiddleware(RequestDelegate next, ISessionTokenService tokens)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) &&
            tokens.TryRead(header[7..].Trim(), out var identity))
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, identity.UserId.ToString()),
                new(ClaimTypes.Name, identity.Username),
                new("organization_id", identity.OrganizationId.ToString())
            };
            claims.AddRange(identity.Roles.Select(role => new Claim("role", role)));
            claims.AddRange(identity.Permissions.Select(permission => new Claim("permission", permission)));
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        }

        await next(context);
    }
}
