using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ProjectCallisto.API.Controllers;

[ApiController]
public class AuthController : ControllerBase
{
    [Authorize]
    [HttpGet("api/auth/me")]
    public IActionResult Me()
    {
        var req =  HttpContext.Request;
        var user = HttpContext.User;
        return Ok(new
        {
            Name = User.FindFirst("name")?.Value,
            Email = User.FindFirst(x => x.Type.Contains("emailaddress"))?.Value,
        });
    }

    [HttpGet("signin")]
    public IActionResult SignIn([FromQuery] string? returnUrl = "/")
    {
        return Challenge(new AuthenticationProperties { RedirectUri = returnUrl });
    }

    [HttpGet("signout")]
    public new IActionResult SignOut()
    {
        return SignOut(new AuthenticationProperties { RedirectUri = "/" }, "Cookies", "OpenIdConnect");
    }
}
