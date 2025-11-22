using PhainonDistributionCenter.Enums;

namespace PhainonDistributionCenter.Models.CacheKeys;

public record DistributionCacheKey(Guid Id, string SubChannel, ResponseType ResponseType)
{
}