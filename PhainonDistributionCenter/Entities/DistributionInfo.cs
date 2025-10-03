using System.ComponentModel.DataAnnotations;
using PhainonDistributionCenter.Abstractions.Entities;

namespace PhainonDistributionCenter.Entities;

/// <summary>
/// 代表某个大版本中一次发布的信息。
/// </summary>
public class DistributionInfo : IObjectWithTime
{
    /// <summary>
    /// Id
    /// </summary>
    [Key] public Guid Id { get; set; }

    /// <summary>
    /// 具体版本，如 2.0.0.0
    /// </summary>
    [MaxLength(32)] public string Version { get; set; } = "";

    /// <summary>
    /// 版本号Major（X.x.x.x）
    /// </summary>
    public int VersionMajor { get; set; } = 0;
    
    /// <summary>
    /// 版本号Minor（x.X.x.x）
    /// </summary>
    public int VersionMinor { get; set; } = 0;
    
    /// <summary>
    /// 版本号Build（x.x.X.x）
    /// </summary>
    public int VersionBuild { get; set; } = 0;
    
    /// <summary>
    /// 版本号Revision（x.x.x.X）
    /// </summary>
    public int VersionRevision { get; set; } = 0;

    /// <summary>
    /// 友好版本号，如 2.0-Khaslana Release 1
    /// </summary>
    [MaxLength(64)] public string FriendlyVersion { get; set; } = "";
    
    /// <summary>
    /// 友好短版本号，如 2.0-Khaslana R1
    /// </summary>
    [MaxLength(64)] public string FriendlyVersionShort { get; set; } = "";

    /// <summary>
    /// 此发行版是否已启用
    /// </summary>
    public bool IsEnabled { get; set; } = false;

    /// <summary>
    /// 此发行版发布的发行渠道
    /// </summary>
    public IList<DistributionChannel> Channels { get; set; } = [];

    /// <summary>
    /// 此发行版的发行日志
    /// </summary>
    public string ChangeLog { get; set; } = "";
    
    /// <summary>
    /// 此发行版关联的版本信息
    /// </summary>
    public VersionInfo VersionInfo { get; set; }
    
    /// <summary>
    /// 此发行版关联的版本信息 Id
    /// </summary>
    public Guid VersionInfoId { get; set; }

    /// <summary>
    /// 当前发行版包含的子频道信息
    /// </summary>
    public IList<DistributionSubChannel> SubChannels { get; set; } = [];
    
    public DateTimeOffset CreatedTime { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedTime { get; set; } = DateTimeOffset.UtcNow;
}