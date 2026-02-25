namespace RiskInsure.FileRetrieval.Domain.Enums;

/// <summary>
/// Azure authentication types for Azure Blob Storage.
/// T022: AzureAuthType enum
/// </summary>
public enum AzureAuthType
{
    /// <summary>Connection string authentication</summary>
    ConnectionString = 1,
    
    /// <summary>Managed Identity (system or user-assigned)</summary>
    ManagedIdentity = 2,
    
    /// <summary>Service Principal (client ID + secret)</summary>
    ServicePrincipal = 3,
    
    /// <summary>Shared Access Signature (SAS) token</summary>
    SasToken = 4
}
