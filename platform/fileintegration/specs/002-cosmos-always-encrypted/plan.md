# Implementation Plan: CosmosDB Always Encrypted for Protocol Credentials

**Feature ID**: 002-cosmos-always-encrypted  
**Document Type**: Implementation Plan  
**Status**: Ready for Execution  
**Last Updated**: 2025-01-07

---

## Executive Summary

This plan outlines the step-by-step approach to implement CosmosDB Always Encrypted encryption for sensitive protocol credentials (passwords and connection strings) across the FileIntegration service and all dependent services. The implementation involves infrastructure changes to enable encryption, domain model updates to rename properties, and comprehensive testing to ensure encryption works correctly.

**Key Phases**:
1. **Foundation** - Set up encryption infrastructure
2. **Domain Updates** - Rename properties and update value objects
3. **Integration** - Update all consumers across codebase
4. **Testing** - Comprehensive encryption validation
5. **Deployment** - Prepare for multi-service rollout

---

## Phase 1: Foundation & Infrastructure Setup

### Objectives
- Create centralized encryption policy configuration
- Set up Azure Key Vault integration
- Enable CosmosDB Always Encrypted at the infrastructure level
- Verify encryption pipeline works end-to-end

### Approach

#### Step 1.1: Create Encryption Policy Builder

**Purpose**: Centralize encryption policy configuration so all services apply encryption consistently.

**Deliverable**: New class `CosmosEncryptionPolicyBuilder`

**Location**: `Infrastructure/Encryption/CosmosEncryptionPolicyBuilder.cs`

**Key Responsibilities**:
- Define encrypted property paths (three credential properties)
- Specify encryption algorithm (AEAD_AES_256_CBC_HMAC_SHA256)
- Build EncryptionPolicy object compatible with CosmosDB SDK
- Accept Key Vault URI and key name as parameters

**Dependencies**: 
- CosmosDB SDK 3.x+
- Azure.Security.KeyVault.Keys NuGet package

**Acceptance Criteria**:
- Builder class compiles without errors
- Generated policy includes all three encrypted paths
- Policy specifies deterministic encryption type
- Unit tests verify builder creates valid policy objects
- Builder can be reused across all services

---

#### Step 1.2: Configure Key Vault Client

**Purpose**: Set up secure authentication to Azure Key Vault for key management.

**Deliverable**: Key Vault client configuration in `Infrastructure/Encryption/KeyVaultConfiguration.cs`

**Key Responsibilities**:
- Initialize Azure DefaultAzureCredential for Managed Identity auth
- Create KeyClient connected to Key Vault
- Read Key Vault URI and key name from configuration
- Handle key versioning for rotation scenarios
- Provide dependency injection bindings

**Configuration Settings Required**:
```
CosmosEncryption:KeyVaultUri = "https://myvault.vault.azure.net/"
CosmosEncryption:KeyName = "cosmos-dek"
CosmosEncryption:KeyVersion = "current" (or specific version)
```

**Acceptance Criteria**:
- Key Vault client initializes without hardcoded credentials
- Managed Identity authentication works in Azure environments
- Configuration supports multiple Key Vault URIs for different environments
- Key versioning properly handled for rotation
- Error handling logs Key Vault connection issues

---

#### Step 1.3: Apply Encryption Policy to CosmosDB Container

**Purpose**: Configure CosmosDB containers to use the encryption policy.

**Deliverable**: Updated `Infrastructure/CosmosPersistenceConfiguration.cs` with encryption policy application

**Key Responsibilities**:
- Integrate encryption policy builder into container initialization
- Apply policy during container creation/update
- Ensure policy applied to all protocol-related containers
- Document encryption configuration for other services

**Implementation Details**:
- Modify CosmosDB client builder to include encryption policy
- Pass policy to ContainerProperties during container creation
- Verify policy applied before container goes live
- Add configuration validation to ensure Key Vault is available

**Acceptance Criteria**:
- Encryption policy successfully applied to containers
- Integration test verifies policy in effect
- Container initialization fails gracefully if Key Vault unavailable
- Documentation explains policy application for multi-service rollout

---

#### Step 1.4: Integration Test for Encryption Pipeline

**Purpose**: Verify encryption works end-to-end before domain changes.

**Deliverable**: `Tests/Integration/Infrastructure/CosmosEncryptionPipelineTests.cs`

**Test Cases**:
1. **Policy Application Test**: Verify policy applied to container
2. **Encryption Transparency Test**: Write and read encrypted property, verify it's encrypted in DB but decrypted on read
3. **Multiple Encryption Test**: Verify all three properties encrypted simultaneously
4. **Unencrypted Properties Test**: Verify non-credential properties remain unencrypted
5. **Key Rotation Test**: Rotate key version and verify decryption still works

**Acceptance Criteria**:
- All 5 test cases pass
- Test data verifiable in CosmosDB Emulator/real Cosmos
- Tests validate ciphertext in database (not plaintext)
- Tests verify automatic decryption on retrieval

---

## Phase 2: Domain Model Updates

### Objectives
- Update three protocol settings value objects with new property names
- Ensure property names are semantically correct
- Maintain type safety and validation
- Document changes for consuming code

### Approach

#### Step 2.1: Update FtpProtocolSettings Value Object

**Purpose**: Rename `PasswordKeyVaultSecret` to `Password`

**File**: `Domain/ProtocolSettings/FtpProtocolSettings.cs`

**Changes**:
```csharp
// Before:
public record FtpProtocolSettings(
    string Host,
    int Port,
    string Username,
    string PasswordKeyVaultSecret,
    string? FolderPath = null
)

// After:
public record FtpProtocolSettings(
    string Host,
    int Port,
    string Username,
    string Password,
    string? FolderPath = null
)
```

**Validation**:
- Password must be non-empty string
- Existing validation rules apply (length limits, format if any)

**Breaking Changes**:
- Old `PasswordKeyVaultSecret` property removed
- All constructors require update
- Factory methods/builders require update

**Acceptance Criteria**:
- New property compiles with correct type
- Old property name no longer exists
- Validation tests pass for new property
- Builder methods updated
- Documentation updated

---

#### Step 2.2: Update HttpsProtocolSettings Value Object

**Purpose**: Rename `PasswordOrTokenKeyVaultSecret` to `PasswordOrToken`

**File**: `Domain/ProtocolSettings/HttpsProtocolSettings.cs`

**Changes**:
```csharp
// Before:
public record HttpsProtocolSettings(
    string Endpoint,
    string? PasswordOrTokenKeyVaultSecret = null,
    int? TimeoutMs = null
)

// After:
public record HttpsProtocolSettings(
    string Endpoint,
    string? PasswordOrToken = null,
    int? TimeoutMs = null
)
```

**Validation**:
- Max 200 characters if provided
- Nullable (optional)
- Existing validation rules apply

**Breaking Changes**:
- Old `PasswordOrTokenKeyVaultSecret` property removed
- All constructors require update

**Acceptance Criteria**:
- New property compiles with correct nullable type
- Old property name no longer exists
- 200-character limit validated
- Builder methods updated

---

#### Step 2.3: Update AzureBlobProtocolSettings Value Object

**Purpose**: Rename `ConnectionStringKeyVaultSecret` to `ConnectionString`

**File**: `Domain/ProtocolSettings/AzureBlobProtocolSettings.cs`

**Changes**:
```csharp
// Before:
public record AzureBlobProtocolSettings(
    string ContainerUri,
    AuthenticationType AuthType,
    string? ConnectionStringKeyVaultSecret = null,
    string? ClientId = null
)

// After:
public record AzureBlobProtocolSettings(
    string ContainerUri,
    AuthenticationType AuthType,
    string? ConnectionString = null,
    string? ClientId = null
)
```

**Validation**:
- Required if AuthType is ConnectionString
- Nullable (optional) for other auth types
- Existing validation rules apply

**Breaking Changes**:
- Old `ConnectionStringKeyVaultSecret` property removed
- Conditional validation must be updated

**Acceptance Criteria**:
- New property compiles with correct type
- Old property name no longer exists
- Conditional validation works correctly
- Builder methods updated

---

#### Step 2.4: Add [Encrypted] Attribute

**Purpose**: Document which properties are encrypted and require Key Vault management.

**Approach**: Create custom attribute if not exists

**File**: `Domain/Attributes/EncryptedAttribute.cs`

```csharp
[AttributeUsage(AttributeTargets.Property)]
public class EncryptedAttribute : Attribute
{
    public string? KeyVaultKeyName { get; set; }
    public string? EncryptionAlgorithm { get; set; }
}
```

**Apply to Properties**:
```csharp
[Encrypted(KeyVaultKeyName = "cosmos-dek")]
public string Password { get; set; }
```

**Acceptance Criteria**:
- Attribute compiles and applies cleanly
- All three credential properties marked
- Attribute helps document encryption requirements
- Optional fields for algorithm/key details

---

## Phase 3: Consumer Updates Across Codebase

### Objectives
- Update all code referencing old property names
- Ensure no stray references remain
- Update test fixtures and builders
- Comprehensive codebase validation

### Approach

#### Step 3.1: Search and Update Domain Managers

**Purpose**: Find all domain managers using protocol settings and update property references.

**Search Pattern**: `PasswordKeyVaultSecret|PasswordOrTokenKeyVaultSecret|ConnectionStringKeyVaultSecret`

**Expected Files** (FileIntegration example):
- `Domain/ProtocolHandlers/FtpProtocolHandler.cs` - Uses FTP Password
- `Domain/ProtocolHandlers/HttpsProtocolHandler.cs` - Uses HTTPS PasswordOrToken
- `Domain/ProtocolHandlers/AzureBlobProtocolHandler.cs` - Uses ConnectionString
- Any `*Manager.cs` or `*Service.cs` classes accessing these properties

**Update Process**:
1. Find all property access points
2. Replace old names with new names
3. Verify logic unchanged (simple rename)
4. Run unit tests for each manager
5. Verify compile successfully

**Acceptance Criteria**:
- All property accesses use new names
- Codebase search finds zero references to old names
- All managers compile without errors
- Unit tests for managers pass

---

#### Step 3.2: Update Message Handlers & Events

**Purpose**: Update any NServiceBus handlers processing protocol settings changes.

**Potential Areas**:
- Event handlers for ProtocolSettingsChanged events
- Command handlers for UpdateProtocolSettings commands
- Saga code accessing protocol properties
- Message mapping/deserialization code

**Update Process**:
1. Search for protocol settings event/command handling
2. Update property mapping code
3. Ensure JSON serialization/deserialization correct
4. Verify event contracts unchanged (NServiceBus compatibility)
5. Run integration tests

**Acceptance Criteria**:
- All handlers use new property names
- JSON serialization works correctly
- No NServiceBus contract changes required
- Handler tests pass

---

#### Step 3.3: Update Test Fixtures & Test Data Builders

**Purpose**: Ensure test code uses new property names.

**Files to Update**:
- `Tests/Unit/Domain/ProtocolSettingsTests.cs` - Create test objects
- `Tests/Integration/Builders/ProtocolSettingsBuilder.cs` - Test data builders
- `Tests/Integration/Fixtures/*` - Test fixtures
- `TestData/ProtocolSettingsTestData.cs` - Static test data
- Any `*DataHelper.cs` or `*Factory.cs` in tests

**Update Process**:
1. Find all test data creation code
2. Replace old property names in constructors/builders
3. Update any hardcoded test data files (if JSON, etc.)
4. Run all tests
5. Verify test data still realistic

**Acceptance Criteria**:
- All test files compile
- Test data builders use new names
- All existing tests pass with no logic changes
- New tests written for encryption features (Phase 4)

---

#### Step 3.4: Update Configuration & Validation Logic

**Purpose**: Update any configuration deserialization or validation that references old properties.

**Potential Areas**:
- Configuration validation classes
- Configuration mappers/deserializers
- OpenAPI/Swagger specifications
- Configuration documentation

**Update Process**:
1. Find configuration-related code
2. Update property names
3. Verify deserialization still works
4. Update any validation rules
5. Run configuration tests

**Acceptance Criteria**:
- Configuration deserialization works
- Validation logic correct
- OpenAPI specs updated if applicable
- Configuration tests pass

---

## Phase 4: Testing & Validation

### Objectives
- Verify encryption works correctly in all scenarios
- Test key rotation procedures
- Ensure no data loss during encryption
- Validate across multiple services

### Approach

#### Step 4.1: Unit Tests for Value Objects

**Purpose**: Ensure renamed properties work correctly at object level.

**Test Cases**:
1. **FtpProtocolSettings Creation**: Create with new Password property, verify immutable
2. **HttpsProtocolSettings Creation**: Create with new PasswordOrToken property, handle null
3. **AzureBlobProtocolSettings Creation**: Create with new ConnectionString property, handle null
4. **Property Access**: Verify properties accessible, correct type
5. **Validation**: Verify validation rules work (non-empty, max length, required if)
6. **Record Equality**: Verify record equality works with new properties
7. **Serialization**: Verify JSON serialization includes new property names

**Acceptance Criteria**:
- All 7 test cases pass
- Code coverage >95% for value objects
- Validation rules enforced
- Serialization correct

---

#### Step 4.2: Integration Tests for Encryption

**Purpose**: Verify encryption works end-to-end with new domain model.

**Test File**: `Tests/Integration/Features/CosmosEncryptionFeatureTests.cs`

**Test Cases**:

1. **FTP Password Encryption**:
   ```csharp
   [Test]
   public async Task CreateFtpSettings_PasswordEncrypted_InDatabase()
   {
       // Create FTP settings with Password property
       // Save to Cosmos
       // Verify password is ciphertext in database
       // Retrieve and verify password decrypted automatically
   }
   ```

2. **HTTPS PasswordOrToken Encryption**:
   ```csharp
   [Test]
   public async Task CreateHttpsSettings_TokenEncrypted_InDatabase()
   {
       // Create HTTPS settings with token
       // Save to Cosmos
       // Verify token encrypted in database
       // Retrieve and verify token accessible
   }
   ```

3. **Azure Blob ConnectionString Encryption**:
   ```csharp
   [Test]
   public async Task CreateBlobSettings_ConnectionStringEncrypted_InDatabase()
   {
       // Create Blob settings with connection string
       // Save to Cosmos
       // Verify connection string encrypted
       // Retrieve and verify works
   }
   ```

4. **Multiple Properties Encrypted Simultaneously**:
   ```csharp
   [Test]
   public async Task MultipleEncryptedProperties_AllEncrypted()
   {
       // Verify all three credential types encrypted in same document
   }
   ```

5. **Unencrypted Properties Remain Plain**:
   ```csharp
   [Test]
   public async Task UnencryptedProperties_NotEncrypted()
   {
       // Create settings with both encrypted and unencrypted properties
       // Verify unencrypted properties visible in plaintext in database
   }
   ```

6. **Key Rotation Scenario**:
   ```csharp
   [Test]
   public async Task KeyRotation_OldDocumentsStillDecrypt()
   {
       // Create document encrypted with key version 1
       // Rotate key to version 2 in Key Vault
       // Verify old document still decrypts
       // Create new document, verify uses version 2
   }
   ```

7. **Update Encrypted Property**:
   ```csharp
   [Test]
   public async Task UpdateEncryptedProperty_NewValueEncrypted()
   {
       // Create settings with password
       // Update password to new value
       // Verify new password encrypted in database
       // Verify old password no longer works
   }
   ```

**Acceptance Criteria**:
- All 7 test cases pass
- Encryption verified by examining raw Cosmos documents
- Decryption verified by checking retrieved values
- Key rotation tested
- Performance acceptable (encryption/decryption <100ms per operation)

---

#### Step 4.3: Cross-Service Integration Tests

**Purpose**: Verify encryption works across all services using protocol settings.

**Services to Test**:
1. FileIntegration (primary)
2. Other services with CosmosDB + protocol configurations

**Test Approach**:
1. Set up test infrastructure for each service
2. Run encryption feature tests against each service
3. Verify centralized encryption policy applied consistently
4. Test inter-service communication with encrypted properties

**Acceptance Criteria**:
- Encryption works in all services
- Policy applied consistently
- No service-specific issues
- Performance acceptable across all services

---

#### Step 4.4: Backward Compatibility Testing

**Purpose**: Ensure no breaking changes for NServiceBus contracts.

**Test Cases**:
1. **Event Serialization**: NServiceBus events still deserialize correctly
2. **Command Serialization**: NServiceBus commands still deserialize correctly
3. **Saga Persistence**: Sagas still persist and retrieve correctly
4. **Handler Routing**: Handlers correctly route to new property names

**Acceptance Criteria**:
- NServiceBus infrastructure unchanged
- Events/commands serialize/deserialize correctly
- No data loss during transition
- Existing handlers function correctly

---

## Phase 5: Documentation & Deployment Prep

### Objectives
- Document encryption implementation
- Prepare deployment procedures
- Create runbooks for key rotation
- Ensure knowledge transfer

### Approach

#### Step 5.1: Technical Documentation

**Deliverable**: `docs/cosmos-encryption-implementation.md`

**Contents**:
- Architecture overview (encryption policy, Key Vault integration)
- Configuration setup for each environment
- Property mapping (old names → new names)
- Key Vault setup requirements
- Encryption algorithm and key sizes
- Performance characteristics

**Audience**: Developers and DevOps engineers

---

#### Step 5.2: Key Rotation Runbook

**Deliverable**: `docs/cosmos-encryption-key-rotation-runbook.md`

**Contents**:
- Prerequisites (Key Vault access, testing environment)
- Step-by-step rotation procedure
- Verification tests to run after rotation
- Rollback procedures
- Troubleshooting guide

**Key Sections**:
1. Create new key version in Key Vault
2. Update configuration to reference new version
3. Run verification tests
4. Monitor application for issues
5. Rollback if needed

**Audience**: DevOps/Security team

---

#### Step 5.3: Deployment Checklist

**Deliverable**: `docs/cosmos-encryption-deployment-checklist.md`

**Pre-Deployment**:
- [ ] All code changes reviewed and approved
- [ ] All tests passing (unit, integration, cross-service)
- [ ] Key Vault configured with DEK
- [ ] Managed Identity permissions verified
- [ ] Configuration updated for all environments
- [ ] Documentation reviewed and complete
- [ ] Runbooks tested in staging

**Deployment**:
- [ ] Deploy to dev environment
- [ ] Run smoke tests
- [ ] Deploy to staging
- [ ] Run full test suite
- [ ] Deploy to production
- [ ] Verify encryption in production Cosmos
- [ ] Monitor for issues

**Post-Deployment**:
- [ ] Verify all protocol settings encrypted
- [ ] Check application performance
- [ ] Monitor Key Vault access logs
- [ ] Review error logs for encryption issues

---

## Phase 6: Knowledge Transfer & Closure

### Objectives
- Ensure team understands encryption implementation
- Document learnings and decisions
- Complete feature documentation
- Plan for future enhancements

### Approach

#### Step 6.1: Knowledge Transfer Session

**Session Type**: Technical walkthrough for development team

**Topics Covered**:
1. CosmosDB Always Encrypted architecture
2. Azure Key Vault integration and key management
3. Encryption policy configuration
4. Property naming changes and rationale
5. Testing strategy and test execution
6. Troubleshooting and common issues
7. Key rotation procedures

**Duration**: 2-3 hours

**Attendees**: Developers, QA, DevOps

---

#### Step 6.2: Post-Implementation Review

**Deliverable**: `docs/cosmos-encryption-post-implementation-review.md`

**Contents**:
- What went well
- Challenges faced and how they were resolved
- Lessons learned
- Performance metrics
- Future enhancement opportunities
- Cost implications

---

## Implementation Timeline

| Phase | Duration | Start | End |
|-------|----------|-------|-----|
| Phase 1: Foundation | 3-4 days | Week 1 | Week 1-2 |
| Phase 2: Domain Updates | 2-3 days | Week 2 | Week 2 |
| Phase 3: Consumer Updates | 3-4 days | Week 2 | Week 3 |
| Phase 4: Testing & Validation | 3-4 days | Week 3 | Week 4 |
| Phase 5: Documentation & Prep | 2 days | Week 4 | Week 4 |
| Phase 6: Knowledge Transfer | 1 day | Week 4 | Week 4 |
| **Total** | **14-18 days** | **Week 1** | **Week 4** |

---

## Risk Management

### Identified Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|-----------|
| Performance degradation | Medium | Medium | Profile encryption/decryption; benchmark before/after |
| Key Vault unavailability blocks application | Low | High | Implement retry logic; add circuit breaker; alert on failures |
| Incomplete property renaming | Low | High | Automated codebase search; compile-time validation; code review |
| Encryption policy not applied consistently | Medium | High | Centralize policy builder; document for other services; template approach |
| Key rotation breaking existing documents | Low | High | Extensive testing of key rotation scenario; maintain old key versions |
| NServiceBus contract incompatibility | Low | High | Test event/command serialization; avoid schema version changes |

### Mitigation Strategies

1. **Performance**: Run integration tests with realistic data volumes; profile encryption operations
2. **Key Vault**: Implement retry logic with exponential backoff; test Key Vault failure scenarios
3. **Code Quality**: Use automated tools (SonarQube, Roslyn analyzers) to find old property references
4. **Consistency**: Create shared NuGet package for encryption policy if serving multiple services
5. **Key Rotation**: Test rotation with old and new key versions simultaneously
6. **NServiceBus**: Test message serialization/deserialization before and after changes

---

## Success Metrics

| Metric | Target | Verification |
|--------|--------|--------------|
| All encrypted properties stored as ciphertext | 100% | Database inspection + tests |
| All old property names removed from codebase | 100% | Automated search + code review |
| Encryption/decryption performance | <100ms per operation | Integration test measurements |
| Test coverage | >95% | Code coverage reports |
| Key rotation success rate | 100% | Key rotation test execution |
| Documentation completeness | 100% | Runbook execution in staging |
| Team understanding | >80% | Knowledge transfer assessment |

---

## Deliverables Summary

### Code Deliverables
- `Infrastructure/Encryption/CosmosEncryptionPolicyBuilder.cs` - Policy configuration
- `Infrastructure/Encryption/KeyVaultConfiguration.cs` - Key Vault setup
- Updated `Domain/ProtocolSettings/*.cs` - Renamed properties
- Updated domain managers, handlers, test fixtures
- New integration tests for encryption

### Documentation Deliverables
- `docs/cosmos-encryption-implementation.md` - Technical reference
- `docs/cosmos-encryption-key-rotation-runbook.md` - Operations guide
- `docs/cosmos-encryption-deployment-checklist.md` - Deployment guide
- `docs/cosmos-encryption-post-implementation-review.md` - Post-mortem

### Testing Deliverables
- Unit tests for value objects and managers
- Integration tests for encryption pipeline
- Cross-service encryption tests
- Key rotation scenario tests
- Backward compatibility tests

---

## Dependencies & Prerequisites

### Azure Resources
- Azure Key Vault with Data Encryption Key (DEK) provisioned
- Managed Identity configured with Key Vault permissions
- CosmosDB accounts with Always Encrypted support (SDK 3.x+)

### Software Requirements
- .NET 6+
- CosmosDB SDK 3.30+ (Always Encrypted support)
- Azure.Security.KeyVault.Keys NuGet package
- NServiceBus 7.x+ with Cosmos persistence

### Access & Permissions
- Azure subscription access for Key Vault and CosmosDB
- Code repository write access
- Ability to deploy to dev/staging environments

---

## Approval & Sign-Off

| Role | Name | Signature | Date |
|------|------|-----------|------|
| Feature Owner | TBD | | |
| Tech Lead | TBD | | |
| Security Review | TBD | | |
| DevOps | TBD | | |

