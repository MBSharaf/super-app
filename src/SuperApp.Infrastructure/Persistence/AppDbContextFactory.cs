using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace SuperApp.Infrastructure.Persistence;

/// <summary>
/// Used only by the EF Core tools (Add-Migration, Update-Database, dotnet ef) at design time.
/// The connection string comes from the CONNECTIONSTRINGS__DEFAULT environment variable (or .env),
/// falling back to the local SQL Server LocalDB database "SuperApp".
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string LocalDb = "Server=(localdb)\\MSSQLLocalDB;Database=SuperApp;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("CONNECTIONSTRINGS__DEFAULT") ?? LocalDb;
        // Identity reads its schema version from the application services; give the tools the same one as the app
        var services = new ServiceCollection()
            .Configure<IdentityOptions>(o => o.Stores.SchemaVersion = AppDbContext.IdentitySchema)
            .BuildServiceProvider();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .UseApplicationServiceProvider(services)
            .Options;
        return new AppDbContext(options);
    }
}
