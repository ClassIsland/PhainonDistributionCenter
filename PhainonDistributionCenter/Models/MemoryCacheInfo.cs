using Microsoft.Extensions.Caching.Memory;

namespace PhainonDistributionCenter.Models;

public class MemoryCacheInfo(string name, MemoryCache memoryCache, MemoryCacheStatistics? statistics, long? sizeLimit)
{
    public string Name { get; } = name;
    public MemoryCache MemoryCache { get; } = memoryCache;
    public long? SizeLimit { get; } = sizeLimit;
    public MemoryCacheStatistics Statistics { get; } = statistics ?? new MemoryCacheStatistics();
    
}