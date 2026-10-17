namespace SuperApp.Application.Common.Options;

/// <summary>Where the site lives, for links inside emails (confirm email, reset password, invitations).</summary>
public sealed class SiteOptions
{
    public const string Section = "Site";

    /// <summary>Public address of the site (development: https://localhost:7254/super-app/).</summary>
    public string PublicUrl { get; set; } = "https://localhost:7254/super-app/";

    /// <summary>A site page address with query values, e.g. Link("auth/reset", ("u", id), ("t", token)).</summary>
    public string Link(string page, params (string Key, string Value)[] query) =>
        $"{PublicUrl.TrimEnd('/')}/#/{page}"
        + (query.Length == 0 ? "" : "?" + string.Join('&', query.Select(q => $"{q.Key}={Uri.EscapeDataString(q.Value)}")));
}
