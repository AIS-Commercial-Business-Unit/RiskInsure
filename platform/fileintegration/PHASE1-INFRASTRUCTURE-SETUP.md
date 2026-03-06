# Phase 1: Infrastructure & Encryption Setup - Implementation Guide

## Overview
Phase 1 establishes the foundational infrastructure for CosmosDB Always Encrypted. This includes:
1. Azure Key Vault integration for encryption key management
2. CosmosDB encryption policy configuration
3. Dependency injection setup

## Completed Tasks

### Task 1: Configure Azure Key Vault Integration ✅
**File**: `src/FileRetrieval.Infrastructure/Cosmos/CosmosEncryptionConfiguration.cs`

**What was implemented**:
- `CosmosEncryptionConfiguration` class that manages encryption setup
- `InitializeEncryptionAsync()` method that:
  - Validates Key Vault configuration
  - Creates `KeyClient` for Key Vault communication using `DefaultAzureCredential`
  - Initializes `CosmosDataEncryptionKeyProvider` for key management
  - Includes comprehensive error logging
- `BuildEncryptionPolicy()` method that:
  - Defines encryption paths for three sensitive properties
  - Applies deterministic encryption with AEAD_AES_256_CBC_HMAC_SHA256
  - Maps to the properties we'll rename in Phase 2

**Configuration Requirements**:
Add to appsettings.json or appsettings.{Environment}.json:
```json
{
  "CosmosDb": {
    "ConnectionString": "AccountEndpoint=...",
    "DatabaseName": "RiskInsure",
    "FileRetrievalConfigsEncryptionKeyName": "file-retrieval-dek"
  },
  "AzureKeyVault": {
    "VaultUri": "https://<vault-name>.vault.azure.net"
  }
}
```

**Key Points**:
- Uses `DefaultAzureCredential` for authentication (supports managed identity, environment variables, etc.)
- Encryption policy defines deterministic encryption for protocol properties
- Key Vault URI points to specific key version for audit and rotation tracking

---

### Task 2: Enable CosmosDB Always Encrypted Infrastructure Configuration ✅
**Files Updated**:
- `src/FileRetrieval.Infrastructure/Cosmos/CosmosDbContext.cs` (major updates)
- `src/FileRetrieval.Infrastructure/DependencyInjection.cs` (added service registration)

**What was implemented**:

#### CosmosDbContext Updates:
1. **Dependency Injection**: Added `CosmosEncryptionConfiguration` parameter
2. **Initialization Flow**:
   - `InitializeAsync()` now calls `InitializeEncryptionAsync()` during startup
   - Applies encryption policy to configurations container after containers are created
   - Added `ApplyEncryptionPolicyAsync()` method that:
     - Only applies encryption to the configurations container (contains protocol settings)
     - Checks if policy already exists before applying (idempotent)
     - Provides detailed logging of policy application status

3. **Key Design Decisions**:
   - Encryption policy is applied only to `file-retrieval-configurations` container (where protocol settings are stored)
   - Other containers are left unencrypted (only configurations contain sensitive data)
   - Policy application is idempotent (safe to call multiple times)

#### DependencyInjection Updates:
```csharp
services.AddSingleton<CosmosEncryptionConfiguration>();
```
- Registered `CosmosEncryptionConfiguration` as a singleton in the DI container
- Ensures single instance of encryption configuration across application lifetime
- Available for injection into CosmosDbContext and other services

---

## Configuration Setup Steps

### Prerequisites
1. **Azure Key Vault**: Must be provisioned in your Azure subscription
2. **Create Data Encryption Key (DEK)**:
   - Go to Azure Key Vault → Keys → Create/Import
   - Create or import an RSA key (recommended: 3072 or 4096 bit)
  - Note the key name and key URI for operations and audit purposes
  - Key name is required in configuration (`CosmosDb:FileRetrievalConfigsEncryptionKeyName`)

3. **Managed Identity**: Service should have Key Vault access
   - If running locally: Use Azure CLI authentication (`az login`)
   - If in Azure: Assign Managed Identity with Key Vault access
   - Ensure permissions: `Get`, `Unwrap Key`, `Wrap Key`

### Local Development Setup
1. Install Azure CLI: `choco install azure-cli`
2. Login: `az login`
3. Update `appsettings.Development.json`:
```json
{
  "CosmosDb": {
    "ConnectionString": "...",
    "DatabaseName": "RiskInsure",
    "FileRetrievalConfigsEncryptionKeyName": "file-retrieval-dek"
  }
}
```

### Azure Environment Setup
1. Create Key Vault (if not exists)
2. Create/import DEK
3. Assign Managed Identity permissions:
```bash
az keyvault set-policy --name <vault> \
  --object-id <managed-identity-object-id> \
  --key-permissions get unwrapKey wrapKey
```

---

## Testing Phase 1

### Compilation Test
```bash
cd platform/fileintegration
dotnet build
```
Expected: No compilation errors

### Runtime Test (Integration Tests - Phase 4)
Phase 1 configuration is validated in Phase 4 integration tests:
- Verify encryption policy is applied to CosmosDB container
- Confirm Key Vault connectivity
- Test encryption/decryption of sample data

---

## Next Steps (Phase 2)

Phase 2 focuses on domain model changes:
1. Rename `FtpProtocolSettings.PasswordKeyVaultSecret` → `Password`
2. Rename `HttpsProtocolSettings.PasswordOrTokenKeyVaultSecret` → `PasswordOrToken`
3. Rename `AzureBlobProtocolSettings.ConnectionStringKeyVaultSecret` → `ConnectionString`

These renamed properties will be automatically encrypted by the policy defined in Phase 1.

---

## Troubleshooting

### Configuration Missing Error
**Error**: "CosmosDb:FileRetrievalConfigsEncryptionKeyName configuration is required"
**Solution**: Add configuration key to appsettings

### Key Vault Authentication Failed
**Error**: "DefaultAzureCredential failed to obtain a token"
**Solution**: 
- Run `az login` for local development
- Verify Managed Identity permissions in Azure
- Check Key Vault firewall rules

### Encryption Policy Application Failed
**Error**: "Failed to apply encryption policy to container"
**Possible causes**:
- Container doesn't exist yet (wait for initialization)
- Permissions issue with Cosmos DB
- Invalid encryption policy format
**Solution**: Check logs for detailed error message

---

## Files Modified/Created

### Created
- ✅ `src/FileRetrieval.Infrastructure/Cosmos/CosmosEncryptionConfiguration.cs` (141 lines)
- ✅ `appsettings.encryption.example.json` (example configuration)

### Modified
- ✅ `src/FileRetrieval.Infrastructure/Cosmos/CosmosDbContext.cs` (added encryption initialization)
- ✅ `src/FileRetrieval.Infrastructure/DependencyInjection.cs` (added service registration)

## Phase 1 Completion Checklist

- [x] CosmosEncryptionConfiguration class created
- [x] Key Vault integration configured
- [x] Encryption policy builder implemented
- [x] CosmosDbContext updated to apply policies
- [x] DependencyInjection setup updated
- [x] Configuration example provided
- [x] Compilation succeeds
- [ ] Integration tests verify encryption works (Phase 4)
- [ ] Configuration documented for team
- [ ] Deployment guides updated (Phase 5)

