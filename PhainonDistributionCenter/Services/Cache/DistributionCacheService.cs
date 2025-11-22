using Microsoft.Extensions.Caching.Memory;
using PhainonDistributionCenter.Shared.Models.Api.Responses.Distribution;

namespace PhainonDistributionCenter.Services.Cache;

public class DistributionCacheService(ILoggerFactory loggerFactory)
{
    public const long CacheSizeLimit = 1024; 
    
    public MemoryCache MemoryCache { get; } = new MemoryCache(new MemoryCacheOptions()
    {
        SizeLimit = CacheSizeLimit,
        CompactionPercentage = .25,
        TrackStatistics = true
    }, loggerFactory);

    public static readonly string MetadataCacheKey = "metadata"; 
    public static readonly string WebLatestRequestCacheKey = "web"; 

    public MemoryCacheEntryOptions DefaultEntryOptions { get; } = new MemoryCacheEntryOptions()
        .SetSlidingExpiration(TimeSpan.FromHours(24))
        .SetSize(1);
    
    public MemoryCacheEntryOptions LargeEntryOptions { get; } = new MemoryCacheEntryOptions()
        .SetSlidingExpiration(TimeSpan.FromHours(12))
        .SetSize(24);

    public void SetMemoryCache(object key, object? value, bool large = false) =>
        MemoryCache.Set(key, value, large ? LargeEntryOptions : DefaultEntryOptions);

    public void InvalidateCache()
    {
        MemoryCache.Clear();
    }
}