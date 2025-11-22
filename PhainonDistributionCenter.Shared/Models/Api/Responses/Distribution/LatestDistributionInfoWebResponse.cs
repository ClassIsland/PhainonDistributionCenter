using System;
using System.Collections.Generic;

namespace PhainonDistributionCenter.Shared.Models.Api.Responses.Distribution;

/// <summary>
/// 代表获取最新分发信息的 Web 响应
/// </summary>
public class LatestDistributionInfoWebResponse
{
    /// <summary>
    /// 分发频道
    /// </summary>
    public Dictionary<Guid, ChannelInfoWeb> Channels { get; set; } = [];

    /// <summary>
    /// 默认分发频道
    /// </summary>
    public Guid DefaultChannel { get; set; } = Guid.Empty;
    
    /// <summary>
    /// 用于 Web 的频道信息
    /// </summary>
    public class ChannelInfoWeb
    {
        /// <summary>
        /// 最新版本 ID
        /// </summary>
        public Guid LatestVersionId { get; set; }

        /// <summary>
        /// 最新版本名称
        /// </summary>
        public string LatestVersion { get; set; } = "";

        /// <summary>
        /// 分发频道名称
        /// </summary>
        public string ChannelName { get; set; } = "";
        
        /// <summary>
        /// 分发频道描述
        /// </summary>
        public string ChannelDescription { get; set; } = "";
    }
}