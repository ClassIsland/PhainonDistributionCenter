using Microsoft.AspNetCore.Http.Extensions;

namespace PhainonDistributionCenter.Middleware;

public static class GitHubCallbackLoadingMiddlewareExtensions
{
    private const string CallbackPath = "/auth/callback/github";
    private const string LoadingQueryKey = "pdc_loading";
    private const string EncodedContinueUrlPlaceholder = "{{encodedContinueUrl}}";
    private static readonly object TemplateLock = new();
    private static string? loadingHtmlTemplate;

    public static IApplicationBuilder UseGitHubCallbackLoadingPage(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            if (context.Request.Path.Equals(CallbackPath, StringComparison.OrdinalIgnoreCase))
            {
                var isOAuthCallback = context.Request.Query.ContainsKey("code") || context.Request.Query.ContainsKey("error");
                var isSecondPass = string.Equals(context.Request.Query[LoadingQueryKey], "1", StringComparison.Ordinal);

                if (isOAuthCallback && !isSecondPass)
                {
                    var html = BuildLoadingHtml(context);
                    context.Response.StatusCode = StatusCodes.Status200OK;
                    context.Response.ContentType = "text/html; charset=utf-8";
                    await context.Response.WriteAsync(html);
                    return;
                }
            }

            await next();
        });
    }

    private static string BuildLoadingHtml(HttpContext context)
    {
        var continueCallbackUrl = BuildContinueCallbackUrl(context);
        var encodedContinueUrl = Uri.EscapeDataString(continueCallbackUrl);
        var template = GetLoadingHtmlTemplate(context);
        return template.Replace(EncodedContinueUrlPlaceholder, encodedContinueUrl, StringComparison.Ordinal);
    }

    private static string BuildContinueCallbackUrl(HttpContext context)
    {
        var queryBuilder = new QueryBuilder();
        foreach (var query in context.Request.Query)
        {
            if (query.Key.Equals(LoadingQueryKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var value in query.Value)
            {
                queryBuilder.Add(query.Key, value ?? string.Empty);
            }
        }

        queryBuilder.Add(LoadingQueryKey, "1");
        return $"{context.Request.PathBase}{CallbackPath}{queryBuilder.ToQueryString()}";
    }

    private static string GetLoadingHtmlTemplate(HttpContext context)
    {
        if (loadingHtmlTemplate is not null)
        {
            return loadingHtmlTemplate;
        }

        lock (TemplateLock)
        {
            if (loadingHtmlTemplate is not null)
            {
                return loadingHtmlTemplate;
            }

            var webHostEnvironment = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
            var templatePath = Path.Combine(webHostEnvironment.WebRootPath, "templates", "github-callback-loading.html");
            loadingHtmlTemplate = File.ReadAllText(templatePath);
            return loadingHtmlTemplate;
        }
    }
}
