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
        return Ok(metadata);
    }

    [HttpGet("latest/{channelId:guid}")]
    public async Task<IActionResult> GetLatestDistributionInfoMin([FromRoute] Guid channelId)
    {
        return Ok();
    }
}