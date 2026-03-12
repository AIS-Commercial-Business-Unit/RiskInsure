namespace RiskInsure.FileRetrieval.Domain.ValueObjects;

/// <summary>
/// Defines how a discovered file should be interpreted during processing.
/// </summary>
public class FileProcessingDefinition
{
    public required string FileType { get; set; }
}