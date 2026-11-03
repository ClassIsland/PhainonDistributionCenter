using System.ComponentModel.DataAnnotations;
using PhainonDistributionCenter.Enums;

namespace PhainonDistributionCenter.Entities;

// Intentionally has no foreign keys: historical counts survive deletion/renaming of releases.
public class HourlyDownloadStatistic
{
    public DateTime HourUtc { get; set; }
    public Guid DistributionId { get; set; }
    [MaxLength(32)] public string Version { get; set; } = "";
    [MaxLength(67)] public string SubChannel { get; set; } = "";
    public DownloadSource Source { get; set; }
    [MaxLength(32)] public string RegionCode { get; set; } = "";
    [MaxLength(160)] public string RegionName { get; set; } = "";
    public long Count { get; set; }
}
