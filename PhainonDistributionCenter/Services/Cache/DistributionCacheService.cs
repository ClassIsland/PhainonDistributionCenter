using Microsoft.Extensions.Caching.Memory;
using PhainonDistributionCenter.Shared.Models.Api.Responses.Distribution;

namespace PhainonDistributionCenter.Services.Cache;

public class DistributionCacheService(ILoggerFactory loggerFactory)
{
    public MemoryCache MemoryCache { get; } = new MemoryCache(new MemoryCacheOptions()
    {
        
    }, loggerFactory);

    public static readonly string MetadataCacheKey = "metadata"; 

    public MemoryCacheEntryOptions DefaultEntryOptions { get; } = new MemoryCacheEntryOptions()
        .SetSlidingExpiration(TimeSpan.FromHours(24));
    
    private volatile LatestDistributionInfoWebResponse? _webRequestCache;

    public  LatestDistributionInfoWebResponse? WebRequestCache
    {
        get => _webRequestCache;
        set => _webRequestCache = value;
    }

    public void SetMemoryCache(object key, object? value) => MemoryCache.Set(key, value, DefaultEntryOptions);

    public void InvalidateCache()
    {
        WebRequestCache = null;
        MemoryCache.Clear();
    }
}