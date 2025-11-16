using System.Security.Claims;

namespace PhainonDistributionCenter.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static bool CanWrite(this ClaimsPrincipal user)
    {
        return user.Claims.Any(c => c is { Type: "urn:pdc:write", Value: "true" });
    }
}