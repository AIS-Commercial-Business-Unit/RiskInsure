using RiskInsure.FileRetrieval.Domain.Enums;
using System.Text.Json.Serialization;

namespace RiskInsure.FileRetrieval.Domain.ValueObjects;

/// <summary>
/// T024: FTP protocol-specific connection settings.
/// </summary>
public sealed class FtpProtocolSettings : ProtocolSettings
{
    public string Server { get; init; }
    public int Port { get; init; }
    public string Username { get; init; }
    public string PasswordKeyVaultSecret { get; init; }
    public bool UseTls { get; init; }
    public bool UsePassiveMode { get; init; }
    public TimeSpan ConnectionTimeout { get; init; }

    [JsonConstructor]
    public FtpProtocolSettings(
        string server,
        int port,
        string username,
        string passwordKeyVaultSecret,
        bool useTls = true,
        bool usePassiveMode = true,
        TimeSpan connectionTimeout = default)
        : base(ProtocolType.FTP)
    {
        if (string.IsNullOrWhiteSpace(server))
            throw new ArgumentException("Server cannot be empty", nameof(server));
        if (server.Length > 255)
            throw new ArgumentException("Server cannot exceed 255 characters", nameof(server));
        if (port < 1 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535");
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be empty", nameof(username));
        if (username.Length > 100)
            throw new ArgumentException("Username cannot exceed 100 characters", nameof(username));
        if (string.IsNullOrWhiteSpace(passwordKeyVaultSecret))
            throw new ArgumentException("PasswordKeyVaultSecret cannot be empty", nameof(passwordKeyVaultSecret));

        Server = server;
        Port = port;
        Username = username;
        PasswordKeyVaultSecret = passwordKeyVaultSecret;
        UseTls = useTls;
        UsePassiveMode = usePassiveMode;
        ConnectionTimeout = connectionTimeout == default ? TimeSpan.FromSeconds(30) : connectionTimeout;

        if (ConnectionTimeout <= TimeSpan.Zero)
            throw new ArgumentException("ConnectionTimeout must be positive", nameof(connectionTimeout));
    }
}
