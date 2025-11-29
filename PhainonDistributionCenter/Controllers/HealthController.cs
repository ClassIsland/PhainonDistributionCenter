using Microsoft.AspNetCore.Mvc;

namespace PhainonDistributionCenter.Controllers;

[ApiController]
[Route("api/v1/health/")]
public class HealthController() : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok();
    }
}