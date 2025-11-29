using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.FluentUI.AspNetCore.Components.Icons.Filled;
using PhainonDistributionCenter.Entities;
using PhainonDistributionCenter.Enums;
using PhainonDistributionCenter.Models;
using PhainonDistributionCenter.Models.CacheKeys;
using PhainonDistributionCenter.Services.Cache;
using PhainonDistributionCenter.Shared.Models.Api.Responses.Distribution;
using PhainonDistributionCenter.Shared.Models.Client;
using PhainonDistributionCenter.Shared.Models.FileMap;

namespace PhainonDistributionCenter.Services;

public class DistributionsService(
    MainDbContext dbContext,
    ILogger<DistributionsService> logger,
    DistributionCacheService distributionCacheService,
    GpgSignatureService gpgSignatureService)
{
    private MainDbContext DbContext { get; } = dbContext;
    private ILogger<DistributionsService> Logger { get; } = logger;
    private DistributionCacheService DistributionCacheService { get; } = distributionCacheService;
    private GpgSignatureService GpgSignatureService { get; } = gpgSignatureService;

    public async Task<LatestDistributionInfoWebResponse> GetWebLatestDistributionInfo()
    {
        if (DistributionCacheService.TryGetValue(DistributionCacheService.WebLatestRequestCacheKey, out var o) 
            && o is LatestDistributionInfoWebResponse cachedRsp)
        {
            return cachedRsp;
        }
        
        var channels = await DbContext.DistributionChannels
            .Where(x => x.IsEnabled)
            .Select(x => x)
            .ToListAsync();
        var channelsRsp = new Dictionary<Guid, LatestDistributionInfoWebResponse.ChannelInfoWeb>();
        foreach (var c in channels)
        {
            var latest = await GetLatestDistributionInfoByChannel(c.Id);
            if (latest == null)
            {
                continue;
            }
            var channel = new LatestDistributionInfoWebResponse.ChannelInfoWeb()
            {
                ChannelDescription = c.Description,
                ChannelName = c.Name,
                LatestVersion = latest.Version,
                LatestVersionId = latest.Id,
            };
            channelsRsp.Add(c.Id, channel);
        }

        var rsp = new LatestDistributionInfoWebResponse()
        {
            Channels = channelsRsp,
            DefaultChannel = channels.FirstOrDefault(x => x.IsDefault)?.Id ?? Guid.Empty
        };
        DistributionCacheService.SetMemoryCache(DistributionCacheService.WebLatestRequestCacheKey, rsp);
        return rsp;
    }

    public async Task<DistributionInfo?> GetLatestDistributionInfoByChannel(Guid channelId, Version? minVersion = null)
    {
        var key = new LatestDistributionCacheKey(channelId, minVersion);
        if (DistributionCacheService.TryGetValue(key, out var o) 
            && o is DistributionInfo cachedRsp)
        {
            return cachedRsp;
        }
        var latest = await DbContext.DistributionInfos
            .Include(x => x.Channels)
            .Where(x => x.IsEnabled 
                        && x.Channels.Any(y => y.Id == channelId) 
                        && (minVersion == null || 
                            (x.MinVersionMajor <= minVersion.Major
                             && x.MinVersionMinor <= minVersion.Minor
                             && x.MinVersionBuild <= minVersion.Build
                             && x.MinVersionRevision <= minVersion.Revision)))
            .OrderByDescending(x => x.VersionMajor)
            .ThenByDescending(x => x.VersionMinor)
            .ThenByDescending(x => x.VersionBuild)
            .ThenByDescending(x => x.VersionRevision)
            .ThenByDescending(x => x.CreatedTime)
            .FirstOrDefaultAsync();
        if (latest != null)
        {
            DistributionCacheService.SetMemoryCache(key, latest);
        }
        return latest;
    }

    public async Task<(DistributionInfo, DistributionSubChannel)?> GetSubChannelInfo(Guid id, string subChannelId)
    {
        var key = new SubChannelCacheKey(id, subChannelId);
        if (DistributionCacheService.TryGetValue(key, out var o) 
            && o is (DistributionInfo, DistributionSubChannel))
        {
            return ((DistributionInfo, DistributionSubChannel))o;
        }
        
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
            return null;
        }
        var subChannelInfo = info.SubChannels.First(y => $"{y.Os}_{y.Arch}_{y.BuildType}_{y.Package}" == subChannelId);

        var r = (info, subChannelInfo);
        DistributionCacheService.SetMemoryCache(key, r, true);
        return r;
    }

    public async Task<DistributionInfoWebResponse?> GetWebDistributionInfo(Guid id, string subChannelId)
    {
        var key = new DistributionCacheKey(id, subChannelId, ResponseType.Web);
        if (DistributionCacheService.TryGetValue(key, out var o) 
            && o is DistributionInfoWebResponse cachedRsp)
        {
            return cachedRsp;
        }

        var infos = await GetSubChannelInfo(id, subChannelId);
        if (infos == null)
        {
            return null;
        }

        var (info, subChannelInfo) = infos.Value;

        var (result, _) = await GpgSignatureService.CheckSignatureAsync(subChannelInfo.FileMapInfo.ContentJson,
            subChannelInfo.FileMapInfo.PgpSignature);
        if (!result)
        {
            return null;
        }

        var fileMap = JsonSerializer.Deserialize<FileMap>(subChannelInfo.FileMapInfo.ContentJson);
        if (fileMap == null)
        {
            return null;
        }
        var rsp = new DistributionInfoWebResponse()
        {
            Version = info.Version,
            ArchiveUrl = fileMap.ArchiveUrl,
            ArchiveSHA512 = Convert.ToHexString(fileMap.ArchiveSha512)
        };
        
        DistributionCacheService.SetMemoryCache(key, rsp);
        return rsp;
    }

    public async Task<DistributionMetadata> GetMetadata()
    {
        if (DistributionCacheService.TryGetValue(DistributionCacheService.MetadataCacheKey, out var o) 
            && o is DistributionMetadata cachedRsp)
        {
            return cachedRsp;
        }
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
        DistributionCacheService.SetMemoryCache(DistributionCacheService.MetadataCacheKey, metadata);
        return metadata;
    }
}