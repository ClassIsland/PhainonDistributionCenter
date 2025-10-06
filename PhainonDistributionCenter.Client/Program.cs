// See https://aka.ms/new-console-template for more information
//
// 夢の続きを知りたいのかい？
// 你想知道梦的后续吗？
// 誰も見たこと無い絵本を捲りなさい，
// 那么就翻开谁也没看过的绘本吧，
// それがあなたの望む世界だとしよう，
// 就把那当作你所渴望的世界，
// 夢の終わりで眠ればいい，
// 你只需要在梦的终点沉睡就好。

/*
 * 要使用 Publish 模式，需要定义以下的环境变量：
 * PDC_Endpoint - PDC 终结点
 * PDC_Token - PDC 令牌
 * S3_Endpoint - S3 存储桶终结点
 * S3_Bucket - S3 存储桶名称
 * S3_AccessKey - 顾名思义
 * S3_SecretKey - 顾名思义
 * S3_Region - 可用区
 */

using System.Collections;
using System.CommandLine;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using PgpCore;
using PhainonDistributionCenter.Client.Helpers;
using PhainonDistributionCenter.Client.Models;
using PhainonDistributionCenter.Shared.Helpers;
using PhainonDistributionCenter.Shared.Models;
using PhainonDistributionCenter.Shared.Models.Api.Requests;
using PhainonDistributionCenter.Shared.Models.Api.Responses;
using PhainonDistributionCenter.Shared.Models.FileMap;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

var deserializer = new DeserializerBuilder()
    .IgnoreUnmatchedProperties()
    .WithNamingConvention(CamelCaseNamingConvention.Instance)
    .Build();

var rootPathArg = new Argument<DirectoryInfo>("root");
var configArg = new Argument<FileInfo>("config");
var generateComponentCommand = new Command("GenerateFileMap", "创建子频道的文件图")
{
    Arguments =
    {
        rootPathArg
    }
};
generateComponentCommand.SetAction(result =>
{
    var config = LoadConfiguration(result);
    
    var root = result.GetValue(rootPathArg);
    if (root == null)
    {
        Console.Error.WriteLine("根路径文件夹无效。");
        return;
    }

    using var sha512 = SHA512.Create();
    var fileMap = new FileMap()
    {
        Variables = config.Variables
    };
    
    foreach (var (id, compConfig) in config.Components)
    {
        Console.WriteLine($"Generating component {id}");
        var compRoot = Path.Combine(root.FullName, VariableStringHelpers.ExpandString(compConfig.Root, config.Variables));
        Console.WriteLine($"[COMP/{id}] Root path: {compRoot}");
        var matcher = new Matcher();
        var comp = new FileMapComponent()
        {
            Root = compConfig.Root,
            AllowDiffUpdate = compConfig.AllowDiffUpdate
        };
        matcher.AddIncludePatterns(compConfig.Includes);
        matcher.AddExcludePatterns(compConfig.Excludes);
        var matchingResult = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(compRoot)));
        foreach (var file in matchingResult.Files)
        {
            var filePath = Path.Combine(compRoot, file.Path);
            using var fileStream = File.OpenRead(filePath);
            var hash = sha512.ComputeHash(fileStream);
            Console.WriteLine($"[COMP/{id}] File {file.Path} SHA512: {Convert.ToBase64String(hash)}");
            comp.Files[file.Path] = new FileMapFile()
            {
                FileSha512 = hash
            };
        }

        fileMap.Components[id] = comp;
        Console.WriteLine($"[COMP/{id}] {comp.Files.Count} files processed");
    }
    
    Console.WriteLine($"{fileMap.Components.Count} components processed");
    File.WriteAllText(Path.Combine(root.FullName, "files.json"), JsonSerializer.Serialize(fileMap));
});

var primaryVersionArg = new Argument<string>("primaryVersion");
var versionArg = new Argument<string>("version");
var publishAppCommand = new Command("Publish", "发布应用到分发服务")
{
    Arguments =
    {
        primaryVersionArg,
        versionArg,
        rootPathArg
    }
};
publishAppCommand.SetAction(async result =>
{
    var config = LoadConfiguration(result);
    var endpoint = GetRequiredValueFromEnvironment("PDC_Endpoint");
    var token = GetRequiredValueFromEnvironment("PDC_Token");
    var signingKey = GetRequiredValueFromEnvironment("PDC_SigningKey");
    var signingKeyPs = GetRequiredValueFromEnvironment("PDC_SigningKeyPs");
    var s3AccessKey = GetRequiredValueFromEnvironment("S3_AccessKey");
    var s3SecretKey = GetRequiredValueFromEnvironment("S3_SecretKey");
    var s3Region = GetRequiredValueFromEnvironment("S3_Region");
    var s3Bucket = GetRequiredValueFromEnvironment("S3_Bucket");
    var s3Endpoint = GetRequiredValueFromEnvironment("S3_Endpoint");
    
    var root = result.GetValue(rootPathArg);
    if (root == null)
    {
        Console.Error.WriteLine("根路径文件夹无效。");
        return;
    }

    var version = result.GetValue(versionArg);
    if (version == null)
    {
        Console.Error.WriteLine("指定的版本无效。");
        return;
    }
    
    var primaryVersion = result.GetValue(primaryVersionArg);
    if (primaryVersion == null)
    {
        Console.Error.WriteLine("指定的大版本无效。");
        return;
    }
    
    Console.WriteLine($"Version: {version}");

    var pattern = $@"^{config.Name}_app_([^_]+)_([^_]+)_([^_]+)_([^_]+)$";
    var regex = new Regex(pattern, RegexOptions.Compiled);
    var subChannels = Directory.GetFiles(root.FullName)
        .Select(Path.GetFileName)
        .Where(x => x != "repo")
        .OfType<string>()
        .Where(x => regex.Match(Path.GetFileNameWithoutExtension(x)).Success)
        .Select(x =>
        {
            var match = regex.Match(Path.GetFileNameWithoutExtension(x));
            return new
            {
                Name = x,
                FullPath = Path.Combine(root.FullName, x),
                Os = match.Groups[1].Value,
                Arch = match.Groups[2].Value,
                BuildType = match.Groups[3].Value,
                Package = match.Groups[4].Value,
                IsArchive = false,
                Request = new AddDistributionInfoRequest.DistributionSubChannel()
            };
        })
        .ToList();
    
    Console.WriteLine($"Found {subChannels.Count} subchannels");
    foreach (var channel in subChannels)
    {
        Console.WriteLine(channel);
    }

    var request = new AddDistributionInfoRequest()
    {
        ChangeLog = ""
    };
    var repo = new FileRepo();
    var repoPath = Path.Combine(root.FullName, "repo");
    if (!Directory.Exists(repoPath))
    {
        Directory.CreateDirectory(repoPath);
    }
    Console.WriteLine("Processing files...");
    using var sha512 = SHA512.Create();
    foreach (var channel in subChannels)
    {
        var reqChannel = channel.Request;
        Console.WriteLine($"[SC/{channel.Name}] Begin processing");
        reqChannel.Os = channel.Os;
        reqChannel.Arch = channel.Arch;
        reqChannel.BuildType = channel.BuildType;
        reqChannel.Package = channel.Package;
        var isFolder = channel.Package == "folder";
        var extractedPath = Path.Combine(root.FullName, Path.GetFileNameWithoutExtension(channel.FullPath));
        await using var archive = File.OpenRead(channel.FullPath);
        if (isFolder)
        {
            ZipFile.ExtractToDirectory(archive,
                extractedPath);
        }

        var fileMap = isFolder ? JsonSerializer.Deserialize<FileMap>(File.OpenRead(Path.Combine(extractedPath, "files.json"))) :
            new FileMap();
        if (fileMap == null)
        {
            continue;
        }

        if (isFolder)
        {
            foreach (var (id, component) in fileMap.Components)
            {
                Console.WriteLine($"[SC/{channel.Name}/{id}] Begin processing");
                var compRoot = Path.Combine(extractedPath, VariableStringHelpers.ExpandString(component.Root, config.Variables));
                Console.WriteLine($"[SC/{channel.Name}/{id}] Root: {compRoot}");
                foreach (var (path, fileInfo) in component.Files)
                {
                    var sha512Base64 = Convert.ToBase64String(fileInfo.FileSha512);
                    if (repo.Items.ContainsKey(sha512Base64))
                    {
                        continue;
                    }

                    var sha512Hex = Convert.ToHexStringLower(fileInfo.FileSha512);
                    var fileName = Path.GetFileName(path);
                    
                    var dirPath = Path.Combine(repoPath, sha512Hex[..2]);
                    if (!Directory.Exists(dirPath))
                    {
                        Directory.CreateDirectory(dirPath);
                    }

                    var rawPath = Path.Combine(compRoot, path);
                    var compressedPath = Path.Combine(dirPath, sha512Hex);
                    await using (var compressStream = File.Create(compressedPath))
                    {
                        await using var rawStream = File.OpenRead(rawPath);
                        await using var compressor = new GZipStream(compressStream, CompressionMode.Compress);
                        rawStream.CopyTo(compressor);
                    }
                    await using var compressedFileStream = File.OpenRead(compressedPath);
                    var compressedHash = sha512.ComputeHash(compressedFileStream);

                    fileInfo.ArchiveDownloadUrl = config.FileRepoRoot + $"{sha512Hex[..2]}/{sha512Hex}";
                    fileInfo.ArchiveSha512 = compressedHash;
                    repo.Items.Add(sha512Base64, new FileRepoItem()
                    {
                        FileSha512 = fileInfo.FileSha512,
                        ArchiveDownloadUrl = fileInfo.ArchiveDownloadUrl,
                        ArchiveSha512 = fileInfo.ArchiveSha512,
                        FileName = fileName
                    });
                    Console.WriteLine($"[SC/{channel.Name}/{id}] Added file {fileName} (SHA512='{sha512Base64}')");
                }
            }
        }

        Console.WriteLine($"[SC/{channel.Name}] Generating archive info...");
        fileMap.ArchiveSha512 = sha512.ComputeHash(archive);
        fileMap.ArchiveUrl = VariableStringHelpers.ExpandString(config.ArchiveRoot, config.Variables) +
                             Path.GetFileName(channel.FullPath);
        Console.WriteLine($"[SC/{channel.Name}] Signing FileMap...");
        var fileMapJson = reqChannel.FileMap = JsonSerializer.Serialize(fileMap);
        reqChannel.FileMapSignature = DetachedSignatureProcessor.CreateSignature(fileMapJson, signingKey, signingKeyPs);
        request.SubChannels.Add(reqChannel);
    }
    File.WriteAllText(Path.Combine(root.FullName, "request.json"), JsonSerializer.Serialize(request));
    Console.WriteLine($"Request dumped request to {Path.Combine(root.FullName, "request.json")}");
    File.WriteAllText(Path.Combine(root.FullName, "repo.json"), JsonSerializer.Serialize(repo));
    Console.WriteLine($"Repo dumped request to {Path.Combine(root.FullName, "repo.json")}");
    Console.WriteLine($"Files in repo: {repo.Items.Count}");
    
    Console.WriteLine($"Determining files to upload...");
    var httpClient = new HttpClient()
    {
        BaseAddress = new Uri(endpoint),
        DefaultRequestHeaders =
        {
            { "X-PDC-Token", token }
        }
    };
    var fileRepoDiff = await httpClient.PostAsJsonAsync("api/v1/fileMaps/diff", repo);
    var uploadingFileSha512 = await fileRepoDiff.Content.ReadFromJsonAsync<Result<List<string>>>();
    if (uploadingFileSha512?.Content == null)
    {
        throw new ArgumentNullException(nameof(uploadingFileSha512));
    } 
    
    var repoDetermined = new FileRepo();
    foreach (var k in uploadingFileSha512.Content)
    {
        if (!repo.Items.TryGetValue(k, out var item))
        {
            continue;
        }
        repoDetermined.Items.Add(k, item);
    }

    Console.WriteLine($"{repoDetermined.Items.Count} files to upload");
    var awsCredential = new BasicAWSCredentials(
        s3AccessKey,
        s3SecretKey);
    using var client = new AmazonS3Client(new AmazonS3Config{
        DefaultAWSCredentials = awsCredential,
        ServiceURL = s3Endpoint
    });

    foreach (var (_, file) in repoDetermined.Items)
    {
        var sha512Hex = Convert.ToHexStringLower(file.FileSha512); 
        var dirPath = Path.Combine(repoPath, sha512Hex[..2]);
        var compressedPath = Path.Combine(dirPath, sha512Hex);
        Console.WriteLine($"Uploading {file.FileName} ({compressedPath})");
        var putRequest = new PutObjectRequest()
        {
            BucketName = s3Bucket,
            Key = VariableStringHelpers.ExpandString(config.BucketKeyRoot, config.Variables) + $"{sha512Hex[..2]}/{sha512Hex}",
            FilePath = compressedPath
        };
        var rsp = await client.PutObjectAsync(putRequest);
    }

    var rsp2 = await httpClient.PostAsJsonAsync("api/v1/fileMaps/upload", repoDetermined);
    rsp2.EnsureSuccessStatusCode();
    
    Console.WriteLine("SUCCESSFULLY uploaded file repo");
    
    Console.WriteLine("Uploading subchannel packages...");
    foreach (var channel in subChannels)
    {
        Console.WriteLine($"[SC/{channel.Name}] Uploading {channel.FullPath}");
        var putRequest = new PutObjectRequest()
        {
            BucketName = s3Bucket,
            Key = VariableStringHelpers.ExpandString(config.ArchiveBucketKeyRoot, config.Variables) + Path.GetFileName(channel.FullPath),
            FilePath = channel.FullPath
        };
        var rsp = await client.PutObjectAsync(putRequest);
        Console.WriteLine($"[SC/{channel.Name}] SUCCESSFULLY Uploaded {channel.FullPath}");
    }

    var rsp3 = await httpClient.PostAsJsonAsync($"api/v1/distribution/{primaryVersion}/{version}", request);
    rsp3.EnsureSuccessStatusCode();
    Console.WriteLine("SUCCESSFULLY created distribution info");
    Console.WriteLine($"COMPLETED!");
});


var rootCommand = new RootCommand("PhainonDistributionCenter Client")
{
    Subcommands =
    {
        generateComponentCommand,
        publishAppCommand
    },
    Arguments =
    {
        configArg
    }
};
var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();

Configuration LoadConfiguration(ParseResult result)
{
    var fileInfo = result.GetValue(configArg);
    if (fileInfo == null)
    {
        throw new Exception("配置文件无效");
    }

    var configYaml = new StreamReader(fileInfo.OpenRead()).ReadToEnd();
    var config = deserializer.Deserialize<Configuration>(configYaml);

    Console.WriteLine($"Name: {config.Name}");
    foreach (var (k, v) in Environment.GetEnvironmentVariables().OfType<DictionaryEntry>())
    {
        if ((k is not string key) || (v is not string value) || !key.StartsWith("PDCC_"))
        {
            continue;
        }

        var name = key[5..];
        Console.WriteLine($"Set variable from env: {name}={value}");
        config.Variables[name] = value;
    }
    
    return config;
}

string GetRequiredValueFromEnvironment(string key)
{
    if (Environment.GetEnvironmentVariable(key) is not {} v)
    {
        throw new InvalidOperationException($"环境变量值 {key} 未设置");
    }

    return v;
}