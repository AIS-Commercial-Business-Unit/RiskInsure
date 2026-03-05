using RiskInsure.FileRetrieval.Domain.Enums;

namespace RiskInsure.FileRetrieval.Domain.ValueObjects;

/// <summary>
/// T023: Base abstract class for protocol-specific connection settings.
/// </summary>
public abstract class ProtocolSettings
{
    /// <summary>
    /// Protocol type (FTP, HTTPS, AzureBlob)
    /// </summary>
    public ProtocolType ProtocolType { get; protected init; }

    protected ProtocolSettings(ProtocolType protocolType)
    {
        ProtocolType = protocolType;
    }
}
