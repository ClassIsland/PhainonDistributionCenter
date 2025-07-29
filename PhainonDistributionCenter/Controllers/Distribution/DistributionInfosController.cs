using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhainonDistributionCenter.Entities;
using PhainonDistributionCenter.Services;
using PhainonDistributionCenter.Shared.Models;
using PhainonDistributionCenter.Shared.Models.Api;
using StatusCodes = PhainonDistributionCenter.Shared.Enums.Api.StatusCodes;

namespace PhainonDistributionCenter.Controllers.Distribution;

[ApiController]
[Route("api/v1/distribution/")]
public class DistributionInfosController(
    MainDbContext dbContext,
    FileMapProcessingService fileMapProcessingService,
    GpgSignatureService gpgSignatureService) : ControllerBase
{
    private GpgSignatureService SignatureService { get; } = gpgSignatureService;
    private MainDbContext DbContext { get; } = dbContext;
    
    private FileMapProcessingService FileMapProcessingService { get; } = fileMapProcessingService;

    [HttpPost("{primaryVersion}/{version}")]
    public async Task<IActionResult> AddDistribution(string primaryVersion, string version, [FromBody] AddDistributionInfoRequest body)
    {
        if (!Version.TryParse(primaryVersion, out _) || !Version.TryParse(version, out _))
        {
            return BadRequest(new Result(StatusCodes.AddDistributionInvalidVersion, "无效的版本名"));
        }

        var versionInfo = await DbContext.VersionInfos.FirstOrDefaultAsync(x => x.Version == version);
        if (versionInfo == null)
        {
            return BadRequest(new Result(StatusCodes.AddDistributionPrimaryVersionNotFound,
                $"找不到请求的大版本 {primaryVersion}"));
        }

        var distributionInfo = new DistributionInfo()
        {
            Version = version,
            ChangeLog = body.ChangeLog,
            Channels = [],
            VersionInfo = versionInfo
        };
        foreach (var subChannel in body.SubChannels)
        {
            var (verifySuccess, keyInfo) =
                await SignatureService.CheckSignatureAsync(subChannel.FileMap, subChannel.FileMapSignature);
            if (!verifySuccess || keyInfo == null)
            {
                return BadRequest(new Result<object>(StatusCodes.GpgSignatureVerifyFailed, new
                    {
                        FailedSubChannel = subChannel
                    },$"子频道 {subChannel} 的文件图签名不正确"));
            }
            distributionInfo.SubChannels.Add(new DistributionSubChannel()
            {
                Os = subChannel.Os,
                Arch = subChannel.Arch,
                BuildType = subChannel.BuildType,
                Package = subChannel.Package,
                DistributionInfo = distributionInfo,
                FileMapInfo = new FileMapInfo()
                {
                    PublicKey = keyInfo,
                    PgpSignature = subChannel.FileMapSignature,
                    ContentJson = subChannel.FileMap
                }
            });
            
        }

        await DbContext.DistributionInfos.AddAsync(distributionInfo);
        await DbContext.SaveChangesAsync();
        return Ok(new Result(StatusCodes.Success));
    }
}