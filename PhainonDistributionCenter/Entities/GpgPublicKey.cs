using System.ComponentModel.DataAnnotations;
using PhainonDistributionCenter.Abstractions.Entities;

namespace PhainonDistributionCenter.Entities;

/// <summary>
/// 代表一个 Gpg 公钥
/// </summary>
public class GpgPublicKey : IObjectWithTime
{
    /// <summary>
    /// 公钥 Id
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// 公钥名称
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 公钥内容
    /// </summary>
    public string PublicKey { get; set; } = "";
    
    public DateTime CreatedTime { get; set; } = DateTime.Now;
    public DateTime UpdatedTime { get; set; } = DateTime.Now;
}