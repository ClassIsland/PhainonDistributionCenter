namespace PhainonDistributionCenter.Models.Statistics;

public static class DownloadStatisticsTime
{
    public static readonly TimeSpan BeijingOffset = TimeSpan.FromHours(8);

    public static DateTime ToUtcHour(DateTimeOffset time)
    {
        var utc = time.UtcDateTime;
        return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc);
    }

    public static DateTime BeijingToUtc(DateTime time) =>
        new DateTimeOffset(DateTime.SpecifyKind(time, DateTimeKind.Unspecified), BeijingOffset).UtcDateTime;

    public static DateTime ToBeijing(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToOffset(BeijingOffset).DateTime;
}
