using System.Text.Json.Serialization;
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

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
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
