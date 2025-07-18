using Microsoft.EntityFrameworkCore;
using Microsoft.FluentUI.AspNetCore.Components;
using PhainonDistributionCenter;
using PhainonDistributionCenter.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Configuration.AddJsonFile("./data/appsettings.json", optional: true, reloadOnChange: true);
builder.Services.AddDbContext<MainDbContext>(options =>
{
    var dbType = builder.Configuration["DatabaseType"];
    switch (dbType)
    {
        case "mysql":
            options.UseMySql(
                builder.Configuration.GetConnectionString(
                    builder.Environment.IsDevelopment() ? "Development" : "Production"
                ),ServerVersion.Parse("8.0.0-mysql"));
            break;
        default:
            throw new InvalidOperationException($"Unsupported database type: {dbType}");
    }
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddFluentUIComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MainDbContext>();
    if (app.Environment.IsDevelopment())
    {
        db.Database.Migrate();
    }
}

app.Run();
