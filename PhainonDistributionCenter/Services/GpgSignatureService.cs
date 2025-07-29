using System.Text;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Asn1.Cms;
using Org.BouncyCastle.Bcpg.OpenPgp;
using PgpCore;
using PhainonDistributionCenter.Entities;
using PhainonDistributionCenter.Helpers;

namespace PhainonDistributionCenter.Services;

public class GpgSignatureService(MainDbContext dbContext, ILogger<GpgSignatureService> logger)
{
    public MainDbContext DbContext { get; } = dbContext;
    public ILogger<GpgSignatureService> Logger { get; } = logger;

    public async Task<(bool, GpgPublicKey?)> CheckSignatureAsync(string payload, string signature)
    {
        Logger.LogInformation("正在解析签名信息：{}", signature);
        try
        {
            var signatureBuffer = Encoding.UTF8.GetBytes(signature);
            var sign = DetachedSignatureProcessor.GetPgpSignature(new MemoryStream(signatureBuffer));
            var keyId = sign.KeyId;
            var keyInfo = await DbContext.PublicKeys.FirstOrDefaultAsync(x => x.KeyId == keyId);
            if (keyInfo == null)
            {
                Logger.LogInformation("校验签名失败，找不到指定的公钥 {}", keyId);
                return (false, null);
            }
            
            return (DetachedSignatureProcessor.VerifyDetachedSignature(payload, signatureBuffer, keyInfo.PublicKey),
                    keyInfo);

        }
        catch (Exception e)
        {
            Logger.LogError(e, "在解析签名信息时发生错误");
            return (false, null);
        }
    }

    public async Task<GpgPublicKey> AddPublicKeyAsync(string name, string publicKey)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(publicKey));
        await using var decodeStream = PgpUtilities.GetDecoderStream(stream);
        var pgpPub = new PgpPublicKeyRing(decodeStream);
        var key = pgpPub.GetPublicKey();
        if (key == null)
        {
            throw new InvalidOperationException("请提供有效的密钥。");
        }

        Logger.LogInformation("KeyId: {}", key.KeyId);
        var keyInfo = new GpgPublicKey()
        {
            KeyId = key.KeyId,
            Name = name,
            PublicKey = publicKey
        };
        await DbContext.PublicKeys.AddAsync(keyInfo);
        await DbContext.SaveChangesAsync();
        
        return keyInfo;
    }
}