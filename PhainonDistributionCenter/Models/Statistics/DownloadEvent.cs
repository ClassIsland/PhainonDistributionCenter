using PhainonDistributionCenter.Enums;

namespace PhainonDistributionCenter.Models.Statistics;

// Only value snapshots cross the request/background boundary. Never retain HttpContext or EF entities.
public sealed record DownloadEvent(DateTimeOffset RequestedAt, Guid DistributionId, string Version,
    string SubChannel, DownloadSource Source, string? IpAddress);

public sealed record DownloadRegion(string Code, string Name)
{
    public static readonly DownloadRegion Unknown = new("unknown", "未知来源");
}
