using System.Net;
using System.Net.Sockets;
using System.Buffers.Binary;
using IP2Region.Net.Abstractions;
using IP2Region.Net.XDB;
using PhainonDistributionCenter.Models.Statistics;

namespace PhainonDistributionCenter.Services.Statistics;

public interface IDownloadRegionResolver
{
    bool IsAvailable { get; }
    DownloadRegion Resolve(string? address);
}

public sealed class DownloadRegionResolver : IDownloadRegionResolver, IDisposable
{
    private readonly ISearcher? _ipv4Searcher;
    private readonly ISearcher? _ipv6Searcher;
    private readonly ILogger<DownloadRegionResolver> _logger;
    private long _lastWarning;
    public bool IsAvailable => _ipv4Searcher != null && _ipv6Searcher != null;

    public DownloadRegionResolver(IConfiguration configuration, IHostEnvironment environment,
        ILogger<DownloadRegionResolver> logger)
    {
        _logger = logger;
        _ipv4Searcher = LoadSearcher("Ip2RegionIpv4DatabasePath", "data/ip2region_v4.xdb", 4);
        _ipv6Searcher = LoadSearcher("Ip2RegionIpv6DatabasePath", "data/ip2region_v6.xdb", 6);

        ISearcher? LoadSearcher(string key, string defaultPath, int ipVersion)
        {
            try
            {
                var path = Path.GetFullPath(configuration[$"DownloadStatistics:{key}"] ?? defaultPath, environment.ContentRootPath);
                ValidateDatabase(path, ipVersion);
                // Content caching is thread-safe and avoids file I/O on every background lookup.
                return new Searcher(CachePolicy.Content, path);
            }
            catch (Exception ex)
            {
                logger.LogWarning("无法加载 ip2region IPv{Version} 数据库（{ErrorType}），对应来源将归入未知；请配置 DownloadStatistics:{Key}",
                    ipVersion, ex.GetType().Name, key);
                return null;
            }
        }
    }

    private static void ValidateDatabase(string path, int ipVersion)
    {
        // The C# searcher does not validate the header or address family itself.
        // Validate the official v3 vector-index format before allowing any lookups.
        using var file = File.OpenRead(path);
        const int dataStart = 256 + 256 * 256 * 8;
        var indexSize = (ipVersion == 4 ? 4 : 16) * 2 + 6;
        if (file.Length < dataStart + indexSize) throw new InvalidDataException("Incomplete ip2region database.");
        Span<byte> header = stackalloc byte[256];
        file.ReadExactly(header);
        var start = BinaryPrimitives.ReadUInt32LittleEndian(header[8..]);
        var end = BinaryPrimitives.ReadUInt32LittleEndian(header[12..]);
        if (BinaryPrimitives.ReadUInt16LittleEndian(header) != 3 ||
            BinaryPrimitives.ReadUInt16LittleEndian(header[2..]) != 1 ||
            BinaryPrimitives.ReadUInt16LittleEndian(header[16..]) != ipVersion ||
            BinaryPrimitives.ReadUInt16LittleEndian(header[18..]) != 4 ||
            start < dataStart || end < start || end + (long)indexSize > file.Length || (end - start) % indexSize != 0)
            throw new InvalidDataException("Expected an ip2region v3 database of the configured address family.");
    }

    public DownloadRegion Resolve(string? address)
    {
        if (!IPAddress.TryParse(address, out var ip)) return DownloadRegion.Unknown;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (!IsPublicAddress(ip)) return DownloadRegion.Unknown;
        var searcher = ip.AddressFamily == AddressFamily.InterNetwork ? _ipv4Searcher : _ipv6Searcher;
        if (searcher == null) return DownloadRegion.Unknown;
        try
        {
            return Classify(searcher.Search(ip));
        }
        catch (Exception ex)
        {
            var now = Environment.TickCount64;
            var last = Interlocked.Read(ref _lastWarning);
            if ((last == 0 || now - last >= 60_000) && Interlocked.CompareExchange(ref _lastWarning, now, last) == last)
                _logger.LogWarning("ip2region 地域查询失败（{ErrorType}），已归入未知来源", ex.GetType().Name);
            return DownloadRegion.Unknown;
        }
    }

    public static DownloadRegion Classify(string? region)
    {
        // ip2region v3: country|province|city|ISP|ISO alpha-2. City/ISP are not stored.
        var fields = region?.Split('|');
        if (fields?.Length != 5) return DownloadRegion.Unknown;
        var country = Field(fields[0]);
        var province = NormalizeProvince(Field(fields[1]));
        var countryCode = Field(fields[4])?.ToUpperInvariant();
        if (countryCode == "HK" || country is "香港" or "中国香港") return new("HK", "香港");
        if (countryCode == "MO" || country is "澳门" or "中国澳门") return new("MO", "澳门");
        if (countryCode == "TW" || country is "台湾" or "中国台湾") return new("TW", "台湾");
        if (countryCode == "CN" || country == "中国")
        {
            // Some data sets report these regions in the province field under CN.
            if (province is "香港") return new("HK", "香港");
            if (province is "澳门") return new("MO", "澳门");
            if (province is "台湾") return new("TW", "台湾");
            if (province != null && Provinces.TryGetValue(province, out var match))
                return new($"CN/{match.Code}", $"中国大陆／{match.Name}");
            return new("CN/unknown", "中国大陆／未知省份");
        }
        if (countryCode is not { Length: 2 } || countryCode.Any(c => c is < 'A' or > 'Z') || countryCode is "ZZ" or "XX")
            return DownloadRegion.Unknown;
        return new(countryCode, country ?? countryCode);
    }

    private static string? Field(string value)
    {
        var text = value.Trim();
        return text is "" or "0" or "-" or "--" or "未知" ? null : text;
    }

    private static string? NormalizeProvince(string? value)
    {
        if (value == null) return null;
        foreach (var suffix in new[] { "特别行政区", "维吾尔自治区", "壮族自治区", "回族自治区", "自治区", "省", "市" })
            if (value.EndsWith(suffix, StringComparison.Ordinal)) return value[..^suffix.Length];
        return value;
    }

    // Keep the same ISO subdivision keys as existing statistics rather than grouping by spelling.
    private static readonly IReadOnlyDictionary<string, (string Code, string Name)> Provinces =
        new Dictionary<string, (string, string)>
        {
            ["北京"] = ("BJ", "北京市"), ["天津"] = ("TJ", "天津市"), ["河北"] = ("HE", "河北省"),
            ["山西"] = ("SX", "山西省"), ["内蒙古"] = ("NM", "内蒙古自治区"), ["辽宁"] = ("LN", "辽宁省"),
            ["吉林"] = ("JL", "吉林省"), ["黑龙江"] = ("HL", "黑龙江省"), ["上海"] = ("SH", "上海市"),
            ["江苏"] = ("JS", "江苏省"), ["浙江"] = ("ZJ", "浙江省"), ["安徽"] = ("AH", "安徽省"),
            ["福建"] = ("FJ", "福建省"), ["江西"] = ("JX", "江西省"), ["山东"] = ("SD", "山东省"),
            ["河南"] = ("HA", "河南省"), ["湖北"] = ("HB", "湖北省"), ["湖南"] = ("HN", "湖南省"),
            ["广东"] = ("GD", "广东省"), ["广西"] = ("GX", "广西壮族自治区"), ["海南"] = ("HI", "海南省"),
            ["重庆"] = ("CQ", "重庆市"), ["四川"] = ("SC", "四川省"), ["贵州"] = ("GZ", "贵州省"),
            ["云南"] = ("YN", "云南省"), ["西藏"] = ("XZ", "西藏自治区"), ["陕西"] = ("SN", "陕西省"),
            ["甘肃"] = ("GS", "甘肃省"), ["青海"] = ("QH", "青海省"), ["宁夏"] = ("NX", "宁夏回族自治区"),
            ["新疆"] = ("XJ", "新疆维吾尔自治区")
        };

    public static bool IsPublicAddress(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any)) return false;
        var bytes = ip.GetAddressBytes();
        if (bytes.Length == 4)
            return !(bytes[0] is 0 or 10 or 127 || bytes[0] >= 224 ||
                     (bytes[0] == 169 && bytes[1] == 254) ||
                     (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                     (bytes[0] == 192 && bytes[1] == 168) ||
                     (bytes[0] == 100 && bytes[1] is >= 64 and <= 127));
        return !ip.IsIPv6LinkLocal && !ip.IsIPv6SiteLocal && !ip.IsIPv6Multicast && (bytes[0] & 0xfe) != 0xfc;
    }

    public void Dispose()
    {
        _ipv4Searcher?.Dispose();
        _ipv6Searcher?.Dispose();
    }
}
