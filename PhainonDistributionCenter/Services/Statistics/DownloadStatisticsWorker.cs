using PhainonDistributionCenter.Entities;
using PhainonDistributionCenter.Models.Statistics;

namespace PhainonDistributionCenter.Services.Statistics;

public sealed class DownloadStatisticsWorker(DownloadEventQueue queue, IDownloadRegionResolver resolver,
    IDownloadStatisticsWriter writer, ILogger<DownloadStatisticsWorker> logger) : BackgroundService
{
    public const int BatchSize = 1_000;
    private readonly CancellationTokenSource _abort = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var abortToken = _abort.Token;
        await Task.Yield();
        // StopAsync completes the channel and grants the worker its own bounded drain period.
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (true)
            {
                var batch = new List<DownloadEvent>(BatchSize);
                while (batch.Count < BatchSize && queue.Reader.TryRead(out var item)) batch.Add(item);
                if (batch.Count > 0) await WriteBatchAsync(batch, abortToken);
                queue.LogDropsIfNeeded();
                if (queue.Reader.Completion.IsCompleted) break;
                if (batch.Count == BatchSize) continue;
                // Completing the queue wakes an idle worker immediately during shutdown.
                using var wake = CancellationTokenSource.CreateLinkedTokenSource(abortToken);
                var tick = timer.WaitForNextTickAsync(wake.Token).AsTask();
                await Task.WhenAny(tick, queue.Reader.Completion);
                wake.Cancel();
                try { await tick; } catch (OperationCanceledException) when (!abortToken.IsCancellationRequested) { }
            }
        }
        catch (OperationCanceledException) when (abortToken.IsCancellationRequested) { }
        finally
        {
            long remaining = 0;
            while (queue.Reader.TryRead(out _)) remaining++;
            if (remaining > 0) queue.ReportDropped(remaining, "shutdown_timeout");
            queue.LogDropsIfNeeded();
        }
    }

    private async Task WriteBatchAsync(List<DownloadEvent> batch, CancellationToken cancellationToken)
    {
        try
        {
            var rows = batch.Select(item => (Event: item, Region: resolver.Resolve(item.IpAddress)))
                .GroupBy(x => new { Hour = DownloadStatisticsTime.ToUtcHour(x.Event.RequestedAt),
                    x.Event.DistributionId, x.Event.Version, x.Event.SubChannel, x.Event.Source, x.Region.Code })
                .Select(group => new HourlyDownloadStatistic
                {
                    HourUtc = group.Key.Hour, DistributionId = group.Key.DistributionId,
                    Version = group.Key.Version, SubChannel = group.Key.SubChannel, Source = group.Key.Source,
                    RegionCode = group.Key.Code, RegionName = group.First().Region.Name, Count = group.LongCount()
                }).OrderBy(x => x.HourUtc).ThenBy(x => x.DistributionId).ThenBy(x => x.Version, StringComparer.Ordinal)
                .ThenBy(x => x.SubChannel, StringComparer.Ordinal).ThenBy(x => x.Source)
                .ThenBy(x => x.RegionCode, StringComparer.Ordinal).ToArray();
            await writer.WriteAsync(rows, cancellationToken);
        }
        catch (Exception ex)
        {
            queue.ReportDropped(batch.Count, "write_failed");
            // The exception may contain SQL parameter data; only log its type.
            logger.LogWarning("下载统计批次写入失败（{ErrorType}），丢弃 {Count} 条事件，不进行自动重试", ex.GetType().Name, batch.Count);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        queue.Complete();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        using var registration = deadline.Token.Register(() => _abort.Cancel());
        try { await base.StopAsync(deadline.Token); }
        finally
        {
            // StopAsync's cancellation waiter can finish before our registered callback runs.
            // Cancel explicitly before disposing that registration so in-flight writes cannot outlive shutdown.
            _abort.Cancel();
        }
    }

    public override void Dispose()
    {
        _abort.Cancel();
        base.Dispose();
        _abort.Dispose();
    }
}
