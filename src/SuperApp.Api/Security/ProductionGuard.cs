namespace SuperApp.Api.Security;

/// <summary>
/// Launch check. In Production the server refuses to start while a setting would make the live site unsafe: free
/// (simulated) payments, demo data, emails saved as files, an open host list, the API reference on, or a missing key.
/// The error lists every problem at once so .env can be fixed in one go. Development and tests are not checked.
/// A staging server may set <c>LAUNCH__ALLOWSIMULATEDPAYMENTS=true</c> to run without the payment keys.
/// </summary>
public static class ProductionGuard
{
    public static void EnsureSafeToStart(this WebApplication app)
    {
        if (!app.Environment.IsProduction())
        {
            return;
        }
        var problems = Problems(app.Configuration).ToList();
        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                "The server is not safe to start in Production. Fix these settings in .env:" + Environment.NewLine + " - " + string.Join(Environment.NewLine + " - ", problems));
        }
    }

    /// <summary>Every unsafe production setting, in plain words (empty = ready).</summary>
    public static IEnumerable<string> Problems(IConfiguration c)
    {
        var simulatedAllowed = c.GetValue("Launch:AllowSimulatedPayments", false);
        if (!simulatedAllowed)
        {
            foreach (var key in new[] { "SecretKey", "PublicKey", "HmacSecret", "IntegrationIds" }.Where(k => string.IsNullOrWhiteSpace(c[$"Payments:Paymob:{k}"])))
            {
                yield return $"PAYMENTS__PAYMOB__{key.ToUpperInvariant()} is empty — without it every payment would be marked paid for free.";
            }
        }
        var site = c["Payments:PublicBaseUrl"];
        if (string.IsNullOrWhiteSpace(site) || !site.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || site.Contains("localhost", StringComparison.OrdinalIgnoreCase))
        {
            yield return "PAYMENTS__PUBLICBASEURL must be the live https address of the site (e.g. https://superapp.eg).";
        }
        var hosts = c["AllowedHosts"];
        if (string.IsNullOrWhiteSpace(hosts) || hosts.Split(';').Any(h => h.Trim() == "*"))
        {
            yield return "ALLOWEDHOSTS must list the site's domain(s), e.g. superapp.eg;www.superapp.eg — not *.";
        }
        if (!bool.TryParse(c["Seed:DemoData"], out var demo) || demo)
        {
            yield return "SEED__DEMODATA must be false — the demo pitches, sellers and accounts must not reach the live site.";
        }
        if (c.GetValue("Docs:Enabled", false))
        {
            yield return "DOCS__ENABLED must be false — the API reference is for development only.";
        }
        if (!string.Equals(c["Email:Mode"], "Smtp", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(c["Email:Smtp_Host"]))
        {
            yield return "EMAIL__MODE must be Smtp with EMAIL__SMTP_HOST set — otherwise reset and confirmation emails are only saved as files.";
        }
        var connection = c.GetConnectionString("Default") ?? string.Empty;
        if (connection.Contains("(localdb)", StringComparison.OrdinalIgnoreCase))
        {
            yield return "CONNECTIONSTRINGS__DEFAULT points at LocalDB — use the production SQL Server with its own login (not sa).";
        }
        if (string.IsNullOrWhiteSpace(c["Security:Data_Protection_Keys_Path"]))
        {
            yield return "SECURITY__DATA_PROTECTION_KEYS_PATH is empty — set the folder that keeps the sign-in keys (it must be in the backups).";
        }
    }
}
