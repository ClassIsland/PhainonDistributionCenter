namespace PhainonDistributionCenter.Client.Helpers;

internal sealed class UploadSettings
{
    public const int DefaultMaxParallelism = 16;
    public const int DefaultMaxRetries = 5;

    public int MaxParallelism { get; private init; }
    public int MaxRetries { get; private init; }

    public static UploadSettings FromEnvironment(Func<string, string?>? getVariable = null)
    {
        getVariable ??= Environment.GetEnvironmentVariable;
        return new UploadSettings
        {
            MaxParallelism = ReadInteger("PDC_MaxParallelism", DefaultMaxParallelism, 1, getVariable),
            MaxRetries = ReadInteger("PDC_MaxRetries", DefaultMaxRetries, 0, getVariable)
        };
    }

    private static int ReadInteger(string name, int defaultValue, int minimum, Func<string, string?> getVariable)
    {
        var value = getVariable(name);
        if (value == null) return defaultValue;
        if (int.TryParse(value, out var result) && result >= minimum) return result;

        throw new InvalidOperationException($"环境变量 {name} 必须是大于或等于 {minimum} 的 32 位整数。");
    }
}
