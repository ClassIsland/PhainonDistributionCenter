using Microsoft.AspNetCore.Mvc;
using PhainonDistributionCenter.Shared.Models.Client;

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
        return Ok();
    }
}