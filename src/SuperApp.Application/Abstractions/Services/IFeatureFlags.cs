namespace SuperApp.Application.Abstractions.Services;

/// <summary>The CEO's kill switches (ops.FeatureFlags). A switched-off area answers 423 with a friendly message.</summary>
public interface IFeatureFlags
{
    Task<bool> IsOnAsync(string key, CancellationToken ct);

    /// <summary>Throws FeatureDisabledException (423) when the switch is off.</summary>
    Task EnsureOnAsync(string key, CancellationToken ct);
}

/// <summary>Keys of the 9 kill switches seeded in ops.FeatureFlags.</summary>
public static class KillSwitches
{
    public const string Bookings = "kill.bookings";
    public const string Shop = "kill.shop";
    public const string Pod = "kill.pod";
    public const string Payouts = "kill.payouts";
    public const string Applications = "kill.applications";
    public const string Fantasy = "kill.fantasy";
    public const string Community = "kill.community";
    public const string Games = "kill.games";
    public const string Agents = "kill.agents";
}
