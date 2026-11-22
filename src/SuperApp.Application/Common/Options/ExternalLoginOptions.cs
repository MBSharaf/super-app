namespace SuperApp.Application.Common.Options;

/// <summary>
/// "Continue with Google / Facebook". Each provider is on only when its ids are set (in .env or the host's settings):
/// EXTERNALLOGINS__GOOGLE__CLIENTID, EXTERNALLOGINS__FACEBOOK__APPID and EXTERNALLOGINS__FACEBOOK__APPSECRET.
/// The Google client id and the Facebook app id are public; the Facebook app secret never leaves the server.
/// </summary>
public sealed class ExternalLoginOptions
{
    public const string Section = "ExternalLogins";

    public GoogleLogin Google { get; set; } = new();

    public FacebookLogin Facebook { get; set; } = new();

    public sealed class GoogleLogin
    {
        public string? ClientId { get; set; }

        public bool Enabled => !string.IsNullOrWhiteSpace(ClientId);
    }

    public sealed class FacebookLogin
    {
        public string? AppId { get; set; }

        public string? AppSecret { get; set; }

        public bool Enabled => !string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrWhiteSpace(AppSecret);
    }
}
