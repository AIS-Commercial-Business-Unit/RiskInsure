namespace RiskInsure.FileRetrieval.Domain.Entities;

/// <summary>
/// Persisted record for file processing outcomes (download + checksum metadata).
/// </summary>
public class ProcessedFileRecord
{
    public Guid Id { get; set; }
    public required string ClientId { get; set; }
    public Guid ConfigurationId { get; set; }
    public Guid ExecutionId { get; set; }
    public Guid DiscoveredFileId { get; set; }
    public required string FileUrl { get; set; }
    public required string Filename { get; set; }
    public required string Protocol { get; set; }
    public long DownloadedSizeBytes { get; set; }
    public required string ChecksumAlgorithm { get; set; }
    public required string ChecksumHex { get; set; }
    public required string CorrelationId { get; set; }
    public required string IdempotencyKey { get; set; }
    public required DateTimeOffset ProcessedAt { get; set; }
    public string Type { get; set; } = nameof(ProcessedFileRecord);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ClientId))
            throw new ArgumentException("ClientId must not be empty", nameof(ClientId));

        if (ConfigurationId == Guid.Empty)
            throw new ArgumentException("ConfigurationId must not be empty", nameof(ConfigurationId));

        if (ExecutionId == Guid.Empty)
            throw new ArgumentException("ExecutionId must not be empty", nameof(ExecutionId));

        if (DiscoveredFileId == Guid.Empty)
            throw new ArgumentException("DiscoveredFileId must not be empty", nameof(DiscoveredFileId));

        if (string.IsNullOrWhiteSpace(FileUrl))
            throw new ArgumentException("FileUrl must not be empty", nameof(FileUrl));

        if (string.IsNullOrWhiteSpace(Filename))
            throw new ArgumentException("Filename must not be empty", nameof(Filename));

        if (string.IsNullOrWhiteSpace(Protocol))
            throw new ArgumentException("Protocol must not be empty", nameof(Protocol));

        if (DownloadedSizeBytes < 0)
            throw new ArgumentException("DownloadedSizeBytes cannot be negative", nameof(DownloadedSizeBytes));

        if (string.IsNullOrWhiteSpace(ChecksumAlgorithm))
            throw new ArgumentException("ChecksumAlgorithm must not be empty", nameof(ChecksumAlgorithm));

        if (string.IsNullOrWhiteSpace(ChecksumHex))
            throw new ArgumentException("ChecksumHex must not be empty", nameof(ChecksumHex));

        if (string.IsNullOrWhiteSpace(CorrelationId))
            throw new ArgumentException("CorrelationId must not be empty", nameof(CorrelationId));

        if (string.IsNullOrWhiteSpace(IdempotencyKey))
            throw new ArgumentException("IdempotencyKey must not be empty", nameof(IdempotencyKey));
    }
}
