using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PhainonDistributionCenter.Entities;

namespace PhainonDistributionCenter.Services;

public class AccessTokenService(MainDbContext dbContext)
{
    private MainDbContext DbContext { get; } = dbContext;

    public async Task<(string token, AccessTokenInfo info)> GenerateTokenAsync(int creatorUid, string creatorName, string name, DateTimeOffset expireTime)
    {
        var tokenData = RandomNumberGenerator.GetBytes(32);
        var tokenHash = SHA256.HashData(tokenData);
        var tokenInfo = new AccessTokenInfo()
        {
            Id = Guid.NewGuid(),
            CreatorUid = creatorUid,
            CreatorName = creatorName,
            Name = name,
            TokenHash = tokenHash,
            IsActive = true,
            ExpireTime = expireTime
        };
        await DbContext.AccessTokens.AddAsync(tokenInfo);
        await DbContext.SaveChangesAsync();
        return (Convert.ToHexStringLower(tokenData), tokenInfo);
    }

    public async Task<(bool success, AccessTokenInfo? info)> VerifyTokenAsync(string token)
    {
        try
        {
            var data = Convert.FromHexString(token);
            var hash = SHA256.HashData(data);
            var now = DateTimeOffset.UtcNow;
            var info = await DbContext.AccessTokens.FirstOrDefaultAsync(x => x.TokenHash.SequenceEqual(hash) &&
                                                                      x.IsActive &&
                                                                      x.ExpireTime >= now);
            return (info != null, info);
        }
        catch
        {
            return (false, null);
        }
    }
    
}