using System.Buffers.Binary;
using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using PhainonDistributionCenter.Models.Statistics;
using PhainonDistributionCenter.Services.Statistics;
using Xunit;

namespace PhainonDistributionCenter.Tests;

public sealed class Ip2RegionResolverTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("pdc-ip2region-").FullName;
    private string V4Path => Path.Combine(_directory, "v4.xdb");
    private string V6Path => Path.Combine(_directory, "v6.xdb");

    [Theory]
    [InlineData("中国|广东省|广州市|电信|CN", "CN/GD", "中国大陆／广东省")]
    [InlineData("中国|上海|上海市|0|CN", "CN/SH", "中国大陆／上海市")]
    [InlineData("中国|北京市|北京市|0|CN", "CN/BJ", "中国大陆／北京市")]
    [InlineData("中国|重庆|重庆市|0|CN", "CN/CQ", "中国大陆／重庆市")]
    [InlineData("中国|内蒙古自治区|呼和浩特|0|CN", "CN/NM", "中国大陆／内蒙古自治区")]
    [InlineData("中国|新疆维吾尔自治区|乌鲁木齐|0|CN", "CN/XJ", "中国大陆／新疆维吾尔自治区")]
    [InlineData("中国|广西|南宁市|0|CN", "CN/GX", "中国大陆／广西壮族自治区")]
    [InlineData("中国|宁夏回族自治区|银川市|0|CN", "CN/NX", "中国大陆／宁夏回族自治区")]
    [InlineData("中国|未识别省份|0|0|CN", "CN/unknown", "中国大陆／未知省份")]
    [InlineData("中国|澳门特别行政区|0|0|CN", "MO", "澳门")]
    [InlineData("中国香港|0|0|0|HK", "HK", "香港")]
    [InlineData("中国澳门|0|0|0|MO", "MO", "澳门")]
    [InlineData("台湾|0|0|0|TW", "TW", "台湾")]
    [InlineData("Japan|Tokyo|Tokyo|0|jp", "JP", "Japan")]
    [InlineData("0|0|0|0|US", "US", "US")]
    [InlineData("0|0|0|0|0", "unknown", "未知来源")]
    [InlineData("0|0|0|0|ZZ", "unknown", "未知来源")]
    [InlineData("invalid", "unknown", "未知来源")]
    [InlineData("US|California|Los Angeles|0|USA", "unknown", "未知来源")]
    [InlineData("", "unknown", "未知来源")]
    public void MapsV3RegionFieldsWithoutRecordingCityOrIsp(string region, string code, string name)
        => Assert.Equal(new DownloadRegion(code, name), DownloadRegionResolver.Classify(region));

    [Fact]
    public void SearchesBothAddressFamiliesAndMappedIpv4UsingContentCache()
    {
        CreateDatabase(V4Path, 4, "8.8.8.8", "中国|上海|上海市|0|CN");
        CreateDatabase(V6Path, 6, "2001:4860:4860::8888", "United States|California|0|0|US");
        using var resolver = CreateResolver();
        Assert.True(resolver.IsAvailable);
        // Lookup continues after removing the input files: both readers use in-memory contents.
        File.Delete(V4Path); File.Delete(V6Path);
        Assert.Equal(new DownloadRegion("CN/SH", "中国大陆／上海市"), resolver.Resolve("8.8.8.8"));
        Assert.Equal(resolver.Resolve("8.8.8.8"), resolver.Resolve("::ffff:8.8.8.8"));
        Assert.Equal(new DownloadRegion("US", "United States"), resolver.Resolve("2001:4860:4860::8888"));
        Assert.Equal(DownloadRegion.Unknown, resolver.Resolve("1.1.1.1"));
        Assert.Equal(DownloadRegion.Unknown, resolver.Resolve("::1"));
        Assert.Equal(DownloadRegion.Unknown, resolver.Resolve("192.168.1.1"));
        Assert.Equal(DownloadRegion.Unknown, resolver.Resolve("not-an-ip"));
    }

    [Theory]
    [InlineData(4)] [InlineData(6)]
    public void MissingOrDamagedFamilyDoesNotDisableOtherFamily(int availableVersion)
    {
        if (availableVersion == 4)
        {
            CreateDatabase(V4Path, 4, "8.8.8.8", "中国|广东省|广州市|0|CN");
            File.WriteAllText(V6Path, "not an xdb");
        }
        else CreateDatabase(V6Path, 6, "2001:4860:4860::8888", "Japan|Tokyo|0|0|JP");
        using var resolver = CreateResolver();
        Assert.False(resolver.IsAvailable);
        Assert.Equal(availableVersion == 4 ? new DownloadRegion("CN/GD", "中国大陆／广东省") : DownloadRegion.Unknown,
            resolver.Resolve("8.8.8.8"));
        Assert.Equal(availableVersion == 6 ? new DownloadRegion("JP", "Japan") : DownloadRegion.Unknown,
            resolver.Resolve("2001:4860:4860::8888"));
    }

    [Fact]
    public void RejectsDatabasesConfiguredForWrongAddressFamily()
    {
        CreateDatabase(V4Path, 6, "2001:4860:4860::8888", "Japan|Tokyo|0|0|JP");
        CreateDatabase(V6Path, 4, "8.8.8.8", "United States|0|0|0|US");
        using var resolver = CreateResolver();
        Assert.False(resolver.IsAvailable);
        Assert.Equal(DownloadRegion.Unknown, resolver.Resolve("8.8.8.8"));
        Assert.Equal(DownloadRegion.Unknown, resolver.Resolve("2001:4860:4860::8888"));
    }

    [Fact]
    public void MalformedIndexFallsBackInsteadOfFailingDownloadBatch()
    {
        CreateDatabase(V4Path, 4, "8.8.8.8", "United States|0|0|0|US");
        var data = File.ReadAllBytes(V4Path);
        var vector = 256 + (8 * 256 + 8) * 8;
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(vector), uint.MaxValue - 20);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(vector + 4), uint.MaxValue - 20);
        File.WriteAllBytes(V4Path, data);
        using var resolver = CreateResolver();
        Assert.Equal(DownloadRegion.Unknown, resolver.Resolve("8.8.8.8"));
    }

    [Fact]
    public void RejectsV2FilesInsteadOfInterpretingOldRegionFieldOrder()
    {
        CreateDatabase(V4Path, 4, "8.8.8.8", "中国|0|广东省|广州市|电信");
        var data = File.ReadAllBytes(V4Path);
        BinaryPrimitives.WriteUInt16LittleEndian(data, 2);
        File.WriteAllBytes(V4Path, data);
        using var resolver = CreateResolver();
        Assert.Equal(DownloadRegion.Unknown, resolver.Resolve("8.8.8.8"));
    }

    [Ip2RegionDataFact]
    public void OfficialDatabasesResolveRealIpv4AndIpv6Addresses()
    {
        var directory = Environment.GetEnvironmentVariable("PDC_TEST_IP2REGION_DIRECTORY")!;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DownloadStatistics:Ip2RegionIpv4DatabasePath"] = "ip2region_v4.xdb",
            ["DownloadStatistics:Ip2RegionIpv6DatabasePath"] = "ip2region_v6.xdb"
        }).Build();
        using var resolver = new DownloadRegionResolver(configuration, new TestEnvironment(directory), NullLogger<DownloadRegionResolver>.Instance);
        Assert.True(resolver.IsAvailable);
        Assert.Equal("US", resolver.Resolve("8.8.8.8").Code);
        Assert.Equal(resolver.Resolve("8.8.8.8"), resolver.Resolve("::ffff:8.8.8.8"));
        Assert.Equal("US", resolver.Resolve("2001:4860:4860::8888").Code);
        var china = resolver.Resolve("114.114.114.114");
        Assert.StartsWith("CN/", china.Code);
        Assert.NotEqual("CN/unknown", china.Code);
    }

    private DownloadRegionResolver CreateResolver()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DownloadStatistics:Ip2RegionIpv4DatabasePath"] = "v4.xdb",
            ["DownloadStatistics:Ip2RegionIpv6DatabasePath"] = "v6.xdb"
        }).Build();
        return new(configuration, new TestEnvironment(_directory), NullLogger<DownloadRegionResolver>.Instance);
    }

    // A minimal v3 XDB fixture with one exact address range and one region record.
    // Search is performed by the real IP2Region.Net SDK, with no network/data-file dependency.
    private static void CreateDatabase(string path, int version, string address, string region)
    {
        var ip = IPAddress.Parse(address).GetAddressBytes();
        var regionBytes = Encoding.UTF8.GetBytes(region);
        const int indexStart = 256 + 256 * 256 * 8;
        var indexSize = ip.Length * 2 + 6;
        var data = new byte[indexStart + indexSize + regionBytes.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(data, 3);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), indexStart);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(12), indexStart);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(16), (ushort)version);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(18), 4);
        var vectorOffset = 256 + (ip[0] * 256 + ip[1]) * 8;
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(vectorOffset), indexStart);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(vectorOffset + 4), indexStart);
        if (version == 4) Array.Reverse(ip);
        ip.CopyTo(data, indexStart);
        ip.CopyTo(data, indexStart + ip.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(indexStart + ip.Length * 2), (ushort)regionBytes.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(indexStart + ip.Length * 2 + 2), (uint)(indexStart + indexSize));
        regionBytes.CopyTo(data, indexStart + indexSize);
        File.WriteAllBytes(path, data);
    }

    private sealed class TestEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    public void Dispose()
    {
        if (File.Exists(V4Path)) File.Delete(V4Path);
        if (File.Exists(V6Path)) File.Delete(V6Path);
        Directory.Delete(_directory);
    }
}

public sealed class Ip2RegionDataFactAttribute : FactAttribute
{
    public Ip2RegionDataFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PDC_TEST_IP2REGION_DIRECTORY")))
            Skip = "Set PDC_TEST_IP2REGION_DIRECTORY to the directory containing the official v3 XDB files.";
    }
}
