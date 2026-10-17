using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SuperApp.Tests.Api;

/// <summary>
/// The real API in memory (same pipeline as Program.cs). The "Testing" environment skips the start-up seed and
/// the API reference; the database is never touched by these tests.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", "Server=(localdb)\\MSSQLLocalDB;Database=SuperApp_NotUsed;Trusted_Connection=True");
        builder.UseSetting("Security:Data_Protection_Keys_Path", Path.Combine(Path.GetTempPath(), "superapp-test-keys"));
    }
}
