using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PhainonDistributionCenter.Abstractions.Entities;

namespace PhainonDistributionCenter.Entities;

/// <summary>
/// 代表一个大版本的信息
/// </summary>
public class VersionInfo : IObjectWithTime
{
    /// <summary>
    /// Id
    /// </summary>
    [Key] public Guid Id { get; set; }

    /// <summary>
    /// 版本，如 2.0
    /// </summary>
    [MaxLength(32)] public string Version { get; set; } = "";

    /// <summary>
    /// 版本代号，如 Khaslana
    /// </summary>
    [MaxLength(64)] public string Codename { get; set; } = "";
    
    public DateTime CreatedTime { get; set; } = DateTime.Now;
    public DateTime UpdatedTime { get; set; } = DateTime.Now;
}