using System.Security.Claims;
using System.Text.Json.Serialization;
using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.FluentUI.AspNetCore.Components;
using Octokit;
using PhainonDistributionCenter;
using PhainonDistributionCenter.Components;
using PhainonDistributionCenter.Security;
using PhainonDistributionCenter.Security.AuthenticationHandlers;
using PhainonDistributionCenter.Services;

var builder = WebApplication.CreateBuilder(args);

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

                var targetOrg = builder.Configuration["GitHub:Organization"];
                if (string.IsNullOrEmpty(targetOrg))
                {
                    context.Fail("Target GitHub organization is not configured.");
                    return;
                }
                
                // 4. 检查成员资格
                var isMember = orgs?.Any(o => o.Login.Equals(targetOrg, StringComparison.OrdinalIgnoreCase)) ?? false;

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
    });
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddScoped<FileRepoProcessingService>();
builder.Services.AddScoped<GpgSignatureService>();
builder.Services.AddScoped<AccessTokenService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseRouting();
app.MapStaticAssets();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapDefaultControllerRoute();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        
    });
}

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
    if (app.Environment.IsDevelopment())
    {
        db.Database.Migrate();
    }
}

app.Run();
