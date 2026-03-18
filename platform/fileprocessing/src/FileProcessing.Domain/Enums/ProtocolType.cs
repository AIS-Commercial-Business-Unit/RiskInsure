namespace RiskInsure.FileProcessing.Domain.Enums;

/// <summary>
/// Protocol types supported for file processing.
/// T018: ProtocolType enum
/// </summary>
public enum ProtocolType
{
    /// <summary>FTP (File Transfer Protocol)</summary>
    FTP = 1,
    
    /// <summary>HTTPS (HTTP Secure)</summary>
    HTTPS = 2,
    
    /// <summary>Azure Blob Storage</summary>
    AzureBlob = 3
}
