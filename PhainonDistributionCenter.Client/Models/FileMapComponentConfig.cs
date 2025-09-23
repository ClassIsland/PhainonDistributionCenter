namespace PhainonDistributionCenter.Client.Models;

public class FileMapComponentConfig
{
    /// <summary>
    /// 此组件允许差分更新
    /// </summary>
    public bool AllowDiffUpdate { get; set; } = false;

    /// <summary>
    /// 组件部署根路径
    /// </summary>
    public string Root { get; set; } = "";

    /// <summary>
    /// 组件包含的文件
    /// </summary>
    public List<string> Includes { get; set; } = [];
    
    /// <summary>
    /// 在包含的文件的基础上排除的文件
    /// </summary>
    public List<string> Excludes { get; set; } = [];
}