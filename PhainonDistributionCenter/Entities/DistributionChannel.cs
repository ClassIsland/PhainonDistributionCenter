using System.ComponentModel.DataAnnotations;
using PhainonDistributionCenter.Abstractions.Entities;

namespace PhainonDistributionCenter.Entities;

/// <summary>
/// 代表一个发行渠道
/// </summary>
public class DistributionChannel : IObjectWithTime
{
    /// <summary>
    /// Id
    /// </summary>
    [Key] public Guid Id { get; set; }

    /// <summary>
    /// 发行渠道显示名称，如“稳定通道”
    /// </summary>
    [MaxLength(64)] public string Name { get; set; } = "";

    /// <summary>
    /// 发行渠道描述
    /// </summary>
    public string Description { get; set; } = "";
    
    /// <summary>
    /// 此发行渠道关联的发行版本信息
    /// </summary>
    public ICollection<DistributionInfo> AssociatedDistributions { get; set; } = [];
    
    public DateTime CreatedTime { get; set; } = DateTime.Now;
    public DateTime UpdatedTime { get; set; } = DateTime.Now;
}