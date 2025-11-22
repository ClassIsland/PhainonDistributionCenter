namespace PhainonDistributionCenter.Shared.Models.Api.Responses.Distribution;

/// <summary>
/// 发行信息 Web 响应。
/// </summary>
public class DistributionInfoWebResponse
{
    /// <summary>
    /// 归档下载链接
    /// </summary>
    public string ArchiveUrl { get; set; } = "";

    /// <summary>
    /// 归档 SHA512
    /// </summary>
    public string ArchiveSHA512 { get; set; } = "";

    /// <summary>
    /// 版本名称
    /// </summary>
    public string Version { get; set; } = "";
}