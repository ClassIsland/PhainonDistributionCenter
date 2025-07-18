using System.ComponentModel.DataAnnotations;

namespace PhainonDistributionCenter.Entities;

/// <summary>
/// 代表某个大版本中一次发布的信息。
/// </summary>
public class DistributionInfo
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
    /// 此发行版发布的发行渠道
    /// </summary>
    public ICollection<DistributionChannel> Channels { get; set; } = [];
    
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
}