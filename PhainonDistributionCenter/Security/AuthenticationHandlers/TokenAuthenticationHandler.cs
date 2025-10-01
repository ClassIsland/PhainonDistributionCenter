using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PhainonDistributionCenter.Services;

namespace PhainonDistributionCenter.Security.AuthenticationHandlers;

public class TokenAuthenticationHandler(
    IOptionsMonitor<TokenOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AccessTokenService accessTokenService)
    : AuthenticationHandler<TokenOptions>(options, logger, encoder)
{
    public const string SchemeName = "PDC_Token";
    
    private const string HeaderName = "X-PDC-Token";
    
    public AccessTokenService AccessTokenService { get; } = accessTokenService;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var headerValues))
            return AuthenticateResult.NoResult();
        
        var provided = headerValues.ToString().Trim();

        var (success, tokenInfo) = await AccessTokenService.VerifyTokenAsync(provided);
        if (!success)
            return AuthenticateResult.Fail("Token invalid.");
        
        var claims = new[] {
            new Claim(ClaimTypes.NameIdentifier, tokenInfo!.CreatorUid.ToString()),
            new Claim(ClaimTypes.Name, tokenInfo.CreatorName),
            new Claim(ClaimTypes.Actor, "ci")
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}