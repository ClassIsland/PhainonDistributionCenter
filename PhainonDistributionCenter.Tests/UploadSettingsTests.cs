using PhainonDistributionCenter.Client.Helpers;
using Xunit;

namespace PhainonDistributionCenter.Tests;

public class UploadSettingsTests
{
    [Fact]
    public void MissingEnvironmentVariablesUseDefaults()
    {
        var settings = UploadSettings.FromEnvironment(_ => null);

        Assert.Equal(16, settings.MaxParallelism);
        Assert.Equal(5, settings.MaxRetries);
    }

    [Theory]
    [InlineData("PDC_MaxParallelism", "1", 1, 5)]
    [InlineData("PDC_MaxParallelism", "32", 32, 5)]
    [InlineData("PDC_MaxRetries", "0", 16, 0)]
    [InlineData("PDC_MaxRetries", "40", 16, 40)]
    public void EnvironmentVariablesOverrideTheirRespectiveDefaults(
        string name, string value, int maxParallelism, int maxRetries)
    {
        var settings = UploadSettings.FromEnvironment(key => key == name ? value : null);

        Assert.Equal(maxParallelism, settings.MaxParallelism);
        Assert.Equal(maxRetries, settings.MaxRetries);
    }

    [Theory]
    [InlineData("PDC_MaxParallelism", "0")]
    [InlineData("PDC_MaxParallelism", "-1")]
    [InlineData("PDC_MaxParallelism", "invalid")]
    [InlineData("PDC_MaxParallelism", "")]
    [InlineData("PDC_MaxParallelism", "2147483648")]
    [InlineData("PDC_MaxRetries", "-1")]
    [InlineData("PDC_MaxRetries", "invalid")]
    [InlineData("PDC_MaxRetries", "1.5")]
    [InlineData("PDC_MaxRetries", "")]
    [InlineData("PDC_MaxRetries", "2147483648")]
    public void InvalidEnvironmentVariablesReportTheSettingName(string name, string value)
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            UploadSettings.FromEnvironment(key => key == name ? value : null));

        Assert.Contains(name, error.Message);
    }
}
