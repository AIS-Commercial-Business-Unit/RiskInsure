using RiskInsure.FileRetrieval.Domain.Enums;
using RiskInsure.FileRetrieval.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Azure.Security.KeyVault.Secrets;

namespace FileRetrieval.Application.Protocols;

/// <summary>
/// Factory for creating protocol adapters based on configuration.
/// Resolves the appropriate adapter implementation for FTP, HTTPS, or Azure Blob protocols.
/// </summary>
public class ProtocolAdapterFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SecretClient _keyVaultClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;

    public ProtocolAdapterFactory(
        IServiceProvider serviceProvider,
        SecretClient keyVaultClient,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _keyVaultClient = keyVaultClient ?? throw new ArgumentNullException(nameof(keyVaultClient));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <summary>
    /// Creates a protocol adapter for the specified protocol type and settings.
    /// </summary>
    /// <param name="protocolType">Type of protocol (FTP, HTTPS, AzureBlob)</param>
    /// <param name="protocolSettings">Protocol-specific connection settings</param>
    /// <returns>Protocol adapter instance configured for the specified protocol</returns>
    /// <exception cref="ArgumentException">If protocol type is not supported</exception>
    /// <exception cref="InvalidOperationException">If adapter cannot be created</exception>
    public IProtocolAdapter CreateAdapter(ProtocolType protocolType, ProtocolSettings protocolSettings)
    {
        ArgumentNullException.ThrowIfNull(protocolSettings);

        return protocolType switch
        {
            ProtocolType.FTP => CreateFtpAdapter((FtpProtocolSettings)protocolSettings),
            ProtocolType.HTTPS => CreateHttpsAdapter((HttpsProtocolSettings)protocolSettings),
            ProtocolType.AzureBlob => CreateAzureBlobAdapter((AzureBlobProtocolSettings)protocolSettings),
            _ => throw new ArgumentException($"Unsupported protocol type: {protocolType}", nameof(protocolType))
        };
    }

    private IProtocolAdapter CreateFtpAdapter(FtpProtocolSettings settings)
    {
        var logger = _loggerFactory.CreateLogger<FtpProtocolAdapter>();
        return new FtpProtocolAdapter(settings, _keyVaultClient, logger);
    }

    private IProtocolAdapter CreateHttpsAdapter(HttpsProtocolSettings settings)
    {
        var logger = _loggerFactory.CreateLogger<HttpsProtocolAdapter>();
        return new HttpsProtocolAdapter(settings, _keyVaultClient, _httpClientFactory, logger);
    }

    private IProtocolAdapter CreateAzureBlobAdapter(AzureBlobProtocolSettings settings)
    {
        var logger = _loggerFactory.CreateLogger<AzureBlobProtocolAdapter>();
        return new AzureBlobProtocolAdapter(settings, _keyVaultClient, logger);
    }
}
