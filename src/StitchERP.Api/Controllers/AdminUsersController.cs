using Microsoft.AspNetCore.Mvc;
using StitchERP.Application.Identity;
using StitchERP.Api.Security;

namespace StitchERP.Api.Controllers;

[ApiController]
[Route("api/v1/admin/users")]
public sealed class AdminUsersController(IUserAdministrationService service) : ControllerBase
{
    [HttpGet]
    [RequirePermission("USER_VIEW")]
    public ActionResult<IReadOnlyCollection<ManagedUser>> Get() => Ok(service.GetUsers());

    [HttpPost]
    [RequirePermission("USER_CREATE")]
    public ActionResult<CreatedUserResponse> Create(CreateUserRequest request) => Ok(service.CreateWithVerification(request));

    [HttpDelete("{id:long}")]
    [RequirePermission("USER_DELETE")]
    public ActionResult<ManagedUser> Delete(long id)
    {
        var target = service.GetUsers().FirstOrDefault(user => user.Id == id) ?? throw new KeyNotFoundException("User was not found.");
        var callerRoles = User.FindAll("role").Select(claim => claim.Value).ToArray();
        if (target.Roles.Contains("SUPER_ADMIN", StringComparer.OrdinalIgnoreCase) ||
            (target.Roles.Contains("ADMIN", StringComparer.OrdinalIgnoreCase) && !callerRoles.Contains("SUPER_ADMIN", StringComparer.OrdinalIgnoreCase)) ||
            (target.Roles.Contains("MANAGER", StringComparer.OrdinalIgnoreCase) && !callerRoles.Contains("SUPER_ADMIN", StringComparer.OrdinalIgnoreCase) && !callerRoles.Contains("ADMIN", StringComparer.OrdinalIgnoreCase)))
            return Forbid();
        if (target.Id == long.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value)) return BadRequest("You cannot delete your own account.");
        return Ok(service.Delete(id));
    }

    [HttpPost("verify-email")]
    public ActionResult<ManagedUser> VerifyEmail([FromQuery] string token) => Ok(service.VerifyEmail(token));

    [HttpPut("{id:long}/status")]
    [RequirePermission("USER_DISABLE")]
    public ActionResult<ManagedUser> SetStatus(long id, SetUserStatusRequest request) => Ok(service.SetStatus(id, request.IsActive));

    [HttpPut("{id:long}/roles")]
    [RequirePermission("PERMISSION_ASSIGN")]
    public ActionResult<ManagedUser> SetRoles(long id, SetUserRolesRequest request) => Ok(service.SetRoles(id, request.Roles));

    [HttpPost("{id:long}/reset-password")]
    [RequirePermission("USER_RESET_PASSWORD")]
    public ActionResult<ManagedUser> ResetPassword(long id, SetUserPasswordRequest request) => Ok(service.SetPassword(id, request.Password));
}