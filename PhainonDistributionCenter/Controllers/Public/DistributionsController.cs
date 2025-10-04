using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhainonDistributionCenter.Shared.Models.Api.Responses;
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

    [HttpGet("latest/{channelId:guid}/{subChannel}")]
    public async Task<IActionResult> GetLatestDistributionInfoMin([FromRoute] Guid channelId, [FromRoute] string subChannel)
    {
        var latest = await DbContext.DistributionInfos
            .Include(x => x.Channels)
            .Include(x => x.SubChannels)
            .ThenInclude(x => x.FileMapInfo)
            .Where(x => x.IsEnabled 
                        && x.Channels.Any(y => y.Id == channelId)
                        && x.SubChannels.Any(y => y.Os + "_" + y.Arch + "_" + y.BuildType + "_" + y.Package == subChannel))
            .OrderByDescending(x => x.VersionMajor)
            .ThenByDescending(x => x.VersionMinor)
            .ThenByDescending(x => x.VersionBuild)
            .ThenByDescending(x => x.VersionRevision)
            .Include(x => x.VersionInfo)
            .FirstOrDefaultAsync();
        if (latest == null)
        {
            return NotFound(new Result(StatusCodes.NoDistributionsFound, $"找不到频道 {channelId}/{subChannel} 上符合要求的最新发行版"));
        }

        var subChannelInfo = latest.SubChannels.First(y => $"{y.Os}_{y.Arch}_{y.BuildType}_{y.Package}" == subChannel);
        
        return Ok(new Result<DistributionInfoClient>(StatusCodes.Success, new DistributionInfoClient
        {
            FriendlyVersion = latest.FriendlyVersion,
            FriendlyVersionShort = latest.FriendlyVersionShort,
            Version = latest.Version,
            ChangeLog = latest.ChangeLog,
            SubChannel = $"{subChannelInfo.Os}_{subChannelInfo.Arch}_{subChannelInfo.BuildType}_{subChannelInfo.Package}",
            FileMapJson = subChannelInfo.FileMapInfo.ContentJson,
            FileMapSignature = subChannelInfo.FileMapInfo.PgpSignature
        })); 
    }
}