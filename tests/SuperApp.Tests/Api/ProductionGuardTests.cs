using Microsoft.Extensions.Configuration;
using Shouldly;
using SuperApp.Api.Security;

namespace SuperApp.Tests.Api;

/// <summary>The launch check, without a server: which production settings stop the start, and when everything is ready.</summary>
public sealed class ProductionGuardTests
{
    private static readonly Dictionary<string, string?> Ready = new()
    {
        ["Payments:Paymob:SecretKey"] = "sk", ["Payments:Paymob:PublicKey"] = "pk", ["Payments:Paymob:HmacSecret"] = "hm", ["Payments:Paymob:IntegrationIds"] = "1,2",
        ["Payments:PublicBaseUrl"] = "https://superapp.example", ["AllowedHosts"] = "superapp.example;www.superapp.example",
        ["Seed:DemoData"] = "false", ["Docs:Enabled"] = "false", ["Email:Mode"] = "Smtp", ["Email:Smtp_Host"] = "smtp.example",
        ["ConnectionStrings:Default"] = "Server=db;Database=SuperApp;User Id=superapp;Password=x", ["Security:Data_Protection_Keys_Path"] = "/var/superapp/keys",
    };

    private static List<string> Problems(params (string Key, string? Value)[] changes)
    {
        var settings = new Dictionary<string, string?>(Ready);
        foreach (var (key, value) in changes)
        {
            settings[key] = value;
        }
        return ProductionGuard.Problems(new ConfigurationBuilder().AddInMemoryCollection(settings).Build()).ToList();
    }

    [Fact]
    public void Complete_settings_are_ready() => Problems().ShouldBeEmpty();

    [Fact]
    public void Missing_payment_keys_stop_the_start_unless_a_staging_server_allows_simulated_payments()
    {
        Problems(("Payments:Paymob:SecretKey", ""), ("Payments:Paymob:HmacSecret", "")).Count.ShouldBe(2);
        Problems(("Payments:Paymob:SecretKey", ""), ("Launch:AllowSimulatedPayments", "true")).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("AllowedHosts", "*")]
    [InlineData("Seed:DemoData", "true")]
    [InlineData("Seed:DemoData", null)]
    [InlineData("Docs:Enabled", "true")]
    [InlineData("Email:Mode", "File")]
    [InlineData("Payments:PublicBaseUrl", "http://superapp.example")]
    [InlineData("Payments:PublicBaseUrl", "https://localhost:7254")]
    [InlineData("ConnectionStrings:Default", "Server=(localdb)\\MSSQLLocalDB;Database=SuperApp")]
    [InlineData("Security:Data_Protection_Keys_Path", "")]
    public void Each_unsafe_setting_is_reported(string key, string? value) => Problems((key, value)).Count.ShouldBe(1);
}
