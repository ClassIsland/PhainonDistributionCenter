using System.Text.Json.Serialization;
using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.FluentUI.AspNetCore.Components;
using PhainonDistributionCenter;
using PhainonDistributionCenter.Components;
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
    });

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddScoped<FileRepoProcessingService>();
builder.Services.AddScoped<GpgSignatureService>();

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
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapGet("/Account/Login", context =>
{
    return context.ChallengeAsync(GitHubAuthenticationDefaults.AuthenticationScheme, new AuthenticationProperties { RedirectUri = "/" });
});

app.MapGet("/auth/logout", async context =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    context.Response.Redirect("/");
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
    if (app.Environment.IsDevelopment())
    {
        db.Database.Migrate();
    }
}

app.Run();
