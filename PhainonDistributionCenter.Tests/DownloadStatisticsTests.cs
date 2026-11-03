using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PhainonDistributionCenter.Components.Pages;
using PhainonDistributionCenter.Controllers.Public;
using PhainonDistributionCenter.Entities;
using PhainonDistributionCenter.Enums;
using PhainonDistributionCenter.Models.CacheKeys;
using PhainonDistributionCenter.Models.Statistics;
using PhainonDistributionCenter.Security;
using PhainonDistributionCenter.Services;
using PhainonDistributionCenter.Services.Cache;
using PhainonDistributionCenter.Services.Statistics;
using PhainonDistributionCenter.Shared.Models.Api.Responses.Distribution;
using PhainonDistributionCenter.Shared.Models.Client;
using Xunit;

namespace PhainonDistributionCenter.Tests;

public class DownloadStatisticsTests
{
    internal static readonly Guid ReleaseId = Guid.Parse("a67934a1-0319-472a-ab8b-1bb4f16a5eb8");
    internal static readonly DateTimeOffset RequestedAt = new(2026, 11, 3, 15, 59, 59, TimeSpan.Zero);
    internal static DownloadEvent Event(DownloadSource source = DownloadSource.Client) =>
        new(RequestedAt, ReleaseId, "2.0.0.0", "windows_x64_full_folder", source, "8.8.8.8");
    internal static DownloadEventQueue Queue() => new(NullLogger<DownloadEventQueue>.Instance);

    [Fact]
    public async Task CachedDetailsCountEveryRequestAndUseInterfaceSource()
    {
        using var fixture = new ControllerFixture();
        fixture.Controller.Request.Headers.UserAgent = "misleading-browser-or-client";
        for (var i = 0; i < 2; i++)
        {
            Assert.IsType<OkObjectResult>(await fixture.Controller.GetDistributionInfoWeb(ReleaseId, fixture.SubChannel));
            Assert.IsType<OkObjectResult>(await fixture.Controller.GetDistributionInfoClient(ReleaseId, fixture.SubChannel));
        }
        var events = new List<DownloadEvent>();
        while (fixture.Queue.Reader.TryRead(out var item)) events.Add(item);
        Assert.Equal(4, events.Count);
        Assert.Equal(2, events.Count(x => x.Source == DownloadSource.Web));
        Assert.Equal(2, events.Count(x => x.Source == DownloadSource.Client));
        Assert.All(events, x => { Assert.Equal(RequestedAt, x.RequestedAt); Assert.Equal("8.8.8.8", x.IpAddress); });
    }

    [Fact]
    public async Task MetadataLatestAndMissingDetailsDoNotCount()
    {
        using var fixture = new ControllerFixture();
        Assert.IsType<OkObjectResult>(await fixture.Controller.GetMetadata());
        Assert.IsType<OkObjectResult>(await fixture.Controller.GetDistributionInfoMinWeb());
        Assert.IsType<OkObjectResult>(await fixture.Controller.GetLatestDistributionInfoMin(fixture.ChannelId, null));
        Assert.IsType<BadRequestObjectResult>(await fixture.Controller.GetLatestDistributionInfoMin(fixture.ChannelId, "bad-version"));
        Assert.IsType<NotFoundObjectResult>(await fixture.Controller.GetDistributionInfoWeb(Guid.NewGuid(), fixture.SubChannel));
        Assert.IsType<NotFoundObjectResult>(await fixture.Controller.GetDistributionInfoClient(Guid.NewGuid(), fixture.SubChannel));
        Assert.False(fixture.Queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task FullOrClosedQueueNeverBlocksDetails()
    {
        using var fixture = new ControllerFixture();
        for (var i = 0; i < DownloadEventQueue.Capacity; i++) Assert.True(fixture.Queue.TryRecord(Event()));
        Assert.IsType<OkObjectResult>(await fixture.Controller.GetDistributionInfoWeb(ReleaseId, fixture.SubChannel).WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(1, fixture.Queue.DroppedEvents);
        fixture.Queue.Complete();
        Assert.IsType<OkObjectResult>(await fixture.Controller.GetDistributionInfoClient(ReleaseId, fixture.SubChannel).WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(2, fixture.Queue.DroppedEvents);
    }

    [Fact]
    public void OverflowDoesNotInvokeLogProvidersOnRequestThread()
    {
        var queue = new DownloadEventQueue(new ThrowingLogger());
        queue.Complete();
        Assert.False(queue.TryRecord(Event()));
        Assert.Equal(1, queue.DroppedEvents);
    }

    [Fact]
    public async Task SlowDatabaseDoesNotBlockDetailsAndShutdownDrains()
    {
        using var fixture = new ControllerFixture();
        var writer = new RecordingWriter { Block = true };
        using var worker = new DownloadStatisticsWorker(fixture.Queue, new FixedResolver(), writer, NullLogger<DownloadStatisticsWorker>.Instance);
        fixture.Queue.TryRecord(Event());
        await worker.StartAsync(default);
        await writer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsType<OkObjectResult>(await fixture.Controller.GetDistributionInfoWeb(ReleaseId, fixture.SubChannel).WaitAsync(TimeSpan.FromSeconds(2)));
        writer.Release.TrySetResult();
        await worker.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, writer.Rows.Sum(x => x.Count));
        Assert.Equal(0, fixture.Queue.DroppedEvents);
    }

    [Fact]
    public async Task SlowGeolocationDoesNotBlockDetails()
    {
        using var fixture = new ControllerFixture();
        using var resolver = new BlockingResolver();
        var writer = new RecordingWriter();
        using var worker = new DownloadStatisticsWorker(fixture.Queue, resolver, writer, NullLogger<DownloadStatisticsWorker>.Instance);
        fixture.Queue.TryRecord(Event());
        await worker.StartAsync(default);
        try
        {
            await resolver.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsType<OkObjectResult>(await fixture.Controller.GetDistributionInfoClient(ReleaseId, fixture.SubChannel).WaitAsync(TimeSpan.FromSeconds(2)));
        }
        finally { resolver.Release.Set(); }
        await worker.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, writer.Rows.Sum(x => x.Count));
    }

    [Fact]
    public async Task ShutdownCancellationStopsPendingWritesAndAccountsForDroppedEvents()
    {
        var queue = Queue();
        var writer = new RecordingWriter { Block = true };
        using var worker = new DownloadStatisticsWorker(queue, new FixedResolver(), writer, NullLogger<DownloadStatisticsWorker>.Instance);
        queue.TryRecord(Event());
        await worker.StartAsync(default);
        await writer.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        queue.TryRecord(Event(DownloadSource.Web));
        using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await worker.StopAsync(deadline.Token).WaitAsync(TimeSpan.FromSeconds(2));
        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(2, queue.DroppedEvents);
        Assert.False(queue.TryRecord(Event()));
        Assert.Equal(3, queue.DroppedEvents);
    }

    [Fact]
    public async Task WorkerGroupsByRequestHourAndSourceAndDoesNotRetryFailedBatch()
    {
        var queue = Queue();
        var writer = new RecordingWriter();
        using (var worker = new DownloadStatisticsWorker(queue, new FixedResolver(), writer, NullLogger<DownloadStatisticsWorker>.Instance))
        {
            queue.TryRecord(Event()); queue.TryRecord(Event()); queue.TryRecord(Event(DownloadSource.Web));
            queue.Complete();
            await worker.StartAsync(default);
            await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(2, writer.Rows.Count);
            Assert.Equal(2, writer.Rows.Single(x => x.Source == DownloadSource.Client).Count);
            Assert.All(writer.Rows, x => Assert.Equal(new DateTime(2026, 11, 3, 15, 0, 0, DateTimeKind.Utc), x.HourUtc));
        }
        using var fixture = new ControllerFixture();
        var failing = new RecordingWriter { Fail = true };
        using var failedWorker = new DownloadStatisticsWorker(fixture.Queue, new FixedResolver(), failing, NullLogger<DownloadStatisticsWorker>.Instance);
        await failedWorker.StartAsync(default);
        Assert.IsType<OkObjectResult>(await fixture.Controller.GetDistributionInfoWeb(ReleaseId, fixture.SubChannel).WaitAsync(TimeSpan.FromSeconds(2)));
        await failedWorker.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, failing.Calls);
        Assert.Equal(1, fixture.Queue.DroppedEvents);
    }

    [Fact]
    public void BeijingBoundariesAndZeroHoursAreCorrect()
    {
        var start = DownloadStatisticsTime.BeijingToUtc(new DateTime(2026, 11, 4, 0, 0, 0));
        Assert.Equal(new DateTime(2026, 11, 3, 16, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(new DateTime(2026, 11, 3, 23, 0, 0), DownloadStatisticsTime.ToBeijing(DownloadStatisticsTime.ToUtcHour(RequestedAt)));
        var hours = DownloadStatisticsQueryService.FillHours(new(start, start.AddHours(3)), [new(start.AddHours(1), 5)]);
        Assert.Equal(new long[] { 0, 5, 0 }, hours.Select(x => x.Count));
        Assert.Throws<ArgumentException>(() => DownloadStatisticsQueryService.FillHours(new(start, start), []));
    }

    [Theory]
    [InlineData("中国|上海市|上海市|电信|CN", "CN/SH", "中国大陆／上海市")]
    [InlineData("中国|0|0|0|CN", "CN/unknown", "中国大陆／未知省份")]
    [InlineData("United States|California|Los Angeles|0|US", "US", "United States")]
    [InlineData("中国|香港|0|0|CN", "HK", "香港")]
    [InlineData("中国|台湾省|台北市|0|CN", "TW", "台湾")]
    [InlineData(null, "unknown", "未知来源")]
    public void RegionGrouping(string? region, string code, string label)
        => Assert.Equal(new DownloadRegion(code, label), DownloadRegionResolver.Classify(region));

    [Theory]
    [InlineData("192.168.1.1", false)] [InlineData("::ffff:192.168.1.1", false)]
    [InlineData("10.0.0.1", false)] [InlineData("172.31.1.1", false)]
    [InlineData("::1", false)] [InlineData("fe80::1", false)] [InlineData("fc00::1", false)]
    [InlineData("8.8.8.8", true)] [InlineData("::ffff:8.8.8.8", true)]
    [InlineData("2001:4860:4860::8888", true)]
    public void AddressClassification(string ip, bool expected) => Assert.Equal(expected, DownloadRegionResolver.IsPublicAddress(IPAddress.Parse(ip)));

    [Fact]
    public void MissingDatabaseFallsBackAndPageRequiresOrganizationMember()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DownloadStatistics:Ip2RegionIpv4DatabasePath"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xdb"),
            ["DownloadStatistics:Ip2RegionIpv6DatabasePath"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xdb")
        }).Build();
        using var resolver = new DownloadRegionResolver(configuration, new TestEnvironment(), NullLogger<DownloadRegionResolver>.Instance);
        Assert.False(resolver.IsAvailable);
        Assert.Equal(DownloadRegion.Unknown, resolver.Resolve("8.8.8.8"));
        var auth = Assert.Single(typeof(DownloadStatisticsPage).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal(Policies.IsOrgMemberPolicyName, auth.Policy);
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    internal sealed class FixedResolver : IDownloadRegionResolver
    {
        public bool IsAvailable => true;
        public DownloadRegion Resolve(string? address) => new("CN/SH", "中国大陆／上海市");
    }

    private sealed class BlockingResolver : IDownloadRegionResolver, IDisposable
    {
        public bool IsAvailable => true;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release { get; } = new();
        public DownloadRegion Resolve(string? address) { Entered.TrySetResult(); Release.Wait(TimeSpan.FromSeconds(15)); return DownloadRegion.Unknown; }
        public void Dispose() => Release.Dispose();
    }

    internal sealed class RecordingWriter : IDownloadStatisticsWriter
    {
        public bool Block { get; init; }
        public bool Fail { get; init; }
        public int Calls { get; private set; }
        public List<HourlyDownloadStatistic> Rows { get; } = [];
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task WriteAsync(IReadOnlyCollection<HourlyDownloadStatistic> rows, CancellationToken token)
        {
            Calls++;
            Entered.TrySetResult();
            if (Block) await Release.Task.WaitAsync(token);
            if (Fail) throw new IOException("Simulated database failure");
            Rows.AddRange(rows);
        }
    }

    private sealed class ControllerFixture : IDisposable
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly MainDbContext _db;
        private readonly DistributionCacheService _cache = new(NullLoggerFactory.Instance);
        public DownloadEventQueue Queue { get; } = DownloadStatisticsTests.Queue();
        public DistributionsController Controller { get; }
        public string SubChannel => "windows_x64_full_folder";
        public Guid ChannelId { get; } = Guid.NewGuid();
        public ControllerFixture()
        {
            _connection.Open();
            _db = new MainDbContext(new DbContextOptionsBuilder<MainDbContext>().UseSqlite(_connection).Options);
            _db.Database.EnsureCreated();
            var info = new DistributionInfo { Id = ReleaseId, Version = "2.0.0.0" };
            var channel = new DistributionSubChannel { Os = "windows", Arch = "x64", BuildType = "full", Package = "folder", FileMapInfo = new() { ContentJson = "{}" } };
            _cache.SetMemoryCache(new SubChannelCacheKey(ReleaseId, SubChannel), (info, channel));
            _cache.SetMemoryCache(new DistributionCacheKey(ReleaseId, SubChannel, ResponseType.Web), new DistributionInfoWebResponse { Version = info.Version });
            _cache.SetMemoryCache(DistributionCacheService.MetadataCacheKey, new DistributionMetadata());
            _cache.SetMemoryCache(DistributionCacheService.WebLatestRequestCacheKey, new LatestDistributionInfoWebResponse());
            _cache.SetMemoryCache(new LatestDistributionCacheKey(ChannelId, new Version(0, 0, 0, 0)), info);
            var service = new DistributionsService(_db, NullLogger<DistributionsService>.Instance, _cache, new GpgSignatureService(_db, NullLogger<GpgSignatureService>.Instance));
            Controller = new(_db, NullLogger<DistributionsController>.Instance, service, Queue, new FixedClock());
            Controller.ControllerContext = new() { HttpContext = new DefaultHttpContext() };
            Controller.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("8.8.8.8");
        }
        public void Dispose() { _cache.MemoryCache.Dispose(); _db.Dispose(); _connection.Dispose(); }
    }

    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => RequestedAt; }

    private sealed class ThrowingLogger : ILogger<DownloadEventQueue>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => throw new InvalidOperationException("Log provider must not run on request thread");
    }
}
