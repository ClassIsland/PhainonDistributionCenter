using PhainonDistributionCenter.Shared.Models.FileMap;

namespace PhainonDistributionCenter.Client.Models;

public class Configuration
{
    public const string DefaultConfigurationFileName = "phainon.yml";
    
    /// <summary>
    /// 文件图中包含的组件
    /// </summary>
    public Dictionary<string, FileMapComponentConfig> Components { get; set; } = new();

    /// <summary>
    /// 预定义的文件图变量列表
    /// </summary>
    public Dictionary<string, string> Variables { get; set; } = [];

    /// <summary>
    /// 项目名称
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 文件仓库根，结尾要有“/”
    /// </summary>
    public string FileRepoRoot { get; set; } = "";
    
    /// <summary>
    /// 下载归档根，结尾要有“/”
    /// </summary>
    public string ArchiveRoot { get; set; } = "";
    
    /// <summary>
    /// S3 桶的文件仓库键根，结尾要有“/”
    /// </summary>
    public string BucketKeyRoot { get; set; } = "";
    
    /// <summary>
    /// S3 桶的下载归档键根，结尾要有“/”
    /// </summary>
    public string ArchiveBucketKeyRoot { get; set; } = "";

}