using System.Net;
using System.Security.Claims;
using System.Text.Json.Serialization;
using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.FluentUI.AspNetCore.Components;
using Octokit;
using PhainonDistributionCenter;
using PhainonDistributionCenter.Components;
using PhainonDistributionCenter.Security;
using PhainonDistributionCenter.Security.AuthenticationHandlers;
using PhainonDistributionCenter.Services;
using PhainonDistributionCenter.Services.Cache;

var builder = WebApplication
    .CreateBuilder(args);
var migrateMode = builder.Configuration["migrate"] == "true";
// Add services to the container.

builder.Configuration.AddJsonFile("./data/appsettings.json", optional: true, reloadOnChange: true);
Action<DbContextOptionsBuilder> optionsAction = options =>
{
    var dbType = builder.Configuration["DatabaseType"];
    switch (dbType)
    {
        case "pgsql":
            options.UseNpgsql(
                builder.Configuration.GetConnectionString(
                    builder.Environment.IsDevelopment() ? "Development" : "Production"
                ));
            break;
        default:
            throw new InvalidOperationException($"Unsupported database type: {dbType}");
    }
};
builder.Services.AddDbContext<MainDbContext>(optionsAction,
    contextLifetime: ServiceLifetime.Transient,
    optionsLifetime: ServiceLifetime.Singleton);
builder.Services.AddDbContextFactory<MainDbContext>(optionsAction);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddFluentUIComponents();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = GitHubAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/auth/logout";
    })
    .AddGitHub(options =>
    {
        options.CallbackPath = "/auth/callback/github";
        options.AccessDeniedPath = "/Error";
        options.ClientSecret = builder.Configuration["GitHub:ClientSecret"] ?? "";
        options.ClientId = builder.Configuration["GitHub:ClientId"] ?? "";
        options.Scope.Add("user:email");
        options.Scope.Add("read:org");
        options.Events.OnCreatingTicket = async context =>
        {
            var accessToken = context.AccessToken;
            if (string.IsNullOrEmpty(accessToken))
            {
                context.Fail("Could not retrieve access token from GitHub.");
                return;
            }

            try
            {
                // 2. 创建并验证 Octokit 客户端
                var github = new GitHubClient(new ProductHeaderValue("PhainonDistributionCenter"))
                    {
                        Credentials = new Credentials(accessToken)
                    };

                var user = await github.User.Current();
                // 3. 使用 Octokit 获取用户组织
                var orgs = await github.Organization.GetAllForCurrent();
                var teams = await github.Organization.Team.GetAllForCurrent();

                var targetOrg = builder.Configuration["GitHub:Organization"];
                if (string.IsNullOrEmpty(targetOrg))
                {
                    context.Fail("Target GitHub organization is not configured.");
                    return;
                }
                var writePermTeam = builder.Configuration["GitHub:WritePermTeam"];
                if (string.IsNullOrEmpty(writePermTeam))
                {
                    context.Fail("Target GitHub team is not configured.");
                    return;
                }
                
                // 4. 检查成员资格
                var isMember = orgs?.Any(o => o.Login.Equals(targetOrg, StringComparison.OrdinalIgnoreCase)) ?? false;
                var canWrite = teams?.Any(x =>
                    x.Name.Equals(writePermTeam, StringComparison.OrdinalIgnoreCase) &&
                    x.Organization.Login.Equals(targetOrg, StringComparison.OrdinalIgnoreCase)) ?? false;
                
                if (!string.IsNullOrEmpty(user.AvatarUrl))
                {
                    var avatarClaim = new Claim("urn:github:avatar_url", user.AvatarUrl, ClaimValueTypes.String,
                        context.Options.ClaimsIssuer);
                    context.Identity?.AddClaim(avatarClaim);
                }
                if (isMember)
                {
                    var orgClaim = new Claim("urn:github:org", targetOrg, ClaimValueTypes.String, context.Options.ClaimsIssuer);
                    context.Identity?.AddClaim(orgClaim);
                    if (canWrite)
                    {
                        context.Identity?.AddClaim(new Claim("urn:github:team", writePermTeam, ClaimValueTypes.String, context.Options.ClaimsIssuer));
                        context.Identity?.AddClaim(new Claim("urn:pdc:write", "true", ClaimValueTypes.Boolean, context.Options.ClaimsIssuer));
                    }
                }
                else
                {
                    context.Fail($"User is not a member of the '{targetOrg}' organization.");
                }
            }
            catch (Exception ex)
            {
                context.Fail(ex);
            }
        };
    })
    .AddScheme<TokenOptions, TokenAuthenticationHandler>(TokenAuthenticationHandler.SchemeName, options =>
    {
        
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.IsOrgMemberPolicyName, policy =>
    {
        policy.AddAuthenticationSchemes(GitHubAuthenticationDefaults.AuthenticationScheme);
        policy.AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
        policy.RequireClaim("urn:github:org", builder.Configuration["GitHub:Organization"] ?? "");
    })
    .AddPolicy(Policies.CanWritePolicyName, policy =>
    {
        policy.AddAuthenticationSchemes(GitHubAuthenticationDefaults.AuthenticationScheme);
        policy.AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
        policy.RequireClaim("urn:pdc:write", "true");
        policy.RequireClaim("urn:github:org", builder.Configuration["GitHub:Organization"] ?? "");
    });
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddCors(options =>
{
    options.AddPolicy("PublicApiPolicy", policy =>
    {
        policy.AllowAnyOrigin()   // 允许任何来源
            .AllowAnyHeader()   // 允许任何 Header (解决 sentry-trace 问题)
            .AllowAnyMethod();  // 允许 GET, POST, OPTIONS 等
    });
});

builder.Services.AddScoped<FileRepoProcessingService>();
builder.Services.AddScoped<GpgSignatureService>();
builder.Services.AddScoped<AccessTokenService>();
builder.Services.AddScoped<OrganizationSettingsService>();
builder.Services.AddScoped<DistributionsService>();
builder.Services.AddSingleton<DistributionCacheService>();


builder.WebHost.UseSentry(o =>
{
    o.Dsn = "https://4cb4555138312008b55f73a3c0e55107@todayeatsentry.classisland.tech:21815/10";
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    var forwardedHeadersOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    };
    forwardedHeadersOptions.KnownNetworks.Clear();
    forwardedHeadersOptions.KnownProxies.Clear();
    app.UseForwardedHeaders(forwardedHeadersOptions);
}

app.UseHttpsRedirection();

app.UseRouting();
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapControllers();
app.MapDefaultControllerRoute();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapStaticAssets();
app.UseSentryTracing();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        
    });
}

var logger = app.Services.GetRequiredService<ILogger<Program>>();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
    if (app.Environment.IsDevelopment())
    {
        db.Database.Migrate();
    }
    
    if (migrateMode)
    {
        db.Database.Migrate();

        logger.LogInformation("已完成数据库迁移，应用即将退出");
        return;
    }
    
    var migrations = await db.Database.GetPendingMigrationsAsync();
    if (migrations.Any())
    {
        logger.LogWarning("数据库未迁移，请在运行应用前先完成数据库迁移。使用参数 --migrate=true 进行数据库迁移");
        return;
    }
}

app.Run();
