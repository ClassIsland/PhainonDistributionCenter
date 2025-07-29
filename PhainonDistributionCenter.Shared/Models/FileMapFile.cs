namespace PhainonDistributionCenter.Shared.Models;

/// <summary>
/// 代表文件图中的一个文件
/// </summary>
public class FileMapFile
{
    /// <summary>
    /// 代表从根目录开始的文件路径
    /// </summary>
    public string Path { get; set; } = "";

    /// <summary>
    /// 文件下载路径
    /// </summary>
    public string DownloadUrl { get; set; } = "";

    /// <summary>
    /// 文件的 SHA512 校验和
    /// </summary>
    public byte[] FileSha512 { get; set; } = [];
}