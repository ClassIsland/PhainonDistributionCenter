using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PhainonDistributionCenter.Entities;

namespace PhainonDistributionCenter.Services.Statistics;

public interface IDownloadStatisticsWriter
{
    Task WriteAsync(IReadOnlyCollection<HourlyDownloadStatistic> rows, CancellationToken cancellationToken);
}

public sealed class DownloadStatisticsWriter(IDbContextFactory<MainDbContext> factory) : IDownloadStatisticsWriter
{
    public async Task WriteAsync(IReadOnlyCollection<HourlyDownloadStatistic> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0) return;
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        db.Database.SetCommandTimeout(TimeSpan.FromSeconds(5));
        var sql = new StringBuilder("""
            INSERT INTO "HourlyDownloadStatistics"
            ("HourUtc", "DistributionId", "Version", "SubChannel", "Source", "RegionCode", "RegionName", "Count") VALUES
            """);
        var parameters = new List<object>();
        foreach (var row in rows)
        {
            if (parameters.Count > 0) sql.Append(',');
            object[] values = [row.HourUtc, row.DistributionId, row.Version, row.SubChannel,
                (int)row.Source, row.RegionCode, row.RegionName, row.Count];
            sql.Append('(');
            for (var i = 0; i < values.Length; i++)
            {
                if (i > 0) sql.Append(',');
                var name = $"p{parameters.Count}";
                sql.Append('@').Append(name);
                parameters.Add(new NpgsqlParameter(name, values[i]));
            }
            sql.Append(')');
        }
        sql.Append("""
             ON CONFLICT ("HourUtc", "DistributionId", "Version", "SubChannel", "Source", "RegionCode")
             DO UPDATE SET "Count" = "HourlyDownloadStatistics"."Count" + EXCLUDED."Count"
            """);
        // One atomic statement; no retry after an ambiguous commit acknowledgement.
        await db.Database.ExecuteSqlRawAsync(sql.ToString(), parameters, cancellationToken);
    }
}
