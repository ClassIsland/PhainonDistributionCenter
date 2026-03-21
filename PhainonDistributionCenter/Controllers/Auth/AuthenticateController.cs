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
        var targetRedirect = NormalizeLocalRedirect(redirect);
        return Challenge(new AuthenticationProperties { RedirectUri = targetRedirect },
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

    private static string NormalizeLocalRedirect(string? redirect)
    {
        if (string.IsNullOrWhiteSpace(redirect))
        {
            return "/";
        }

        if (Uri.TryCreate(redirect, UriKind.Relative, out _) &&
            redirect.StartsWith('/') &&
            !redirect.StartsWith("//", StringComparison.Ordinal))
        {
            return redirect;
        }

        return "/";
    }
}
