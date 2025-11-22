namespace PhainonDistributionCenter.Models.CacheKeys;

public record LatestDistributionCacheKey(Guid ChannelId, Version? MinVersion)
{
}