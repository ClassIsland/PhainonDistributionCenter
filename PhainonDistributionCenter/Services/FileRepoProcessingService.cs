using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.EntityFrameworkCore;
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
        
        var existingHashSet = await GetExistingHashSetAsync(repo);

        var newEntries = repo.Items.Values
            .Where(file => !existingHashSet.Contains(Convert.ToBase64String(file.FileSha512)))
            .Select(file => new FileRepoEntry
            {
                FileSha512 = file.FileSha512,
                ArchiveSha512 = file.ArchiveSha512,
                FileName = file.FileName,
                ArchiveDownloadUrl = file.ArchiveDownloadUrl
            })
            .ToList();

        if (newEntries.Count > 0)
        {
            await DbContext.FileRepoEntries.AddRangeAsync(newEntries);
            await DbContext.SaveChangesAsync();
        }
    }

    public async Task<IList<byte[]>> GetFileRepoDiffAsync(FileRepo repo)
    {
        var existingHashSet = await GetExistingHashSetAsync(repo);

        return repo.Items.Values
            .Where(file => !existingHashSet.Contains(Convert.ToBase64String(file.FileSha512)))
            .Select(file => file.FileSha512)
            .ToList();
    }

    /// <summary>
    /// 一次性查询数据库，返回 repo.Items 中已存在的 FileSha512 的 Base64 集合。
    /// </summary>
    private async Task<HashSet<string>> GetExistingHashSetAsync(FileRepo repo)
    {
        var allHashes = repo.Items.Values.Select(x => x.FileSha512).ToList();

        var existingHashes = await DbContext.FileRepoEntries
            .Where(e => allHashes.Contains(e.FileSha512))
            .Select(e => e.FileSha512)
            .ToListAsync();

        return existingHashes.Select(Convert.ToBase64String).ToHashSet();
    }
}