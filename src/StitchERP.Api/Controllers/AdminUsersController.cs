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
    public ActionResult<ManagedUser> Create(CreateUserRequest request) => Ok(service.Create(request));

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