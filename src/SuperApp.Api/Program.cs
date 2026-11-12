using Serilog;
using SuperApp.Api.Extensions;
using SuperApp.Api.Security;
using SuperApp.Application;
using SuperApp.Infrastructure;
using SuperApp.Infrastructure.Persistence.Seed;

// Secrets come from .env (never committed); real environment variables win over it
DotNetEnv.Env.NoClobber().TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// No "Server: Kestrel" header, and a small default body: only the upload endpoints raise their own limit
builder.WebHost.ConfigureKestrel(o =>
{
    o.AddServerHeader = false;
    o.Limits.MaxRequestBodySize = 2 * 1024 * 1024;
});

builder.Host.UseSerilog((context, services, log) => log
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddApiLayer(builder.Configuration);

// Site files (css, js, svg, the page itself) are sent compressed: about a quarter of the size on the wire.
// API answers (JSON) are left as they are, so no secret is ever inside a compressed https response.
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.MimeTypes = ["text/css", "text/javascript", "application/javascript", "image/svg+xml", "text/html", "application/manifest+json", "application/xml", "text/plain"];
});

var app = builder.Build();
app.EnsureSafeToStart();   // Production only: refuses unsafe settings (ProductionGuard)
app.UseApiPipeline();

// Idempotent seed (ERD §12): every start in development, elsewhere only when Seed:OnStartup=true
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Seed:OnStartup"))
{
    await app.Services.SeedDatabaseAsync();
}
await app.RunAsync();

/// <summary>Entry point; public so the integration tests can start the API in memory.</summary>
public partial class Program;
