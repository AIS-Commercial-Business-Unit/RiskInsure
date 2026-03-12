namespace FileRetrieval.Contracts.DTOs;

/// <summary>
/// File-specific processing configuration.
/// </summary>
public class FileProcessingConfig
{
    public required string FileType { get; init; }
}