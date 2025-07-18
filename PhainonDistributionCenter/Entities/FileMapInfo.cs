using System.ComponentModel.DataAnnotations;

namespace PhainonDistributionCenter.Entities;

/// <summary>
/// 代表文件图信息
/// </summary>
public class FileMapInfo
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
}