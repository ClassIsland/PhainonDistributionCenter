using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using PhainonDistributionCenter.Entities;
using PhainonDistributionCenter.Enums;
using PhainonDistributionCenter.Models.Statistics;
using PhainonDistributionCenter.Services.Statistics;
using Xunit;

namespace PhainonDistributionCenter.Tests;

public class PostgresDownloadStatisticsTests
{
    [PostgresFact]
    public async Task MigrationsConcurrentUpsertsFiltersAndHistoryWorkOnPostgres()
    {
        // Only the uniquely named database created here is ever migrated or dropped.
        var databaseName = "pdc_download_test_" + Guid.NewGuid().ToString("N");
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PDC_TEST_POSTGRES")!);
        await using var admin = new NpgsqlConnection(connection.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", admin)) await create.ExecuteNonQueryAsync();
        connection.Database = databaseName;
        connection.Pooling = false;
        try
        {
            var factory = new TestDbFactory(connection.ConnectionString);
            await using var db = factory.CreateDbContext();
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20251116013828_AddMinVersion");
            var version = new VersionInfo { Id = Guid.NewGuid(), Version = "2.0" };
            var release = new DistributionInfo { Id = DownloadStatisticsTests.ReleaseId, Version = "2.0.0.0", VersionInfo = version };
            db.DistributionInfos.Add(release);
            await db.SaveChangesAsync();
            await migrator.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
            Assert.Equal(1, await db.DistributionInfos.CountAsync());
            var start = DownloadStatisticsTime.ToUtcHour(DownloadStatisticsTests.RequestedAt);
            HourlyDownloadStatistic Row(DownloadSource source, long count = 1, DateTime? hour = null,
                string subChannel = "windows_x64_full_folder", string releaseVersion = "2.0.0.0", string region = "CN/SH") => new()
            {
                HourUtc = hour ?? start, DistributionId = release.Id, Version = releaseVersion,
                SubChannel = subChannel, Source = source, RegionCode = region,
                RegionName = region == "CN/SH" ? "中国大陆／上海市" : region == "US" ? "美国" : "未知来源", Count = count
            };

            // Separate contexts/writer instances model multiple application instances.
            await Task.WhenAll(Enumerable.Range(0, 24).Select(_ =>
                new DownloadStatisticsWriter(factory).WriteAsync([Row(DownloadSource.Client, 3)], default)));
            var writer = new DownloadStatisticsWriter(factory);
            await writer.WriteAsync([
                Row(DownloadSource.Web, 5),
                Row(DownloadSource.Web, 7, start.AddHours(1), releaseVersion: "2.1.0.0", region: "US"),
                Row(DownloadSource.Client, (long)int.MaxValue + 3, start.AddHours(2))
            ], default);
            await writer.WriteAsync(Enumerable.Range(0, 33).Select(i =>
                Row(DownloadSource.Client, hour: start.AddHours(1), subChannel: $"channel-{i:00}", region: "unknown")).ToArray(), default);
            var query = new DownloadStatisticsQueryService(factory);
            var filter = new DownloadStatisticsFilter(start, start.AddHours(2));
            var options = await query.GetOptionsAsync();
            Assert.Equal(2, options.Versions.Count);
            Assert.Equal(34, options.SubChannels.Count);
            Assert.Equal(3, options.Regions.Count);
            var report = await query.GetAsync(filter);
            Assert.Equal(117, report.Total);
            Assert.Equal(105, report.ClientCount);
            Assert.Equal(12, report.WebCount);
            Assert.Equal(new long[] { 77, 40 }, report.Hours.Select(x => x.Count));
            Assert.Equal(report.Total, report.Versions.Sum(x => x.Count));
            Assert.Equal(report.Total, report.SubChannels.Sum(x => x.Count));
            Assert.Equal(report.Total, report.Regions.Sum(x => x.Count));
            Assert.Equal(36, report.TotalRows);
            Assert.Equal(25, report.Details.Count);
            var second = await query.GetAsync(filter, 1);
            Assert.Equal(11, second.Details.Count);
            Assert.Empty(report.Details.Intersect(second.Details, new StatisticKeyComparer()));
            var filtered = await query.GetAsync(filter with { Source = DownloadSource.Client, Version = "2.0.0.0", SubChannel = "windows_x64_full_folder", RegionCode = "CN/SH" });
            Assert.Equal(72, filtered.Total);
            Assert.Equal(0, filtered.WebCount);
            Assert.Single(filtered.Details);
            var empty = await query.GetAsync(filter with { RegionCode = "nonexistent" });
            Assert.Equal(0, empty.Total);
            Assert.All(empty.Hours, x => Assert.Equal(0, x.Count));
            Assert.Empty(empty.Details);
            Assert.Equal(0, empty.PageIndex);
            Assert.Equal(1, (await query.GetAsync(filter, int.MaxValue)).PageIndex);
            Assert.Equal(117L + int.MaxValue + 3, await query.GetTotalAsync());

            // A bad row makes the entire statement fail; valid rows in that batch cannot partially commit.
            var invalid = Row(DownloadSource.Web, subChannel: new string('x', 68));
            await Assert.ThrowsAsync<PostgresException>(() => writer.WriteAsync([Row(DownloadSource.Client, 10), invalid], default));
            Assert.Equal(117, (await query.GetAsync(filter)).Total);
            db.DistributionInfos.Remove(release);
            await db.SaveChangesAsync();
            Assert.Equal(117, (await query.GetAsync(filter)).Total);
            await migrator.MigrateAsync("20251116013828_AddMinVersion");
            Assert.Equal(1, await db.VersionInfos.CountAsync());
            await migrator.MigrateAsync();
            Assert.Equal(0, await query.GetTotalAsync());
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{databaseName}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class TestDbFactory(string connectionString) : IDbContextFactory<MainDbContext>
    {
        public MainDbContext CreateDbContext() => new(new DbContextOptionsBuilder<MainDbContext>().UseNpgsql(connectionString).Options);
    }

    private sealed class StatisticKeyComparer : IEqualityComparer<HourlyDownloadStatistic>
    {
        private static object Key(HourlyDownloadStatistic x) => new { x.HourUtc, x.DistributionId, x.Version, x.SubChannel, x.Source, x.RegionCode };
        public bool Equals(HourlyDownloadStatistic? x, HourlyDownloadStatistic? y) => x != null && y != null && Key(x).Equals(Key(y));
        public int GetHashCode(HourlyDownloadStatistic obj) => Key(obj).GetHashCode();
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PDC_TEST_POSTGRES")))
            Skip = "Set PDC_TEST_POSTGRES to a disposable PostgreSQL instance to run integration tests.";
    }
}
