namespace RiskInsure.FileRetrieval.Domain.Enums;

/// <summary>
/// Authentication types for FTP/HTTPS protocols.
/// T021: AuthType enum
/// </summary>
public enum AuthType
{
    /// <summary>No authentication required</summary>
    None = 1,
    
    /// <summary>Username and password authentication</summary>
    UsernamePassword = 2,
    
    /// <summary>Certificate-based authentication</summary>
    Certificate = 3,
    
    /// <summary>Bearer token authentication (OAuth, JWT)</summary>
    BearerToken = 4,
    
    /// <summary>API key authentication</summary>
    ApiKey = 5
}
