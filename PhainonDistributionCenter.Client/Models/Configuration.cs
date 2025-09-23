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

}