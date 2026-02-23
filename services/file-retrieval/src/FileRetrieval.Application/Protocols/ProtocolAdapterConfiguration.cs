namespace RiskInsure.FileRetrieval.Application.Protocols;

/// <summary>
/// T103: Configuration for protocol adapter behavior including timeouts and retry policies.
/// Allows protocol-specific tuning for connection timeouts, read timeouts, and retry strategies.
/// </summary>
public class ProtocolAdapterConfiguration
{
    /// <summary>
    /// Connection timeout for establishing initial connection
    /// </summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Read/write operation timeout
    /// </summary>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Maximum number of retry attempts for transient failures
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Initial delay before first retry
    /// </summary>
    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum delay between retries (for exponential backoff)
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Backoff multiplier for exponential backoff
    /// </summary>
    public double RetryBackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Whether to add jitter to retry delays to prevent thundering herd
    /// </summary>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    /// Creates default configuration for FTP protocol
    /// </summary>
    public static ProtocolAdapterConfiguration ForFtp()
    {
        return new ProtocolAdapterConfiguration
        {
            ConnectionTimeout = TimeSpan.FromSeconds(30),
            OperationTimeout = TimeSpan.FromSeconds(120), // FTP can be slower
            MaxRetryAttempts = 3,
            InitialRetryDelay = TimeSpan.FromSeconds(2),
            MaxRetryDelay = TimeSpan.FromSeconds(60),
            RetryBackoffMultiplier = 2.0,
            UseJitter = true
        };
    }

    /// <summary>
    /// Creates default configuration for HTTPS protocol
    /// </summary>
    public static ProtocolAdapterConfiguration ForHttps()
    {
        return new ProtocolAdapterConfiguration
        {
            ConnectionTimeout = TimeSpan.FromSeconds(30),
            OperationTimeout = TimeSpan.FromSeconds(90),
            MaxRetryAttempts = 3,
            InitialRetryDelay = TimeSpan.FromSeconds(1),
            MaxRetryDelay = TimeSpan.FromSeconds(30),
            RetryBackoffMultiplier = 2.0,
            UseJitter = true
        };
    }

    /// <summary>
    /// Creates default configuration for Azure Blob Storage protocol
    /// </summary>
    public static ProtocolAdapterConfiguration ForAzureBlob()
    {
        return new ProtocolAdapterConfiguration
        {
            ConnectionTimeout = TimeSpan.FromSeconds(30),
            OperationTimeout = TimeSpan.FromSeconds(60),
            MaxRetryAttempts = 3,
            InitialRetryDelay = TimeSpan.FromSeconds(0.5),
            MaxRetryDelay = TimeSpan.FromSeconds(20),
            RetryBackoffMultiplier = 2.0,
            UseJitter = true
        };
    }

    /// <summary>
    /// Calculates the delay for a given retry attempt using exponential backoff with optional jitter
    /// </summary>
    public TimeSpan CalculateRetryDelay(int attemptNumber)
    {
        if (attemptNumber < 1)
        {
            return TimeSpan.Zero;
        }

        // Calculate exponential backoff
        var delayMilliseconds = InitialRetryDelay.TotalMilliseconds * Math.Pow(RetryBackoffMultiplier, attemptNumber - 1);
        
        // Cap at max delay
        delayMilliseconds = Math.Min(delayMilliseconds, MaxRetryDelay.TotalMilliseconds);

        // Add jitter if enabled (random 0-20% variation)
        if (UseJitter)
        {
            var jitterFactor = 0.8 + (Random.Shared.NextDouble() * 0.4); // 0.8 to 1.2
            delayMilliseconds *= jitterFactor;
        }

        return TimeSpan.FromMilliseconds(delayMilliseconds);
    }
}

/// <summary>
/// T103: Configuration container for all protocol adapters
/// </summary>
public class ProtocolAdapterConfigurationSet
{
    public ProtocolAdapterConfiguration Ftp { get; set; } = ProtocolAdapterConfiguration.ForFtp();
    public ProtocolAdapterConfiguration Https { get; set; } = ProtocolAdapterConfiguration.ForHttps();
    public ProtocolAdapterConfiguration AzureBlob { get; set; } = ProtocolAdapterConfiguration.ForAzureBlob();

    /// <summary>
    /// Gets configuration for a specific protocol type
    /// </summary>
    public ProtocolAdapterConfiguration GetConfiguration(Domain.Enums.ProtocolType protocolType)
    {
        return protocolType switch
        {
            Domain.Enums.ProtocolType.FTP => Ftp,
            Domain.Enums.ProtocolType.HTTPS => Https,
            Domain.Enums.ProtocolType.AzureBlob => AzureBlob,
            _ => throw new ArgumentException($"Unknown protocol type: {protocolType}", nameof(protocolType))
        };
    }
}
