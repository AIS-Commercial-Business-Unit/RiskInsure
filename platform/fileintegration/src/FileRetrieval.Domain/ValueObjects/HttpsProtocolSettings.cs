using RiskInsure.FileRetrieval.Domain.Enums;
using System.Text.Json.Serialization;

namespace RiskInsure.FileRetrieval.Domain.ValueObjects;

/// <summary>
/// T025: HTTPS protocol-specific connection settings.
/// </summary>
public sealed class HttpsProtocolSettings : ProtocolSettings
{
    public override ProtocolType ProtocolType => ProtocolType.HTTPS;

    public string BaseUrl { get; init; }
    public AuthType AuthenticationType { get; init; }
    public string? Username { get; init; }

    /// In order for this property to be encrypted, it has to reside
    /// at the root of the JSON being sent to Cosmos. The JsonPath 
    /// attribute causes this to be serialized/deserialized at a 
    /// specific location in the JSON document, using the provided path, 
    /// rather than as a nested property.
    [JsonPath(CosmosEncryptionConfiguration.HttpsSecretPath)]
    public string? PasswordOrTokenOrApiKey { get; init; }

    public TimeSpan ConnectionTimeout { get; init; }
    public bool FollowRedirects { get; init; }
    public int MaxRedirects { get; init; }

    [JsonConstructor]
    public HttpsProtocolSettings(
        string baseUrl,
        AuthType authenticationType,
        string? username = null,
        string? passwordOrTokenOrApiKey = null,
        TimeSpan connectionTimeout = default,
        bool followRedirects = true,
        int maxRedirects = 3)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("BaseUrl cannot be empty", nameof(baseUrl));
        if (!baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("BaseUrl must start with https://", nameof(baseUrl));
        if (baseUrl.Length > 500)
            throw new ArgumentException("BaseUrl cannot exceed 500 characters", nameof(baseUrl));
        if (username?.Length > 200)
            throw new ArgumentException("Username cannot exceed 200 characters", nameof(username));
        if (passwordOrTokenOrApiKey?.Length > 200)
            throw new ArgumentException("PasswordOrTokenOrApiKey cannot exceed 200 characters", nameof(passwordOrTokenOrApiKey));
        if (maxRedirects < 0 || maxRedirects > 10)
            throw new ArgumentOutOfRangeException(nameof(maxRedirects), "MaxRedirects must be between 0 and 10");

        BaseUrl = baseUrl;
        AuthenticationType = authenticationType;
        Username = username;
        PasswordOrTokenOrApiKey = passwordOrTokenOrApiKey;
        ConnectionTimeout = connectionTimeout == default ? TimeSpan.FromSeconds(30) : connectionTimeout;
        FollowRedirects = followRedirects;
        MaxRedirects = maxRedirects;

        if (ConnectionTimeout <= TimeSpan.Zero)
            throw new ArgumentException("ConnectionTimeout must be positive", nameof(connectionTimeout));
    }
}
