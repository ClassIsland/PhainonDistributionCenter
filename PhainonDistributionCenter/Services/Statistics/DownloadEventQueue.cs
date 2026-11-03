using System.Diagnostics.Metrics;
using System.Threading.Channels;
using PhainonDistributionCenter.Models.Statistics;

namespace PhainonDistributionCenter.Services.Statistics;

public sealed class DownloadEventQueue(ILogger<DownloadEventQueue> logger)
{
    public const int Capacity = 10_000;
    private static readonly Meter Meter = new("PhainonDistributionCenter.DownloadStatistics");
    private static readonly Counter<long> Dropped = Meter.CreateCounter<long>("download_statistics.dropped");
    private readonly Channel<DownloadEvent> _channel = Channel.CreateBounded<DownloadEvent>(new BoundedChannelOptions(Capacity)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.Wait,
        AllowSynchronousContinuations = false
    });
    private long _droppedEvents;
    private long _lastWarning;
    private long _lastLoggedCount;
    private string _lastDropReason = "";

    public ChannelReader<DownloadEvent> Reader => _channel.Reader;
    public long DroppedEvents => Interlocked.Read(ref _droppedEvents);

    public bool TryRecord(DownloadEvent download)
    {
        if (_channel.Writer.TryWrite(download)) return true;
        ReportDropped(1, "queue_unavailable");
        return false;
    }

    public void Complete() => _channel.Writer.TryComplete();

    public void ReportDropped(long count, string reason)
    {
        Interlocked.Add(ref _droppedEvents, count);
        Volatile.Write(ref _lastDropReason, reason);
        Dropped.Add(count, new KeyValuePair<string, object?>("reason", reason));
    }

    // Even overflow logging runs in the worker: a slow log provider must not delay a response.
    public void LogDropsIfNeeded()
    {
        var total = DroppedEvents;
        if (total == Interlocked.Read(ref _lastLoggedCount)) return;
        var now = Environment.TickCount64;
        var last = Interlocked.Read(ref _lastWarning);
        if ((last == 0 || now - last >= 60_000) && Interlocked.CompareExchange(ref _lastWarning, now, last) == last)
        {
            Interlocked.Exchange(ref _lastLoggedCount, total);
            logger.LogWarning("下载统计事件丢失：{Reason}，累计 {Count} 条", Volatile.Read(ref _lastDropReason), total);
        }
    }
}
