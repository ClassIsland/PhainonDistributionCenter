using System.ComponentModel.DataAnnotations;
using PhainonDistributionCenter.Abstractions.Entities;

namespace PhainonDistributionCenter.Entities;

/// <summary>
/// 代表特定于某平台的发行子频道信息
/// </summary>
public class DistributionSubChannel : IObjectWithTime
{
    /// <summary>
    /// Id
    /// </summary>
    [Key] public Guid Id { get; set; }

    /// <summary>
    /// 子频道的目标 OS，如 windows
    /// </summary>
    [MaxLength(16)]
    public string Os { get; set; } = "unknown";
    
    /// <summary>
    /// 子频道的目标 CPU 架构，如 x64
    /// </summary>
    [MaxLength(16)]
    public string Arch { get; set; } = "unknown";
    
    /// <summary>
    /// 子频道的目标打包方式，如 folder（文件夹打包）
    /// </summary>
    [MaxLength(16)]
    public string Package { get; set; } = "unknown";
    
    /// <summary>
    /// 子频道的目标构建方式，如 full（完整构建）
    /// </summary>
    [MaxLength(16)]
    public string BuildType { get; set; } = "unknown";
    
    /// <summary>
    /// 当前子频道关联的文件图信息
    /// </summary>
    public FileMapInfo FileMapInfo { get; set; }
    
    /// <summary>
    /// 当前子频道关联的文件图信息 Id
    /// </summary>
    public Guid FileMapInfoId { get; set; }
    
    public DateTime CreatedTime { get; set; } = DateTime.Now;
    public DateTime UpdatedTime { get; set; } = DateTime.Now;
}