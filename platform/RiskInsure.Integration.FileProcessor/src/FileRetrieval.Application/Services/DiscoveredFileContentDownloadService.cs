using System.Net.Http.Headers;
using Azure.Identity;
using Azure.Storage.Blobs;
using FileRetrieval.Application.Protocols;
using FileRetrieval.Contracts.Commands;
using FluentFTP;
using Microsoft.Extensions.Logging;
using RiskInsure.FileRetrieval.Domain.Entities;
using RiskInsure.FileRetrieval.Domain.Enums;
using RiskInsure.FileRetrieval.Domain.ValueObjects;

namespace RiskInsure.FileRetrieval.Application.Services;

/// <summary>
/// Downloads discovered file content in-memory based on protocol configuration.
/// </summary>
public class DiscoveredFileContentDownloadService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DiscoveredFileContentDownloadService> _logger;

    public DiscoveredFileContentDownloadService(
        IHttpClientFactory httpClientFactory,
        ILogger<DiscoveredFileContentDownloadService> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<byte[]> DownloadToMemoryAsync(
        FileRetrievalConfiguration configuration,
        ProcessDiscoveredFile command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(command);

        return configuration.ProtocolSettings switch
        {
            FtpProtocolSettings ftpSettings => await DownloadFromFtpAsync(ftpSettings, command.FileUrl, cancellationToken),
            HttpsProtocolSettings httpsSettings => await DownloadFromHttpsAsync(httpsSettings, command.FileUrl, cancellationToken),
            AzureBlobProtocolSettings azureBlobSettings => await DownloadFromAzureBlobAsync(azureBlobSettings, command.FileUrl, cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported protocol settings type: {configuration.ProtocolSettings.GetType().Name}")
        };
    }

    private async Task<byte[]> DownloadFromFtpAsync(
        FtpProtocolSettings settings,
        string fileUrl,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Downloading FTP file content from {FileUrl}", fileUrl);

        var fileUri = new Uri(fileUrl, UriKind.Absolute);
        var remotePath = Uri.UnescapeDataString(fileUri.AbsolutePath);
        var password = settings.PasswordKeyVaultSecret;

        using var ftpClient = new AsyncFtpClient(
            settings.Server,
            settings.Username,
            password,
            settings.Port);

        ftpClient.Config.EncryptionMode = settings.UseTls
            ? FtpEncryptionMode.Explicit
            : FtpEncryptionMode.None;

        ftpClient.Config.DataConnectionType = settings.UsePassiveMode
            ? FtpDataConnectionType.AutoPassive
            : FtpDataConnectionType.AutoActive;

        ftpClient.Config.ConnectTimeout = (int)settings.ConnectionTimeout.TotalMilliseconds;
        ftpClient.Config.ReadTimeout = (int)settings.ConnectionTimeout.TotalMilliseconds;
        ftpClient.Config.DataConnectionConnectTimeout = (int)settings.ConnectionTimeout.TotalMilliseconds;
        ftpClient.Config.ValidateAnyCertificate = true;

        await ftpClient.Connect(cancellationToken);
        var bytes = await ftpClient.DownloadBytes(remotePath, token: cancellationToken);

        if (bytes == null || bytes.Length == 0)
        {
            throw new InvalidOperationException($"Downloaded FTP content was empty for {fileUrl}");
        }

        return bytes;
    }

    private async Task<byte[]> DownloadFromHttpsAsync(
        HttpsProtocolSettings settings,
        string fileUrl,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Downloading HTTPS file content from {FileUrl}", fileUrl);

        using var httpClient = _httpClientFactory.CreateClient("FileRetrievalHttpsDownload");
        httpClient.Timeout = settings.ConnectionTimeout;

        switch (settings.AuthenticationType)
        {
            case AuthType.UsernamePassword:
                if (!string.IsNullOrWhiteSpace(settings.UsernameOrApiKey) &&
                    !string.IsNullOrWhiteSpace(settings.PasswordOrTokenKeyVaultSecret))
                {
                    var credentials = Convert.ToBase64String(
                        System.Text.Encoding.ASCII.GetBytes($"{settings.UsernameOrApiKey}:{settings.PasswordOrTokenKeyVaultSecret}"));
                    httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Basic", credentials);
                }
                break;

            case AuthType.BearerToken:
                if (!string.IsNullOrWhiteSpace(settings.PasswordOrTokenKeyVaultSecret))
                {
                    httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", settings.PasswordOrTokenKeyVaultSecret);
                }
                break;

            case AuthType.ApiKey:
                if (!string.IsNullOrWhiteSpace(settings.UsernameOrApiKey))
                {
                    httpClient.DefaultRequestHeaders.Add("X-API-Key", settings.UsernameOrApiKey);
                }
                break;

            case AuthType.None:
            default:
                break;
        }

        using var response = await httpClient.GetAsync(fileUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private async Task<byte[]> DownloadFromAzureBlobAsync(
        AzureBlobProtocolSettings settings,
        string fileUrl,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Downloading Azure Blob content from {FileUrl}", fileUrl);

        var uriBuilder = new BlobUriBuilder(new Uri(fileUrl));
        string blobName = uriBuilder.BlobName;

        var containerClient = CreateBlobContainerClient(settings);
        var blobClient = containerClient.GetBlobClient(blobName);

        var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
        await using var contentStream = response.Value.Content;
        using var memoryStream = new MemoryStream();
        await contentStream.CopyToAsync(memoryStream, cancellationToken);
        return memoryStream.ToArray();
    }

    private static BlobContainerClient CreateBlobContainerClient(AzureBlobProtocolSettings settings)
    {
        return settings.AuthenticationType switch
        {
            AzureAuthType.ConnectionString => new BlobContainerClient(
                settings.ConnectionStringKeyVaultSecret ?? throw new InvalidOperationException("ConnectionStringKeyVaultSecret is required for ConnectionString authentication."),
                settings.ContainerName),

            AzureAuthType.SasToken => new BlobContainerClient(
                new Uri($"https://{settings.StorageAccountName}.blob.core.windows.net/{settings.ContainerName}?{(settings.SasTokenKeyVaultSecret ?? throw new InvalidOperationException("SasTokenKeyVaultSecret is required for SasToken authentication.")).TrimStart('?')}")
            ),

            AzureAuthType.ManagedIdentity or AzureAuthType.ServicePrincipal => new BlobContainerClient(
                new Uri($"https://{settings.StorageAccountName}.blob.core.windows.net/{settings.ContainerName}"),
                new DefaultAzureCredential()),

            _ => throw new InvalidOperationException($"Unsupported Azure authentication type: {settings.AuthenticationType}")
        };
    }
}
