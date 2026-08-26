using Microsoft.AspNetCore.Mvc;
using StitchERP.Application.Identity;
using StitchERP.Api.Security;

namespace StitchERP.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
public sealed class NotificationsController(IAuthenticationService service) : ControllerBase
{
    [HttpGet]
    [RequirePermission("NOTIFICATION_VIEW")]
    public ActionResult<IReadOnlyCollection<RoleNotification>> Get()
    {
        var userId = long.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 1;
        var roles = User.FindAll("role").Select(x => x.Value).ToArray();
        if (roles.Length == 0) roles = ["MANAGER"];
        return Ok(service.GetNotifications(userId, roles));
    }
}