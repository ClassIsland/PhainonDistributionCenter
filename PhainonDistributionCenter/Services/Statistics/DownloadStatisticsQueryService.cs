using System.Data;
using Microsoft.EntityFrameworkCore;
using PhainonDistributionCenter.Enums;
using PhainonDistributionCenter.Models.Statistics;

namespace PhainonDistributionCenter.Services.Statistics;

public sealed class DownloadStatisticsQueryService(IDbContextFactory<MainDbContext> factory)
{
    public async Task<long> GetTotalAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.HourlyDownloadStatistics.SumAsync(x => x.Count, cancellationToken);
    }

    public async Task<DownloadStatisticsOptions> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var rows = db.HourlyDownloadStatistics.AsNoTracking();
        var versions = await rows.Select(x => x.Version).Distinct().OrderBy(x => x).ToListAsync(cancellationToken);
        var subChannels = await rows.Select(x => x.SubChannel).Distinct().OrderBy(x => x).ToListAsync(cancellationToken);
        var regions = await rows.GroupBy(x => x.RegionCode)
            .Select(g => new { Code = g.Key, Name = g.Max(x => x.RegionName)! })
            .OrderBy(x => x.Name).Select(x => new RegionOption(x.Code, x.Name)).ToListAsync(cancellationToken);
        return new(versions, subChannels, regions);
    }

    public async Task<DownloadStatisticsReport> GetAsync(DownloadStatisticsFilter filter, int pageIndex = 0,
        CancellationToken cancellationToken = default)
    {
        Validate(filter);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        // Every panel observes the same snapshot even while background writers are incrementing counts.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var rows = db.HourlyDownloadStatistics.AsNoTracking()
            .Where(x => x.HourUtc >= filter.StartUtc && x.HourUtc < filter.EndUtc);
        if (!string.IsNullOrEmpty(filter.Version)) rows = rows.Where(x => x.Version == filter.Version);
        if (!string.IsNullOrEmpty(filter.SubChannel)) rows = rows.Where(x => x.SubChannel == filter.SubChannel);
        if (filter.Source.HasValue) rows = rows.Where(x => x.Source == filter.Source.Value);
        if (!string.IsNullOrEmpty(filter.RegionCode)) rows = rows.Where(x => x.RegionCode == filter.RegionCode);

        var sources = await rows.GroupBy(x => x.Source)
            .Select(g => new { Source = g.Key, Count = g.Sum(x => x.Count) }).ToListAsync(cancellationToken);
        var total = sources.Sum(x => x.Count);
        var clients = sources.Where(x => x.Source == DownloadSource.Client).Sum(x => x.Count);
        var web = sources.Where(x => x.Source == DownloadSource.Web).Sum(x => x.Count);
        var sparseHours = await rows.GroupBy(x => x.HourUtc)
            .Select(g => new HourlyDownloadCount(g.Key, g.Sum(x => x.Count))).ToListAsync(cancellationToken);
        var versions = await rows.GroupBy(x => x.Version)
            .Select(g => new { Name = g.Key, Count = g.Sum(x => x.Count) })
            .OrderByDescending(x => x.Count).ThenBy(x => x.Name)
            .Select(x => new DownloadCount(x.Name, x.Count)).ToListAsync(cancellationToken);
        var subChannels = await rows.GroupBy(x => x.SubChannel)
            .Select(g => new { Name = g.Key, Count = g.Sum(x => x.Count) })
            .OrderByDescending(x => x.Count).ThenBy(x => x.Name)
            .Select(x => new DownloadCount(x.Name, x.Count)).ToListAsync(cancellationToken);
        var regions = await rows.GroupBy(x => x.RegionCode)
            .Select(g => new { Name = g.Max(x => x.RegionName)!, Count = g.Sum(x => x.Count) })
            .OrderByDescending(x => x.Count).ThenBy(x => x.Name)
            .Select(x => new DownloadCount(x.Name, x.Count)).ToListAsync(cancellationToken);
        var rowCount = await rows.CountAsync(cancellationToken);
        pageIndex = Math.Clamp(pageIndex, 0, Math.Max(0, (rowCount - 1) / DownloadStatisticsReport.PageSize));
        var details = await rows.OrderByDescending(x => x.HourUtc).ThenBy(x => x.Version)
            .ThenBy(x => x.DistributionId).ThenBy(x => x.SubChannel).ThenBy(x => x.Source).ThenBy(x => x.RegionCode)
            .Skip(pageIndex * DownloadStatisticsReport.PageSize).Take(DownloadStatisticsReport.PageSize)
            .ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(total, clients, web, FillHours(filter, sparseHours), versions, subChannels, regions, details, rowCount, pageIndex);
    }

    public static IReadOnlyList<HourlyDownloadCount> FillHours(DownloadStatisticsFilter filter,
        IEnumerable<HourlyDownloadCount> sparseHours)
    {
        Validate(filter);
        var counts = sparseHours.ToDictionary(x => x.HourUtc, x => x.Count);
        var result = new List<HourlyDownloadCount>();
        for (var hour = filter.StartUtc; hour < filter.EndUtc; hour = hour.AddHours(1))
            result.Add(new(hour, counts.GetValueOrDefault(hour)));
        return result;
    }

    private static void Validate(DownloadStatisticsFilter filter)
    {
        if (filter.StartUtc.Kind != DateTimeKind.Utc || filter.EndUtc.Kind != DateTimeKind.Utc ||
            filter.StartUtc.Ticks % TimeSpan.TicksPerHour != 0 || filter.EndUtc.Ticks % TimeSpan.TicksPerHour != 0 ||
            filter.StartUtc >= filter.EndUtc || filter.EndUtc == DateTime.MaxValue ||
            (filter.Source.HasValue && !Enum.IsDefined(filter.Source.Value)))
            throw new ArgumentException("请选择有效的整小时时间范围和下载来源。");
    }
}
