# CosmosDB Always Encrypted for Protocol Credentials

**Feature ID**: 002-cosmos-always-encrypted  
**Service**: FileProcessing (and all services with CosmosDB)  
**Status**: In Design  
**Last Updated**: 2025-01-07

---

## Problem Statement

The RiskInsure platform stores sensitive credentials (FTP passwords, HTTPS tokens, Azure Blob connection strings) in CosmosDB as unencrypted strings. These credentials are security-sensitive and should be encrypted at rest using the Azure CosmosDB Always Encrypted feature to meet compliance and security requirements.

### Current State
- Credentials stored as plaintext in CosmosDB documents
- Three sensitive properties identified:
  - `PasswordKeyVaultSecret` in FTP protocol settings
  - `PasswordOrTokenKeyVaultSecret` in HTTPS protocol settings
  - `ConnectionStringKeyVaultSecret` in Azure Blob protocol settings
- Properties have confusing names (KeyVaultSecret suffix implies different storage mechanism)
- No field-level encryption at CosmosDB layer

### Desired State
- Sensitive credentials encrypted at rest using CosmosDB Always Encrypted
- Clear, semantically correct property names (Password, PasswordOrToken, ConnectionString)
- Encryption key managed securely via Azure Key Vault
- Encryption transparent to business logic
- All services using CosmosDB with protocol configs updated consistently

---

## Goals

1. **Encrypt sensitive credentials** at rest using CosmosDB Always Encrypted feature
2. **Rename properties** to remove confusing 'KeyVaultSecret' suffix for cleaner domain model
3. **Apply consistently** across all services that use CosmosDB with protocol configurations
4. **Manage encryption keys** via Azure Key Vault for secure key lifecycle management
5. **Maintain backward compatibility** where possible during transition

---

## Scope

### In Scope

**CosmosDB Configuration**
- Enable CosmosDB Always Encrypted feature at service/container level
- Define encryption policy specifying:
  - Three sensitive property paths
  - Deterministic encryption with AEAD_AES_256_CBC_HMAC_SHA256 algorithm
  - Azure Key Vault as key management system
- Configure Data Encryption Key (DEK) in Azure Key Vault
- Use Managed Identity for Key Vault authentication

**Domain Model Updates**
- Update `FtpProtocolSettings` value object:
  - Rename `PasswordKeyVaultSecret` → `Password`
  - Mark with `[Encrypted]` attribute
  - Type: non-nullable string
- Update `HttpsProtocolSettings` value object:
  - Rename `PasswordOrTokenKeyVaultSecret` → `PasswordOrToken`
  - Mark with `[Encrypted]` attribute
  - Type: nullable string (optional)
- Update `AzureBlobProtocolSettings` value object:
  - Rename `ConnectionStringKeyVaultSecret` → `ConnectionString`
  - Mark with `[Encrypted]` attribute
  - Type: nullable string (optional, required for ConnectionString auth type)

**Infrastructure Updates**
- Centralize CosmosDB encryption policy configuration
- Configure Key Vault client integration
- Apply encryption policy to all document containers
- Inject Key Vault references into CosmosDB client builder

**Consumer Updates**
- Update all domain managers accessing these properties
- Update all message handlers referencing old property names
- Update integration test fixtures and test data builders
- Update validation logic if applicable

**Testing**
- Unit tests for property renaming and value object creation
- Integration tests verifying encryption/decryption works
- Key rotation scenario testing

### Out of Scope

- **Data Migration**: No existing production data to migrate (feature stores new credentials only)
- **Encryption Scope**: Limited to three identified properties only
- **Schema Changes**: No changes to NServiceBus message contracts or event schemas
- **Application-Layer Encryption**: Rely entirely on CosmosDB Always Encrypted
- **Backward Compatibility**: No support for old property names; consumers must be updated
- **Performance Optimization**: Accept standard encryption/decryption overhead; optimize only if profiling shows critical issues

---

## Functional Requirements

### FR1: CosmosDB Encryption Policy Configuration

**Description**: Configure CosmosDB to encrypt specific credential properties at the database layer.

**Requirements**:
- System creates encryption policy defining encrypted properties and algorithms
- Encryption policy specifies three paths: `protocolSettings.password`, `protocolSettings.passwordOrToken`, `protocolSettings.connectionString`
- Use deterministic encryption algorithm (AEAD_AES_256_CBC_HMAC_SHA256)
- Policy applied at container initialization time
- All three properties encrypted transparently without application intervention

**Acceptance Criteria**:
- [x] Encryption policy configuration code exists and is testable
- [x] Policy includes all three credential properties
- [x] Policy uses deterministic encryption
- [x] CosmosDB client applies policy on container creation
- [x] Integration tests verify encrypted properties have ciphertext in database
- [x] Integration tests verify unencrypted properties remain plaintext

### FR2: Property Renaming

**Description**: Rename credential properties to remove confusing 'KeyVaultSecret' suffix.

**Requirements**:
- `FtpProtocolSettings.PasswordKeyVaultSecret` renamed to `FtpProtocolSettings.Password`
- `HttpsProtocolSettings.PasswordOrTokenKeyVaultSecret` renamed to `HttpsProtocolSettings.PasswordOrToken`
- `AzureBlobProtocolSettings.ConnectionStringKeyVaultSecret` renamed to `AzureBlobProtocolSettings.ConnectionString`
- New names used consistently throughout codebase
- Old property names no longer accessible
- Property types unchanged: string (FTP), string? (HTTPS), string? (Azure Blob)

**Acceptance Criteria**:
- [x] All three value object classes have new property names
- [x] Old property names removed completely
- [x] No compilation errors in codebase
- [x] All consumers of these properties updated to new names
- [x] Value object factories/builders updated
- [x] Domain model documentation updated

### FR3: Azure Key Vault Integration

**Description**: Configure secure encryption key management via Azure Key Vault.

**Requirements**:
- Data Encryption Key (DEK) stored in Azure Key Vault
- CosmosDB client configured with Key Vault reference
- Managed Identity used for Key Vault authentication (no credentials in code)
- Key Vault URI and key name configurable via environment
- Key rotation supported through Key Vault versioning

**Acceptance Criteria**:
- [x] Azure Key Vault client initialization code exists
- [x] Managed Identity authentication configured
- [x] Key Vault reference injected into CosmosDB client configuration
- [x] Key names and URIs configurable via configuration
- [x] Integration tests verify key can be accessed via Managed Identity
- [x] Documentation explains key rotation process

### FR4: Consumer Updates

**Description**: Update all code consuming the renamed properties.

**Requirements**:
- All domain managers referencing old property names updated
- All message handlers accessing renamed properties updated
- All integration test fixtures and test data builders updated
- All validation logic updated
- All configuration deserializers updated
- No remaining references to old property names in codebase

**Acceptance Criteria**:
- [x] Codebase search finds zero references to old property names
- [x] All protocol settings consumers compile and run successfully
- [x] Integration tests for all consumers pass
- [x] Test data generators use new property names
- [x] API documentation (if applicable) updated

### FR5: Encryption Transparency

**Description**: Encryption/decryption handled transparently by CosmosDB SDK.

**Requirements**:
- Business logic unchanged; reads return decrypted values automatically
- Writes automatically encrypted by CosmosDB client
- Encryption/decryption failures bubble up as exceptions
- No manual encryption/decryption code in business logic
- Encryption visible in CosmosDB containers (binary ciphertext in documents)

**Acceptance Criteria**:
- [x] Reading decrypted values works without explicit decryption calls
- [x] Writing encrypted values works without explicit encryption calls
- [x] Integration tests verify values are encrypted in database
- [x] Integration tests verify values are decrypted on retrieval
- [x] No encryption/decryption methods in domain code

---

## User Scenarios & Testing

### Scenario 1: Creating Protocol Settings with Encrypted Credentials
**Actor**: Developer/Integration Test  
**Flow**:
1. Create FtpProtocolSettings with new `Password` property
2. Create HttpsProtocolSettings with new `PasswordOrToken` property
3. Save to CosmosDB
4. Verify credentials are encrypted in database

**Testable Outcomes**:
- Password values stored as encrypted ciphertext in CosmosDB
- Unencrypted properties stored normally
- Property values round-trip correctly (write then read returns original value)

### Scenario 2: Retrieving and Using Protocol Settings
**Actor**: Protocol Handler/Domain Manager  
**Flow**:
1. Query CosmosDB for protocol settings document
2. Access `Password` property (automatic decryption)
3. Use credentials to establish connection
4. Verify connection succeeds with decrypted credential

**Testable Outcomes**:
- Retrieved password is decrypted automatically
- Connection uses correct plaintext credentials
- No errors from encryption/decryption layer

### Scenario 3: Updating Encrypted Credentials
**Actor**: Protocol Configuration Manager  
**Flow**:
1. Retrieve existing protocol settings
2. Update `Password` property with new credential
3. Save changes
4. Verify new credential is encrypted in database
5. Verify new credential works for connections

**Testable Outcomes**:
- Old credential replaced with new encrypted value
- Updated value verified in database
- New credential functions correctly

### Scenario 4: Key Rotation in Azure Key Vault
**Actor**: DevOps/Security Team  
**Flow**:
1. Rotate Data Encryption Key version in Key Vault
2. Existing encrypted documents still accessible (old key version)
3. New documents encrypted with new key version
4. CosmosDB client handles key versions transparently

**Testable Outcomes**:
- Old encrypted documents decrypt correctly after key rotation
- New documents encrypt with new key version
- No data loss during rotation

---

## Technical Approach

### 1. CosmosDB Encryption Policy Configuration

**File**: `Infrastructure/CosmosPersistenceConfiguration.cs` (or new `Encryption/CosmosEncryptionPolicyBuilder.cs`)

**Implementation**:
```csharp
public class CosmosEncryptionPolicyBuilder
{
    private const string EncryptionAlgorithm = "AEAD_AES_256_CBC_HMAC_SHA256";
    
    public static EncryptionPolicy BuildPolicy(string keyVaultUri, string keyName, string keyVersion)
    {
        var policy = new EncryptionPolicy
        {
            PathsToEncrypt = new[]
            {
                "/protocolSettings/password",           // FTP
                "/protocolSettings/passwordOrToken",    // HTTPS
                "/protocolSettings/connectionString"    // Azure Blob
            },
            EncryptionType = "Deterministic",
            EncryptionAlgorithm = EncryptionAlgorithm,
            KeyManagement = new KeyManagementConfig
            {
                KeyVaultUri = keyVaultUri,
                KeyName = keyName,
                KeyVersion = keyVersion
            }
        };
        
        return policy;
    }
}
```

**Configuration in Startup**:
- Read Key Vault URI and key name from configuration
- Pass to CosmosDB client builder
- Apply policy during container initialization

### 2. Domain Model Changes

#### FtpProtocolSettings
```csharp
public record FtpProtocolSettings(
    string Host,
    int Port,
    string Username,
    string Password,  // Renamed from PasswordKeyVaultSecret
    string? FolderPath = null
)
{
    // Validation: Password must be non-empty
    // Mark: [Encrypted] attribute for documentation
}
```

#### HttpsProtocolSettings
```csharp
public record HttpsProtocolSettings(
    string Endpoint,
    string? PasswordOrToken = null,  // Renamed from PasswordOrTokenKeyVaultSecret
    int? TimeoutMs = null
)
{
    // Validation: Max 200 character if provided
    // Mark: [Encrypted] attribute
}
```

#### AzureBlobProtocolSettings
```csharp
public record AzureBlobProtocolSettings(
    string ContainerUri,
    AuthenticationType AuthType,
    string? ConnectionString = null,  // Renamed from ConnectionStringKeyVaultSecret
    string? ClientId = null
)
{
    // Validation: Required if AuthType is ConnectionString
    // Mark: [Encrypted] attribute
}
```

### 3. Infrastructure Updates

**CosmosDB Client Configuration**:
```csharp
var keyVaultUri = configuration["AzureKeyVault:VaultUri"];
var keyName = configuration["CosmosEncryption:KeyName"];

var encryptionPolicy = CosmosEncryptionPolicyBuilder.BuildPolicy(
    keyVaultUri, 
    keyName, 
    keyVersion
);

// Apply during CosmosDB container creation
var containerProperties = new ContainerProperties
{
    Id = "protocols",
    PartitionKeyPath = "/partitionKey",
    EncryptionPolicy = encryptionPolicy
};
```

### 4. Key Vault Integration

**Setup**:
- Ensure Data Encryption Key (DEK) exists in Azure Key Vault
- Configure Managed Identity with permissions to read keys from Key Vault
- Provide Key Vault URI and key name via configuration

**Authentication**:
```csharp
// Use DefaultAzureCredential for Managed Identity
var credential = new DefaultAzureCredential();
var keyVaultClient = new KeyClient(new Uri(keyVaultUri), credential);
```

### 5. Consumer Updates - Key Areas

**Domain Managers**:
- Update all property access from `PasswordKeyVaultSecret` → `Password`
- Update from `PasswordOrTokenKeyVaultSecret` → `PasswordOrToken`
- Update from `ConnectionStringKeyVaultSecret` → `ConnectionString`

**Message Handlers**:
- Update any handlers processing protocol settings events
- Use new property names in deserialization/mapping code

**Test Fixtures**:
- Update test data builders to use new property names
- Update integration test setup code
- Update mock/fake implementations

### 6. Testing Strategy

**Unit Tests**:
- Value object creation with new property names
- Validation rules for each property
- Property immutability (if using records)

**Integration Tests**:
```csharp
[Test]
public async Task EncryptedProperties_StoredAsCiphertext_InDatabase()
{
    // Arrange
    var settings = new FtpProtocolSettings("host", 21, "user", "mypassword");
    
    // Act
    await cosmosContainer.CreateItemAsync(settings);
    
    // Assert
    var rawDocument = await cosmosContainer.GetRawDocumentAsync(settings.Id);
    Assert.That(rawDocument["protocolSettings"]["password"], Is.Not.EqualTo("mypassword"));
    Assert.That(rawDocument["protocolSettings"]["password"], Is.StringContaining("encrypted"));
}

[Test]
public async Task EncryptedProperties_DecryptedOnRetrieval()
{
    // Arrange
    var originalPassword = "mypassword";
    var settings = new FtpProtocolSettings("host", 21, "user", originalPassword);
    await cosmosContainer.CreateItemAsync(settings);
    
    // Act
    var retrieved = await cosmosContainer.ReadItemAsync<FtpProtocolSettings>(settings.Id);
    
    // Assert
    Assert.That(retrieved.Password, Is.EqualTo(originalPassword));
}
```

**Key Rotation Testing**:
- Create document with key version 1
- Rotate to key version 2
- Verify old document still decrypts
- Verify new documents encrypt with version 2

---

## Data Model

### Encrypted Properties

| Property | Parent Object | Type | Encryption | Nullable | Default | Validation |
|----------|---------------|------|-----------|----------|---------|-----------|
| `password` | `FtpProtocolSettings` | `string` | ✅ Encrypted | No | Required | Non-empty |
| `passwordOrToken` | `HttpsProtocolSettings` | `string?` | ✅ Encrypted | Yes | null | Max 200 chars |
| `connectionString` | `AzureBlobProtocolSettings` | `string?` | ✅ Encrypted | Yes | null | Required if AuthType = ConnectionString |

### Unencrypted Properties (No Change)

All other properties in protocol settings remain unencrypted:
- FtpProtocolSettings: Host, Port, Username, FolderPath
- HttpsProtocolSettings: Endpoint, TimeoutMs
- AzureBlobProtocolSettings: ContainerUri, AuthType, ClientId

---

## Assumptions

- **SDK Support**: CosmosDB SDK 3.x+ supports Always Encrypted feature
- **Key Vault**: Azure Key Vault already provisioned in all environments (dev, staging, production)
- **Identity**: Managed Identity is configured with permissions to read keys from Key Vault
- **NServiceBus**: Services using NServiceBus 7.x+ with compatible Cosmos persistence packages
- **No Data Migration**: Feature adds support for new credentials only; no existing data encrypted
- **Development Environment**: Local development supports Azure Key Vault via DefaultAzureCredential and user authentication

---

## Success Criteria

1. ✅ **Encryption Implemented**: Protocol credentials encrypted in CosmosDB using Always Encrypted
2. ✅ **Properties Renamed**: All three properties use clean names without 'KeyVaultSecret' suffix
3. ✅ **Code Updated**: All consumers of old property names updated to new names
4. ✅ **Tests Passing**: Integration tests verify encryption/decryption works correctly
5. ✅ **Key Management**: Azure Key Vault integration functional for key lifecycle management
6. ✅ **No Breaking Changes**: NServiceBus message contracts unchanged
7. ✅ **Documentation**: Technical approach and setup instructions documented

---

## Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|-----------|
| Performance degradation from encryption/decryption | Medium | Medium | Profile with integration tests; benchmark before/after; optimize if needed |
| Key Vault access failures blocking operations | Low | High | Implement retry logic with exponential backoff; add monitoring/alerts for auth failures |
| Incomplete property renaming causing runtime errors | Low | High | Run comprehensive codebase search before completion; use static analysis tools; compile-time validation |
| Encryption policy not applied consistently across services | Medium | High | Centralize policy configuration; use shared NuGet package if applicable; document in shared docs |
| Key rotation issues causing decryption failures | Low | High | Test key rotation scenario in integration tests; document rotation procedure; maintain key versions for rollback |
| Azure dependencies unavailable in development | Medium | Medium | Document local development setup; provide test Key Vault or mock for offline development |

---

## References & Links

- [Azure CosmosDB Always Encrypted](https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/how-to-setup-always-encrypted)
- [Azure Key Vault Client Library](https://learn.microsoft.com/en-us/dotnet/api/azure.security.keyvault.keys)
- [CosmosDB SDK Documentation](https://github.com/Azure/azure-cosmos-dotnet-v3)
- [Managed Identity Documentation](https://learn.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/overview)

---

## Specification Metadata

| Field | Value |
|-------|-------|
| **Feature ID** | 002-cosmos-always-encrypted |
| **Service** | FileProcessing (and dependent services) |
| **Priority** | High (Security) |
| **Complexity** | Medium |
| **Effort Estimate** | 5-8 story points |
| **Status** | In Design |
| **Created** | 2025-01-07 |
| **Last Modified** | 2025-01-07 |
