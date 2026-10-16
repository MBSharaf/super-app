using Serilog;
using SuperApp.Api.Extensions;
using SuperApp.Application;
using SuperApp.Infrastructure;

// Secrets come from .env (never committed); real environment variables win over it
DotNetEnv.Env.NoClobber().TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, log) => log
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddApiLayer(builder.Configuration);

var app = builder.Build();
app.UseApiPipeline();
await app.RunAsync();

/// <summary>Entry point; public so the integration tests can start the API in memory.</summary>
public partial class Program;
