using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using PhainonDistributionCenter.Entities;
using PhainonDistributionCenter.Shared.Models;

namespace PhainonDistributionCenter.Services;

public class FileRepoProcessingService(
    MainDbContext dbContext,
    GpgSignatureService gpgSignatureService,
    ILogger<FileRepoProcessingService> logger)
{
    private static JsonSerializerOptions FileMapJsonSerializerOptions { get; } = JsonSerializerOptions.Web;
    
    private MainDbContext DbContext { get; } = dbContext;
    private GpgSignatureService GpgSignatureService { get; } = gpgSignatureService;
    private ILogger<FileRepoProcessingService> Logger { get; } = logger;

    public async Task AddFileRepoEntriesToFileRepoFromFileMap(FileRepo repo)
    {
        Logger.LogInformation("开始将文件仓中的文件条目添加到文件仓库条目 ({} 条)", repo.Items.Count);
        
        foreach (var (_, file) in repo.Items
                     .Where(x => !DbContext.FileRepoEntries.Any(y => y.FileSha512.SequenceEqual(x.Value.FileSha512)))
                     .ToList())
        {
            await DbContext.FileRepoEntries.AddAsync(new FileRepoEntry()
            {
                FileSha512 = file.FileSha512,
                ArchiveSha512 = file.ArchiveSha512,
                FileName = file.FileName,
                ArchiveDownloadUrl = file.ArchiveDownloadUrl
            });
        }

        await DbContext.SaveChangesAsync();
    }

    public IList<byte[]> GetFileRepoDiff(FileRepo repo)
    {
        var diff = repo.Items
            .Where(x => !DbContext.FileRepoEntries.Any(y => y.FileSha512.SequenceEqual(x.Value.FileSha512)))
            .ToList();
        return diff.Select(x => x.Value.FileSha512).ToList();
    }
}