using System.ComponentModel.DataAnnotations;
using PhainonDistributionCenter.Abstractions.Entities;

namespace PhainonDistributionCenter.Entities;

/// <summary>
/// 代表一个发行通道
/// </summary>
public class DistributionChannel : IObjectWithTime
{
    /// <summary>
    /// Id
    /// </summary>
    [Key] public Guid Id { get; set; }

    /// <summary>
    /// 发行通道显示名称，如“稳定通道”
    /// </summary>
    [MaxLength(64)] public string Name { get; set; } = "";

    /// <summary>
    /// 发行通道描述
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// 是否是默认发行通道
    /// </summary>
    public bool IsDefault { get; set; } = false;

    /// <summary>
    /// 发行通道已启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// 此发行通道关联的发行版本信息
    /// </summary>
    public ICollection<DistributionInfo> AssociatedDistributions { get; set; } = [];
    
    public DateTimeOffset CreatedTime { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedTime { get; set; } = DateTimeOffset.UtcNow;
}