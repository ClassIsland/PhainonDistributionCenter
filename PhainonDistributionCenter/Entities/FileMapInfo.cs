using System.ComponentModel.DataAnnotations;
using PhainonDistributionCenter.Abstractions.Entities;

namespace PhainonDistributionCenter.Entities;

/// <summary>
/// 代表文件图信息
/// </summary>
public class FileMapInfo : IObjectWithTime
{
    /// <summary>
    /// Id
    /// </summary>
    [Key] public Guid Id { get; set; }

    /// <summary>
    /// 文件图 JSON 内容
    /// </summary>
    public string ContentJson { get; set; } = "{}";

    /// <summary>
    /// 文件图的 PGP 签名
    /// </summary>
    public string PgpSignature { get; set; } = "";
    
    /// <summary>
    /// 文件图签名的公钥 Id
    /// </summary>
    public Guid PublicKeyId { get; set; }
    
    /// <summary>
    /// 文件图签名的公钥
    /// </summary>
    public GpgPublicKey PublicKey { get; set; }
    
    public DateTimeOffset CreatedTime { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedTime { get; set; } = DateTimeOffset.UtcNow;
}