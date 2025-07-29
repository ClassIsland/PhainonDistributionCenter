using Microsoft.AspNetCore.Mvc;
using PhainonDistributionCenter.Services;
using PhainonDistributionCenter.Shared.Models;
using PhainonDistributionCenter.Shared.Models.Api.Responses;
using StatusCodes = PhainonDistributionCenter.Shared.Enums.Api.StatusCodes;

namespace PhainonDistributionCenter.Controllers;

[ApiController]
[Route("api/v1/fileMaps/")]
public class FileReposController(FileRepoProcessingService fileRepoProcessingService) : ControllerBase
{
    private FileRepoProcessingService FileRepoProcessingService { get; } = fileRepoProcessingService;

    [HttpPost("diff")]
    public IActionResult GetFileRepoDiff([FromBody] FileRepo body)
    {
        var result = FileRepoProcessingService.GetFileRepoDiff(body);
        return Ok(new Result<IList<byte[]>>(StatusCodes.Success, result));
    }
    
    [HttpPost("upload")]
    public async Task<IActionResult> UploadFileRepo([FromBody] FileRepo body)
    {
        await FileRepoProcessingService.AddFileRepoEntriesToFileRepoFromFileMap(body);
        return Ok(new Result(StatusCodes.Success));
    }
}