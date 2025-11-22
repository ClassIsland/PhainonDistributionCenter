using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhainonDistributionCenter.Services;
using PhainonDistributionCenter.Shared.Models.Api.Responses;
using PhainonDistributionCenter.Shared.Models.Api.Responses.Distribution;
using PhainonDistributionCenter.Shared.Models.Client;
using StatusCodes = PhainonDistributionCenter.Shared.Enums.Api.StatusCodes;

namespace PhainonDistributionCenter.Controllers.Public;

[ApiController]
[Route("/api/v1/public/distributions")]
public class DistributionsController(MainDbContext dbContext, ILogger<DistributionsController> logger, DistributionsService distributionsService) : ControllerBase
{
    public MainDbContext DbContext { get; } = dbContext;
    public ILogger<DistributionsController> Logger { get; } = logger;
    private DistributionsService DistributionsService { get; } = distributionsService;

    [HttpGet("web")]
    public async Task<IActionResult> GetDistributionInfoMinWeb()
    {
        return Ok(new Result<LatestDistributionInfoWebResponse>(StatusCodes.Success, await DistributionsService.GetWebLatestDistributionInfo())); 
    }
    
    [HttpGet("web/{versionId:Guid}/{subChannelId}")]
    public async Task<IActionResult> GetDistributionInfoWeb([FromRoute] Guid versionId, string subChannelId)
    {
        var response = await DistributionsService.GetWebDistributionInfo(versionId, subChannelId);
        if (response == null)
        {
            return NotFound(new Result(StatusCodes.DistributionNotFound, $"找不到分发信息 {versionId}/{subChannelId} "));
        }
        return Ok(new Result<DistributionInfoWebResponse>(StatusCodes.Success, response));
    }

    [HttpGet("metadata")]
    public async Task<IActionResult> GetMetadata()
    {
        var metadata = await DistributionsService.GetMetadata();
        return Ok(new Result<DistributionMetadata>(StatusCodes.Success, metadata));
    }

    [HttpGet("latest/{channelId:guid}")]
    public async Task<IActionResult> GetLatestDistributionInfoMin([FromRoute] Guid channelId, [FromQuery] string? appVersion)
    {
        if (!Version.TryParse(appVersion ?? "0.0.0.0", out var version))
        {
            return BadRequest(new Result(StatusCodes.DistributionInvalidClientVersionCode, $"客户端版本 {appVersion} 无效。"));
        }
        var latest = await DistributionsService.GetLatestDistributionInfoByChannel(channelId, version);
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
        var infos = await DistributionsService.GetSubChannelInfo(id, subChannelId);
        if (infos == null)
        {
            return NotFound(new Result(StatusCodes.DistributionNotFound, $"找不到分发信息 {id}/{subChannelId} "));
        }
        var (info, subChannelInfo) = infos.Value;
        
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