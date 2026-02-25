using RiskInsure.FileRetrieval.Domain.Enums;

namespace RiskInsure.FileRetrieval.Domain.ValueObjects;

/// <summary>
/// T025: HTTPS protocol-specific connection settings.
/// </summary>
public sealed class HttpsProtocolSettings : ProtocolSettings
{
    public string BaseUrl { get; init; }
    public AuthType AuthenticationType { get; init; }
    public string? UsernameOrApiKey { get; init; }
    public string? PasswordOrTokenKeyVaultSecret { get; init; }
    public TimeSpan ConnectionTimeout { get; init; }
    public bool FollowRedirects { get; init; }
    public int MaxRedirects { get; init; }

    public HttpsProtocolSettings(
        string baseUrl,
        AuthType authenticationType,
        string? usernameOrApiKey = null,
        string? passwordOrTokenKeyVaultSecret = null,
        TimeSpan? connectionTimeout = null,
        bool followRedirects = true,
        int maxRedirects = 3)
        : base(ProtocolType.HTTPS)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("BaseUrl cannot be empty", nameof(baseUrl));
        if (!baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("BaseUrl must start with https://", nameof(baseUrl));
        if (baseUrl.Length > 500)
            throw new ArgumentException("BaseUrl cannot exceed 500 characters", nameof(baseUrl));
        if (usernameOrApiKey?.Length > 200)
            throw new ArgumentException("UsernameOrApiKey cannot exceed 200 characters", nameof(usernameOrApiKey));
        if (passwordOrTokenKeyVaultSecret?.Length > 200)
            throw new ArgumentException("PasswordOrTokenKeyVaultSecret cannot exceed 200 characters", nameof(passwordOrTokenKeyVaultSecret));
        if (maxRedirects < 0 || maxRedirects > 10)
            throw new ArgumentOutOfRangeException(nameof(maxRedirects), "MaxRedirects must be between 0 and 10");

        BaseUrl = baseUrl;
        AuthenticationType = authenticationType;
        UsernameOrApiKey = usernameOrApiKey;
        PasswordOrTokenKeyVaultSecret = passwordOrTokenKeyVaultSecret;
        ConnectionTimeout = connectionTimeout ?? TimeSpan.FromSeconds(30);
        FollowRedirects = followRedirects;
        MaxRedirects = maxRedirects;

        if (ConnectionTimeout <= TimeSpan.Zero)
            throw new ArgumentException("ConnectionTimeout must be positive", nameof(connectionTimeout));
    }
}
