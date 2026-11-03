# 下载量统计

## 部署

1. 在服务项目目录执行 `npm ci`，构建解决方案。Tailwind CLI 是构建依赖。
2. 使用现有数据库备份与迁移流程，在启动服务前运行 `dotnet PhainonDistributionCenter.dll --migrate=true`。新增迁移只创建小时统计表，不回填历史。
3. 从 [ip2region 官方数据目录](https://github.com/lionsoul2014/ip2region/tree/master/data)取得 **v3 格式**的 `ip2region_v4.xdb` 和 `ip2region_v6.xdb`，放在应用内容目录的 `data/` 下。也可以在配置文件中设置：

   ```json
   {
     "DownloadStatistics": {
       "Ip2RegionIpv4DatabasePath": "data/ip2region_v4.xdb",
       "Ip2RegionIpv6DatabasePath": "data/ip2region_v6.xdb"
     }
   }
   ```

   环境变量名分别为 `DownloadStatistics__Ip2RegionIpv4DatabasePath` 和 `DownloadStatistics__Ip2RegionIpv6DatabasePath`。相对路径相对于应用 ContentRoot；文件放在 `wwwroot` 外，不随源码提交。发布时应单独复制或挂载这两个文件，更新后重启应用加载。使用官方 v3 数据字段 `国家|省份|城市|ISP|ISO 国家代码`，不支持旧版 v2 数据文件或自定义字段顺序。
4. 确认反向代理正确传递真实来源 IP。统计读取经过现有 Forwarded Headers 中间件处理的 `RemoteIpAddress`，不自行读取客户端请求头。现有生产配置接受转发头，入口代理必须覆盖外部传入的转发头，并限制后端直接访问，避免伪造来源。
5. 使用组织成员账号访问 `/DownloadStatistics`。首页“总下载次数”与此页使用相同统计数据。

使用官方 C# 绑定 `IP2Region.Net`，两份 XDB 在启动时校验格式和地址族，并以线程安全的全文内存缓存方式复用。IPv4 映射地址使用 IPv4 库；原生 IPv6 使用 IPv6 库。一份数据库缺失或损坏只影响对应地址族，无法定位的统计归入“未知来源”，页面会提示地域解析未完整可用。

国内省级名称统一为中文，境外国家名称采用 ip2region 数据中的名称（官方数据通常为英文）。IP 地域信息仅代表数据库推断。历史地域快照和已计入“未知来源”的统计保留；切换数据源不回填历史，也无需新增数据库迁移。

## 口径与延迟

- `/api/v1/public/distributions/web/{versionId}/{subChannelId}` 成功返回详情计为“网页”。
- `/api/v1/public/distributions/{id}/{subChannelId}` 成功返回详情计为“客户端”。
- 缓存命中、重复请求均计数，失败请求、最新版本和元数据查询不计数。此数据衡量详情获取次数，并非文件传输完成数，也不按 IP 去重。
- 按请求发生时刻归入 UTC 小时桶，排队跨小时不会改变归属。页面使用北京时间 UTC+8，开始时间包含、结束时间不包含，默认最近 168 个小时并包含当前小时。
- 中国大陆按省级行政区统计；港澳台及其他来源按国家/地区统计。中国大陆省份缺失时单独归入“中国大陆／未知省份”。
- 请求路径仅非阻塞入队，不等待地理解析或写库。后台每批最多 1,000 个事件，每秒刷写，积压时连续处理。正常情况下刷新后可看到约一秒前的数据，积压及写库耗时会增加延迟。
- 聚合保留版本名称、子频道及地域快照，删除或重命名业务数据不抹除历史统计。聚合数据长期保留；原始 IP 仅暂存于内存队列，不写入统计表或统计日志。

## 故障与停机

队列容量为 10,000。队列满、应用退出、进程崩溃或数据库写入失败可能丢失尚未持久化的统计，不阻塞详情响应。失败批次不自动重试，避免提交结果不明确时重复累加。每批使用一条原子 SQL，多实例对相同维度使用数据库原子累加。

正常停机先关闭入队，再最多等待 10 秒排空。为容器/进程留出至少 10 秒的退出宽限期。写库命令超时为 5 秒；强制终止不保证排空。

诊断指标位于 `PhainonDistributionCenter.DownloadStatistics` Meter 的 `download_statistics.dropped` Counter，按 `reason` 区分 `queue_unavailable`、`write_failed`、`shutdown_timeout`。丢弃告警每分钟最多一次；批次失败日志包含事件数量和错误类型，不包含请求 IP。

## 验证

先构建，再运行测试：

```powershell
dotnet build PhainonDistributionCenter.sln
dotnet test PhainonDistributionCenter.Tests/PhainonDistributionCenter.Tests.csproj --no-build
```

设置 `PDC_TEST_POSTGRES` 为临时 PostgreSQL 实例的管理连接字符串后，可启用集成测试。测试账号需要创建数据库权限；测试只迁移和删除它创建的 `pdc_download_test_<随机值>` 数据库，不修改连接字符串指定的原数据库。未设置时明确跳过集成测试。

集成测试覆盖旧库升级、回滚和重新升级、并发原子累加、来源与地域分离、跨小时过滤、全部汇总一致、分页、批次原子性及删除发行版后的历史保留。

地域解析测试使用自行生成的最小 v3 XDB 测试 IPv4/IPv6 分流、映射地址、缺失/损坏数据库和地域归一化，不依赖外部下载。设置 `PDC_TEST_IP2REGION_DIRECTORY` 为包含两份官方 v3 XDB 文件的绝对目录后，可额外运行真实数据库查询测试；未设置时跳过该项。
