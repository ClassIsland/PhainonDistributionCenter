using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;
using PhainonDistributionCenter.Components.Controls;
using PhainonDistributionCenter.Models.Statistics;
using Xunit;

namespace PhainonDistributionCenter.Tests;

public class DownloadDistributionRenderingTests
{
    [Theory]
    [InlineData(3, 5, 600)]
    [InlineData(0, 0, 0)]
    [InlineData(long.MaxValue, long.MaxValue, 1000)]
    public async Task ShareUsesDeterminateFluentProgressWithoutOverflow(long count, long total, int value)
    {
        var html = await RenderAsync([new DownloadCount("test-release", count)], total);

        Assert.Contains("<fluent-progress", html);
        Assert.Contains($"value=\"{value}\"", html);
        Assert.Contains("max=\"1000\"", html);
        Assert.Contains("aria-label=\"test-release下载占比\"", html);
        Assert.Contains("aria-valuetext=", html);
        Assert.DoesNotContain("<meter", html);
    }

    [Fact]
    public async Task EmptyDistributionKeepsTheGridAndShowsEmptyContent()
    {
        var html = await RenderAsync([], 0);

        Assert.Contains("暂无数据", html);
        Assert.DoesNotContain("<fluent-progress", html);
    }

    private static async Task<string> RenderAsync(IReadOnlyList<DownloadCount> items, long total)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFluentUIComponents();
        services.AddSingleton<IJSRuntime, NoJavaScript>();
        services.AddSingleton<NavigationManager, TestNavigationManager>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<DownloadDistribution>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(DownloadDistribution.Title)] = "版本分布",
                [nameof(DownloadDistribution.Dimension)] = "发行版本",
                [nameof(DownloadDistribution.Items)] = items,
                [nameof(DownloadDistribution.Total)] = total
            }));
            return WebUtility.HtmlDecode(component.ToHtmlString());
        });
    }

    private sealed class NoJavaScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new InvalidOperationException("Static rendering should not invoke JavaScript.");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("http://localhost/", "http://localhost/DownloadStatistics");

        protected override void NavigateToCore(string uri, bool forceLoad) =>
            throw new InvalidOperationException("Rendering a distribution should not navigate.");
    }
}
