using System.Diagnostics.CodeAnalysis;
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

    public void SetMemoryCache(object key, object? value, bool large = false)
    {
         var cacheSpan = SentrySdk.GetSpan()?.StartChild("cache.put");
        // Set the key you're going to use to add to the cache
        cacheSpan?.SetExtra("cache.key", key);
        MemoryCache.Set(key, value, large ? LargeEntryOptions : DefaultEntryOptions);
        cacheSpan?.Finish();
    }

    public bool TryGetValue(object key, [NotNullWhen(true)] out object? o)
    {
        var cacheSpan = SentrySdk.GetSpan()?.StartChild("cache.get");
        // Set the key you're going to use to retrieve from the cache
        cacheSpan?.SetExtra("cache.key", key);
        var hit = MemoryCache.TryGetValue(key, out o);
        cacheSpan?.SetExtra("cache.hit", hit);
        cacheSpan?.Finish();
        return hit;
    }

    public void InvalidateCache()
    {
        MemoryCache.Clear();
    }
}