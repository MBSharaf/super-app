namespace SuperApp.Application.Features.CeoSystem;

/// <summary>One endpoint group: path prefix under /api/v1, who may call it, and the switch that turns it off (if any).</summary>
public sealed record ApiGroupInfo(string Group, string Name, string Purpose, string Permission, string Callers, string Path, string? SwitchKey);

/// <summary>
/// The API catalogue the CEO sees (API document, every section). Groups with a switch (<c>api.*</c> or a kill switch)
/// can be turned off from the catalogue; callers then get 423.
/// </summary>
public static class ApiCatalogue
{
    public static readonly IReadOnlyList<ApiGroupInfo> Groups =
    [
        new("auth", "Sign-in and accounts", "Sign up, sign in, 2FA, password links", "Public", "Site", "/auth", null),
        new("me", "My account", "Profile, addresses, notifications, sessions", "Signed in", "Site", "/me", null),
        new("pitches", "Pitches", "Search pitches, availability, reviews", "Public", "Site, agents", "/pitches", null),
        new("bookings", "Bookings", "Book, pay, cancel, QR", "Signed in", "Site", "/bookings", "kill.bookings"),
        new("packages", "Weekly packages", "Quotes, buy, cancel games", "Signed in", "Site", "/packages", "kill.bookings"),
        new("partner", "Partner dashboard", "Venues, pitches, bookings, staff, check-in", "Partner", "Partner dashboard", "/partner", null),
        new("shop", "Shop", "Products, cart, checkout, orders", "Public / signed in", "Site", "/shop", "kill.shop"),
        new("seller", "Seller studio", "Designs, store, earnings", "Seller", "Seller dashboard", "/seller", "kill.pod"),
        new("factory", "Factory", "Jobs, staff, devices", "Factory", "Factory system", "/factory", null),
        new("fantasy", "Fantasy", "Teams, transfers, groups, Fantasy Pass", "Signed in", "Site", "/fantasy", "api.fantasy"),
        new("communities", "Communities", "Posts, comments, polls", "Signed in", "Site", "/communities", "kill.community"),
        new("support", "Support", "Tickets and live chat", "Signed in", "Site", "/support", null),
        new("applications", "Applications", "Partner, seller and factory applications", "Signed in", "Site", "/applications", "kill.applications"),
        new("admin", "Admin console", "Every admin screen", "Admin permission (2FA) or agent key", "Admin console, agents", "/admin", null),
        new("ceo", "CEO system", "Team, agents, switches, org chart, integrations, reports", "CEO (2FA)", "CEO system", "/ceo", null),
        new("agent", "Agent API", "Agent identity, tasks, runs and cost", "Agent key", "n8n, agents", "/agent", "api.agents"),
        new("webhooks", "Webhooks", "n8n subscriptions and deliveries", "CEO", "n8n", "/webhooks", "api.webhooks"),
    ];

    /// <summary>The group of a request path such as <c>/api/v1/admin/customers</c> → <c>admin</c>.</summary>
    public static string GroupOf(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var segment = parts.Length >= 3 && parts[0] == "api" ? parts[2] : parts.FirstOrDefault() ?? "";
        return Groups.FirstOrDefault(g => g.Path == "/" + segment)?.Group ?? segment;
    }
}
