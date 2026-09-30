using DepotFlow.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DepotFlow.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await authService.LoginAsync(request, cancellationToken);

        // Same message for an unknown email and a wrong password, so callers cannot probe for accounts.
        return response is null
            ? Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials", detail: "Email or password is incorrect.")
            : Ok(response);
    }
}
