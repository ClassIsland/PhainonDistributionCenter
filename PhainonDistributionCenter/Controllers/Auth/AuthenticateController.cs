using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace PhainonDistributionCenter.Controllers.Auth;

[Route("auth/")]
public class AuthenticateController : Controller
{
    [HttpGet("login/github")]
    public IActionResult LoginGitHub([FromQuery] string? redirect = null)
    {
        return Challenge(new AuthenticationProperties { RedirectUri = redirect ?? "/" },
            GitHubAuthenticationDefaults.AuthenticationScheme);
    }
    
    [HttpGet("logout")]
    public IActionResult Logout()
    {
        return SignOut(new AuthenticationProperties()
        {
            RedirectUri = "/Account/LoggedOut",
            AllowRefresh = true
        }, CookieAuthenticationDefaults.AuthenticationScheme);
    }
}