using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhainonDistributionCenter.Shared.Models.Api.Responses;
using PhainonDistributionCenter.Shared.Models.Api.Responses.Distribution;
using PhainonDistributionCenter.Shared.Models.Client;
using StatusCodes = PhainonDistributionCenter.Shared.Enums.Api.StatusCodes;

namespace PhainonDistributionCenter.Controllers.Public;

[ApiController]
[Route("/api/v1/public/distributions")]
public class DistributionsController(MainDbContext dbContext, ILogger<DistributionsController> logger) : ControllerBase
{
    public MainDbContext DbContext { get; } = dbContext;
    public ILogger<DistributionsController> Logger { get; } = logger;

    [HttpGet("metadata")]
    public async Task<IActionResult> GetMetadata()
    {
        var metadata = new DistributionMetadata()
        {
            Channels = await DbContext.DistributionChannels
                .Where(x => x.IsEnabled)
                .Select(x => x)
                .ToDictionaryAsync(x => x.Id, x => new DistributionMetadata.DistributionChannel()
                {
                    Name = x.Name,
                    Description = x.Description
                }),
            DefaultChannelId = (await DbContext.DistributionChannels
                .FirstOrDefaultAsync(x => x.IsDefault && x.IsEnabled))?.Id ?? Guid.Empty
        };
        return Ok(new Result<DistributionMetadata>(StatusCodes.Success, metadata));
    }

    [HttpGet("latest/{channelId:guid}")]
    public async Task<IActionResult> GetLatestDistributionInfoMin([FromRoute] Guid channelId, [FromQuery] string? appVersion)
    {
        if (!Version.TryParse(appVersion ?? "0.0.0.0", out var version))
        {
            return BadRequest(new Result(StatusCodes.DistributionInvalidClientVersionCode, $"客户端版本 {appVersion} 无效。"));
        }
        var latest = await DbContext.DistributionInfos
            .Include(x => x.Channels)
            .Where(x => x.IsEnabled 
                        && x.Channels.Any(y => y.Id == channelId) 
                        && x.MinVersionMajor <= version.Major
                        && x.MinVersionMinor <= version.Minor
                        && x.MinVersionBuild <= version.Build
                        && x.MinVersionRevision <= version.Revision)
            .OrderByDescending(x => x.VersionMajor)
            .ThenByDescending(x => x.VersionMinor)
            .ThenByDescending(x => x.VersionBuild)
            .ThenByDescending(x => x.VersionRevision)
            .ThenByDescending(x => x.CreatedTime)
            .FirstOrDefaultAsync();
        if (latest == null)
        {
            return NotFound(new Result(StatusCodes.NoDistributionsAvailable, $"找不到频道 {channelId}/ 上符合要求的最新发行版"));
        }
        
        return Ok(new Result<LatestDistributionInfoMinResponse>(StatusCodes.Success, new LatestDistributionInfoMinResponse
        {
            DistributionId = latest.Id,
            Version = latest.Version
        })); 
    }

    [HttpGet("{id:guid}/{subChannelId}")]
    public async Task<IActionResult> GetDistributionInfoClient([FromRoute] Guid id, [FromRoute] string subChannelId)
    {
        var info = await DbContext.DistributionInfos
            .Include(x => x.SubChannels)
            .ThenInclude(x => x.FileMapInfo)
            .Where(x => x.Id == id && x.IsEnabled &&
                        x.SubChannels.Any(y =>
                            y.Os + "_" + y.Arch + "_" + y.BuildType + "_" + y.Package == subChannelId))
            .Include(x => x.VersionInfo)
            .FirstOrDefaultAsync();
        if (info == null)
        {
            return NotFound(new Result(StatusCodes.DistributionNotFound, $"找不到分发信息 {id}/{subChannelId} "));
        }
        
        var subChannelInfo = info.SubChannels.First(y => $"{y.Os}_{y.Arch}_{y.BuildType}_{y.Package}" == subChannelId);
        
        return Ok(new Result<DistributionInfoClient>(StatusCodes.Success, new DistributionInfoClient
        {
            FriendlyVersion = info.FriendlyVersion,
            FriendlyVersionShort = info.FriendlyVersionShort,
            Version = info.Version,
            ChangeLog = info.ChangeLog,
            SubChannel = $"{subChannelInfo.Os}_{subChannelInfo.Arch}_{subChannelInfo.BuildType}_{subChannelInfo.Package}",
            FileMapJson = subChannelInfo.FileMapInfo.ContentJson,
            FileMapSignature = subChannelInfo.FileMapInfo.PgpSignature
        })); 
    }
}