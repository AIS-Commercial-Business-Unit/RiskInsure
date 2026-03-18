using RiskInsure.FileProcessing.Domain.Enums;

namespace RiskInsure.FileProcessing.Domain.ValueObjects;

/// <summary>
/// T023: Base abstract class for protocol-specific connection settings.
/// </summary>
public abstract class ProtocolSettings
{
    /// <summary>
    /// Protocol type (FTP, HTTPS, AzureBlob)
    /// </summary>
    public abstract ProtocolType ProtocolType { get; }
}
