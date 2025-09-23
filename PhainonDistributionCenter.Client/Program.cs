// See https://aka.ms/new-console-template for more information

using System.Collections;
using System.CommandLine;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using PhainonDistributionCenter.Client.Models;
using PhainonDistributionCenter.Shared.Helpers;
using PhainonDistributionCenter.Shared.Models.FileMap;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

var deserializer = new DeserializerBuilder()
    .IgnoreUnmatchedProperties()
    .WithNamingConvention(CamelCaseNamingConvention.Instance)
    .Build();

var rootPathArg = new Argument<FileInfo>("root");
var configArg = new Argument<FileInfo>("config");
var subChannelArg = new Argument<FileInfo>("config");
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

    using var mySha256 = SHA256.Create();
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
            var hash = mySha256.ComputeHash(fileStream);
            Console.WriteLine($"[COMP/{id}] File {file.Path} SHA256: {Convert.ToBase64String(hash)}");
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


var rootCommand = new RootCommand("PhainonDistributionCenter Client")
{
    Subcommands =
    {
        generateComponentCommand
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