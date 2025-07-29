using PhainonDistributionCenter.Entities;
using PhainonDistributionCenter.Shared.Models;

namespace PhainonDistributionCenter.Services;

public class FileMapProcessingService(MainDbContext dbContext, GpgSignatureService gpgSignatureService)
{
    private MainDbContext DbContext { get; } = dbContext;
    private GpgSignatureService GpgSignatureService { get; } = gpgSignatureService;
    
}