# Protocol Settings API Documentation

## Overview

This document describes the protocol-specific settings for file retrieval configurations. Each protocol type requires different connection parameters passed through the `protocolSettings` field as a dictionary of key-value pairs.

## FTP Protocol Settings

### Property Names (Renamed)

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Server` | string | Yes | FTP server hostname or IP address (max 255 chars) |
| `Port` | integer | Yes | FTP server port (1-65535, typically 21) |
| `Username` | string | Yes | FTP login username (max 100 chars) |
| `Password` | string | Yes | FTP login password (stored encrypted in CosmosDB via Always Encrypted) |
| `UseTls` | boolean | No | Enable TLS/SSL encryption (default: true) |
| `UsePassiveMode` | boolean | No | Use passive mode for data connections (default: true) |
| `ConnectionTimeoutSeconds` | integer | No | Connection timeout in seconds (default: 30) |

### Example Request

```json
{
  "name": "Daily FTP Reports",
  "description": "Retrieve daily reports from vendor FTP server",
  "protocol": "FTP",
  "protocolSettings": {
    "Server": "ftp.vendor.com",
    "Port": 21,
    "Username": "reports_user",
    "Password": "secure-password-or-reference",
    "UseTls": true,
    "UsePassiveMode": true,
    "ConnectionTimeoutSeconds": 30
  },
  "filePathPattern": "/reports/{yyyy}/{mm}/{dd}",
  "filenamePattern": "daily_report_*.xlsx",
  "fileExtension": "xlsx",
  "schedule": {
    "cronExpression": "0 2 * * *",
    "timezone": "America/New_York",
    "description": "Daily at 2 AM"
  }
}
```

### Security Notes

- **Password Storage**: The `Password` property is automatically encrypted at rest in CosmosDB using Always Encrypted.
- **Encryption**: Sensitive credentials are never logged or exposed in API responses (they appear as `[REDACTED]` in logs).
- **Transport**: Use HTTPS for all API calls to protect credentials in transit.

---

## HTTPS Protocol Settings

### Property Names (Renamed)

| Property | Type | Required | Condition | Description |
|----------|------|----------|-----------|-------------|
| `BaseUrl` | string | Yes | Always | HTTPS base URL (must start with https://, max 500 chars) |
| `AuthenticationType` | string | No | Always | Authentication type: `None`, `UsernamePassword`, `BearerToken`, `ApiKey` (default: None) |
| `UsernameOrApiKey` | string | No | UsernamePassword or ApiKey | Username for basic auth or API key value (max 200 chars) |
| `PasswordOrToken` | string | No | UsernamePassword or BearerToken | Password for basic auth or bearer token (max 200 chars, encrypted) |
| `ConnectionTimeoutSeconds` | integer | No | Always | Timeout in seconds (default: 30) |
| `FollowRedirects` | boolean | No | Always | Follow HTTP redirects (default: true) |
| `MaxRedirects` | integer | No | Always | Maximum redirects to follow (0-10, default: 3) |

### Example Requests

#### No Authentication
```json
{
  "protocol": "HTTPS",
  "protocolSettings": {
    "BaseUrl": "https://api.example.com",
    "AuthenticationType": "None",
    "ConnectionTimeoutSeconds": 30
  }
}
```

#### Basic Authentication (Username/Password)
```json
{
  "protocol": "HTTPS",
  "protocolSettings": {
    "BaseUrl": "https://api.example.com",
    "AuthenticationType": "UsernamePassword",
    "UsernameOrApiKey": "myusername",
    "PasswordOrToken": "mypassword",
    "ConnectionTimeoutSeconds": 30
  }
}
```

#### Bearer Token Authentication
```json
{
  "protocol": "HTTPS",
  "protocolSettings": {
    "BaseUrl": "https://api.example.com",
    "AuthenticationType": "BearerToken",
    "PasswordOrToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "ConnectionTimeoutSeconds": 30
  }
}
```

#### API Key Authentication
```json
{
  "protocol": "HTTPS",
  "protocolSettings": {
    "BaseUrl": "https://api.example.com",
    "AuthenticationType": "ApiKey",
    "UsernameOrApiKey": "sk-1234567890abcdef",
    "ConnectionTimeoutSeconds": 30
  }
}
```

### Security Notes

- **Token/Password Encryption**: The `PasswordOrToken` property is automatically encrypted at rest in CosmosDB.
- **Headers**: API Keys are sent in the `X-API-Key` header; usernames and passwords use HTTP Basic Authentication.
- **HTTPS Required**: All connections use HTTPS; connections to non-HTTPS URLs will be rejected.

---

## Azure Blob Storage Protocol Settings

### Property Names (Renamed)

| Property | Type | Required | Condition | Description |
|----------|------|----------|-----------|-------------|
| `StorageAccountName` | string | Yes | Always | Azure Storage account name (max 24 chars) |
| `ContainerName` | string | Yes | Always | Blob container name (max 63 chars, lowercase only) |
| `AuthenticationType` | string | Yes | Always | Auth type: `ManagedIdentity`, `ConnectionString`, `SasToken` |
| `ConnectionString` | string | No | ConnectionString | Connection string (required if AuthenticationType = ConnectionString, encrypted) |
| `SasToken` | string | No | SasToken | SAS token reference (required if AuthenticationType = SasToken) |
| `BlobPrefix` | string | No | Always | Optional prefix filter for blobs (max 1024 chars) |

### Example Requests

#### Managed Identity Authentication
```json
{
  "protocol": "AzureBlob",
  "protocolSettings": {
    "StorageAccountName": "mystorageacct",
    "ContainerName": "reports",
    "AuthenticationType": "ManagedIdentity",
    "BlobPrefix": "2024/reports/"
  }
}
```

#### Connection String Authentication
```json
{
  "protocol": "AzureBlob",
  "protocolSettings": {
    "StorageAccountName": "mystorageacct",
    "ContainerName": "reports",
    "AuthenticationType": "ConnectionString",
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=mystorageacct;AccountKey=...",
    "BlobPrefix": "2024/reports/"
  }
}
```

#### SAS Token Authentication
```json
{
  "protocol": "AzureBlob",
  "protocolSettings": {
    "StorageAccountName": "mystorageacct",
    "ContainerName": "reports",
    "AuthenticationType": "SasToken",
    "SasToken": "?sv=2021-06-08&ss=bfqt&srt=sco&sp=rwdlacupitfx&...",
    "BlobPrefix": "2024/reports/"
  }
}
```

### Security Notes

- **Connection String Encryption**: The `ConnectionString` property is automatically encrypted at rest in CosmosDB using Always Encrypted.
- **Managed Identity Recommended**: For Azure-hosted services, use Managed Identity authentication to avoid managing secrets.
- **SAS Token Rotation**: SAS tokens should include an expiration; rotate them regularly for security.

---

## API Response Example

When retrieving a configuration, protocol settings are returned with sensitive values redacted:

```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "protocol": "FTP",
  "protocolSettings": {
    "Server": "ftp.vendor.com",
    "Port": 21,
    "Username": "reports_user",
    "Password": "[REDACTED]",
    "UseTls": true,
    "UsePassiveMode": true,
    "ConnectionTimeoutSeconds": 30
  }
}
```

---

## Validation Rules

### FTP Settings
- Server: Required, non-empty, max 255 chars
- Port: Required, between 1-65535
- Username: Required, non-empty, max 100 chars
- Password: Required, non-empty
- ConnectionTimeout: Must be positive if provided

### HTTPS Settings
- BaseUrl: Required, must start with https://, max 500 chars
- PasswordOrToken: Max 200 chars if provided
- MaxRedirects: Must be between 0-10
- ConnectionTimeout: Must be positive if provided

### Azure Blob Settings
- StorageAccountName: Required, max 24 chars
- ContainerName: Required, max 63 chars, must be lowercase
- BlobPrefix: Max 1024 chars if provided
- Auth validation:
  - ConnectionString required when AuthenticationType = ConnectionString
  - SasToken required when AuthenticationType = SasToken

---

## Error Responses

### 400 Bad Request
```json
{
  "error": "FTP protocol requires: Server, Port, Username, Password"
}
```

### 401 Unauthorized
```json
{
  "error": "Invalid authentication credentials"
}
```

### 500 Internal Server Error
```json
{
  "error": "An error occurred while creating the configuration"
}
```

