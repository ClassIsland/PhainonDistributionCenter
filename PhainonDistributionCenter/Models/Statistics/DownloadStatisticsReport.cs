using PhainonDistributionCenter.Entities;
using PhainonDistributionCenter.Enums;

namespace PhainonDistributionCenter.Models.Statistics;

public sealed record DownloadStatisticsFilter(DateTime StartUtc, DateTime EndUtc, string? Version = null,
    string? SubChannel = null, DownloadSource? Source = null, string? RegionCode = null);

public sealed record DownloadCount(string Name, long Count);
public sealed record HourlyDownloadCount(DateTime HourUtc, long Count);
public sealed record RegionOption(string Code, string Name);
public sealed record DownloadStatisticsOptions(List<string> Versions, List<string> SubChannels, List<RegionOption> Regions);

public sealed record DownloadStatisticsReport(long Total, long ClientCount, long WebCount,
    IReadOnlyList<HourlyDownloadCount> Hours, List<DownloadCount> Versions, List<DownloadCount> SubChannels,
    List<DownloadCount> Regions, List<HourlyDownloadStatistic> Details, int TotalRows, int PageIndex)
{
    public const int PageSize = 25;
    public int PageCount => Math.Max(1, (int)Math.Ceiling((double)TotalRows / PageSize));
}
