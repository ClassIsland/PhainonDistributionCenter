using System.Collections.Generic;

namespace PhainonDistributionCenter.Shared.Models;

/// <summary>
/// 代表一个文件图
/// </summary>
public class FileMap
{
    /// <summary>
    /// 文件图中包含的组件
    /// </summary>
    public Dictionary<string, List<FileMapFile>> Components { get; set; } = new();
}