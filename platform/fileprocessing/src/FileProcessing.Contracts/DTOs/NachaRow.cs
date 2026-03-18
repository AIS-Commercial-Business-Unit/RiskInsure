namespace FileProcessing.Contracts.DTOs;

/// <summary>
/// Represents a single NACHA entry detail row (record type 6).
/// </summary>
public class NachaRow
{
    public int RowNumber { get; init; }
    public required string TransactionCode { get; init; }
    public required string RoutingNumber { get; init; }
    public required string AccountNumber { get; init; }
    public long AmountCents { get; init; }
    public string? IndividualId { get; init; }
    public string? IndividualName { get; init; }
    public required string TraceNumber { get; init; }
    public required string RawRecord { get; init; }
}