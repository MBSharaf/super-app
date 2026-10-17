using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Infrastructure;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Tests.Seed;

/// <summary>
/// A throw-away LocalDB database built from the model (no migration needed), dropped after the tests.
/// The real database is created by the owner with Add-Migration / Update-Database.
/// </summary>
public sealed class SeedDatabaseFixture : IAsyncLifetime
{
    public const string CeoEmail = "ceo@superapp.test";
    private readonly string name = $"SuperApp_SeedTest_{Guid.NewGuid():N}";

    public ServiceProvider Services { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = $"Server=(localdb)\\MSSQLLocalDB;Database={name};Trusted_Connection=True;TrustServerCertificate=True",
            ["Seed:Ceo_Email"] = CeoEmail,
            ["Seed:Ceo_Password"] = "Test-only-1234",
            ["Seed:Ceo_Name"] = "Test CEO",
            ["Seed:DemoData"] = "true",
        }).Build();

        var services = new ServiceCollection().AddLogging();
        services.AddSingleton(Substitute.For<ICurrentUser>());
        services.AddInfrastructure(configuration);
        Services = services.BuildServiceProvider();

        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
        await Services.DisposeAsync();
    }

    public async Task<T> QueryAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        await using var scope = Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public Task<int> CountAsync<TEntity>() where TEntity : class =>
        QueryAsync(db => db.Set<TEntity>().IgnoreQueryFilters().CountAsync());
}
