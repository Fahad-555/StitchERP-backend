using Microsoft.AspNetCore.Mvc;
using StitchERP.Application.Identity;

namespace StitchERP.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthenticationService service, ISessionTokenService tokens) : ControllerBase
{
    [HttpPost("login")]
    public ActionResult<LoginResponse> Login(LoginRequest request)
    {
        var response = service.Login(request);
        var user = service.GetUser(response.UserId);
        return Ok(response with { AccessToken = tokens.Create(user) });
    }

    [HttpPost("forgot-password")]
    public ActionResult<ForgotPasswordResponse> ForgotPassword(ForgotPasswordRequest request) => Ok(service.RequestPasswordReset(request));

    [HttpPost("reset-password")]
    public IActionResult ResetPassword(ResetPasswordRequest request)
    {
        service.ResetPassword(request);
        return NoContent();
    }

    [HttpPost("change-password")]
    public IActionResult ChangePassword(ChangePasswordRequest request)
    {
        service.ChangePassword(request);
        return NoContent();
    }
}