# Implementation Tasks: CosmosDB Always Encrypted for Protocol Credentials

**Feature ID**: 002-cosmos-always-encrypted  
**Document Type**: Task Breakdown  
**Total Tasks**: 28  
**Status**: Ready for Execution  
**Last Updated**: 2025-01-07

---

## Task Organization

Tasks are organized by phase and include clear dependencies, acceptance criteria, and complexity estimates. Tasks are ordered for optimal execution flow while respecting dependencies.

**Task ID Format**: `T2-NNN` (Feature 002, Task NNN)

---

## Phase 1: Foundation & Infrastructure Setup

### T2-001: Create CosmosEncryptionPolicyBuilder Class
**Type**: Implementation  
**Complexity**: Medium  
**Depends On**: None  
**Estimated Hours**: 3  
**Assigned To**: Backend Engineer  

**Description**: Create centralized class for building CosmosDB encryption policies.

**Acceptance Criteria**:
- [ ] `Infrastructure/Encryption/CosmosEncryptionPolicyBuilder.cs` exists
- [ ] Class has `BuildPolicy(string keyVaultUri, string keyName, string keyVersion)` method
- [ ] Policy includes all three encrypted paths: `/protocolSettings/password`, `/protocolSettings/passwordOrToken`, `/protocolSettings/connectionString`
- [ ] Uses deterministic encryption type and AEAD_AES_256_CBC_HMAC_SHA256 algorithm
- [ ] Compiles without errors
- [ ] No external dependencies beyond CosmosDB SDK
- [ ] Class is static/utility type suitable for reuse

**Test Case**:
- Unit test verifies builder creates valid EncryptionPolicy object
- Unit test verifies all three paths included in policy
- Unit test verifies encryption algorithm correct

**Definition of Done**:
- Code written and reviewed
- Unit tests written and passing
- Ready for Phase 1.2

---

### T2-002: Implement KeyVaultConfiguration Class
**Type**: Implementation  
**Complexity**: Medium  
**Depends On**: T2-001  
**Estimated Hours**: 4  
**Assigned To**: Backend Engineer  

**Description**: Create Azure Key Vault client configuration for managing encryption keys.

**Acceptance Criteria**:
- [ ] `Infrastructure/Encryption/KeyVaultConfiguration.cs` exists
- [ ] Uses `DefaultAzureCredential` for Managed Identity authentication
- [ ] Reads Key Vault URI from configuration: `CosmosEncryption:KeyVaultUri`
- [ ] Reads key name from configuration: `CosmosEncryption:KeyName`
- [ ] Reads key version from configuration: `CosmosEncryption:KeyVersion`
- [ ] Creates KeyClient for Key Vault operations
- [ ] Provides dependency injection bindings for KeyClient
- [ ] Handles Key Vault unavailability gracefully
- [ ] Includes configuration validation

**Test Cases**:
- Unit test: KeyVaultConfiguration initializes with valid config
- Unit test: KeyVaultConfiguration validates missing required settings
- Integration test: KeyVaultConfiguration connects to Key Vault (if available in test env)

**Definition of Done**:
- Code written and reviewed
- All tests passing
- Configuration documentation updated
- Ready for Phase 1.3

---

### T2-003: Apply Encryption Policy to CosmosDB Container
**Type**: Implementation  
**Complexity**: Medium  
**Depends On**: T2-001, T2-002  
**Estimated Hours**: 4  
**Assigned To**: Backend Engineer  

**Description**: Integrate encryption policy into CosmosDB persistence configuration.

**Acceptance Criteria**:
- [ ] `Infrastructure/CosmosPersistenceConfiguration.cs` updated
- [ ] Encryption policy builder integrated into container initialization
- [ ] Policy applied to all protocol-related containers
- [ ] Configuration reads encryption settings from app configuration
- [ ] Policy application wrapped in try-catch with appropriate error handling
- [ ] Container initialization logs encryption policy application
- [ ] Compiles without errors
- [ ] No breaking changes to existing container setup

**Test Cases**:
- Unit test: Container configuration successfully applies encryption policy
- Integration test: Policy visible on initialized container
- Integration test: Encryption policy configuration validated

**Definition of Done**:
- Code written and reviewed
- Tests passing
- Configuration validated
- Ready for Phase 1.4

---

### T2-004: Write Integration Test for Encryption Pipeline
**Type**: Testing  
**Complexity**: High  
**Depends On**: T2-001, T2-002, T2-003  
**Estimated Hours**: 6  
**Assigned To**: QA Engineer  

**Description**: Create comprehensive integration tests verifying encryption works end-to-end.

**Acceptance Criteria**:
- [ ] Test file: `Tests/Integration/Infrastructure/CosmosEncryptionPipelineTests.cs` exists
- [ ] Test 1: Policy successfully applied to container
- [ ] Test 2: Encrypted property stored as ciphertext in database
- [ ] Test 3: Encrypted property decrypted automatically on retrieval
- [ ] Test 4: All three properties encrypted simultaneously
- [ ] Test 5: Unencrypted properties remain as plaintext
- [ ] Test 6: Key rotation handled correctly (old key still works)
- [ ] All tests use Cosmos Emulator or real Cosmos connection
- [ ] Tests can verify ciphertext in raw documents
- [ ] All tests pass consistently

**Test Details**:
```
T2-004-1: Policy Application
  - Create container with encryption policy
  - Verify policy present on container
  - Expected result: Policy applied successfully

T2-004-2: Encryption Verification
  - Create test document with encrypted property
  - Query raw document from Cosmos
  - Verify value is ciphertext (not plaintext)
  - Expected result: Plaintext never stored in database

T2-004-3: Decryption Verification
  - Create encrypted document
  - Retrieve with CosmosDB client
  - Verify automatic decryption occurs
  - Expected result: Application receives plaintext value

T2-004-4: Multiple Encryption
  - Create document with all three credential properties
  - Verify all three encrypted in database
  - Expected result: All three properties encrypted

T2-004-5: Unencrypted Properties
  - Create document with mixed encrypted/unencrypted properties
  - Verify unencrypted visible as plaintext
  - Expected result: Non-credential properties not encrypted

T2-004-6: Key Rotation
  - Create document with key version 1
  - Simulate key rotation to version 2
  - Verify old document still decrypts
  - Create new document with version 2
  - Expected result: Both key versions work correctly
```

**Definition of Done**:
- All tests written and passing
- Test code reviewed
- Ready to proceed with domain model changes

---

## Phase 2: Domain Model Updates

### T2-005: Update FtpProtocolSettings Value Object
**Type**: Implementation  
**Complexity**: Low  
**Depends On**: None (can start parallel to Phase 1)  
**Estimated Hours**: 2  
**Assigned To**: Backend Engineer  

**Description**: Rename `PasswordKeyVaultSecret` property to `Password` in FtpProtocolSettings.

**Acceptance Criteria**:
- [ ] `Domain/ProtocolSettings/FtpProtocolSettings.cs` updated
- [ ] Property `PasswordKeyVaultSecret` renamed to `Password`
- [ ] Property type remains `string` (non-nullable)
- [ ] Old property name completely removed
- [ ] All constructors updated
- [ ] Record equality and hashing still work
- [ ] Validation rules still apply (non-empty string)
- [ ] Compiles without errors
- [ ] No external behavior changes

**Test Cases**:
- Unit test: FtpProtocolSettings creates with new Password property
- Unit test: Password property immutable
- Unit test: Password validation works (non-empty)
- Unit test: Record equality works with new property

**Definition of Done**:
- Code written
- Tests pass (but may fail in later phases until consumers updated)
- Ready for consumer updates

---

### T2-006: Update HttpsProtocolSettings Value Object
**Type**: Implementation  
**Complexity**: Low  
**Depends On**: None (can start parallel to Phase 1)  
**Estimated Hours**: 2  
**Assigned To**: Backend Engineer  

**Description**: Rename `PasswordOrTokenKeyVaultSecret` property to `PasswordOrToken` in HttpsProtocolSettings.

**Acceptance Criteria**:
- [ ] `Domain/ProtocolSettings/HttpsProtocolSettings.cs` updated
- [ ] Property `PasswordOrTokenKeyVaultSecret` renamed to `PasswordOrToken`
- [ ] Property type remains `string?` (nullable)
- [ ] Old property name completely removed
- [ ] Validation for max 200 characters applied
- [ ] Nullable handling correct (can be null)
- [ ] Record equality works
- [ ] Compiles without errors

**Test Cases**:
- Unit test: HttpsProtocolSettings creates with PasswordOrToken property
- Unit test: PasswordOrToken can be null
- Unit test: Max 200 character validation works
- Unit test: Record equality works with nullable property

**Definition of Done**:
- Code written
- Unit tests pass
- Ready for consumer updates

---

### T2-007: Update AzureBlobProtocolSettings Value Object
**Type**: Implementation  
**Complexity**: Low  
**Depends On**: None (can start parallel to Phase 1)  
**Estimated Hours**: 2  
**Assigned To**: Backend Engineer  

**Description**: Rename `ConnectionStringKeyVaultSecret` property to `ConnectionString` in AzureBlobProtocolSettings.

**Acceptance Criteria**:
- [ ] `Domain/ProtocolSettings/AzureBlobProtocolSettings.cs` updated
- [ ] Property `ConnectionStringKeyVaultSecret` renamed to `ConnectionString`
- [ ] Property type remains `string?` (nullable)
- [ ] Old property name completely removed
- [ ] Validation: Required if AuthType is ConnectionString
- [ ] Nullable for other auth types
- [ ] Record equality works
- [ ] Compiles without errors

**Test Cases**:
- Unit test: AzureBlobProtocolSettings creates with ConnectionString property
- Unit test: ConnectionString can be null
- Unit test: Required when AuthType is ConnectionString
- Unit test: Record equality works

**Definition of Done**:
- Code written
- Unit tests pass
- Ready for consumer updates

---

### T2-008: Create [Encrypted] Attribute
**Type**: Implementation  
**Complexity**: Low  
**Depends On**: None  
**Estimated Hours**: 1  
**Assigned To**: Backend Engineer  

**Description**: Create custom attribute to mark encrypted properties for documentation.

**Acceptance Criteria**:
- [ ] `Domain/Attributes/EncryptedAttribute.cs` exists
- [ ] Attribute marked with `[AttributeUsage(AttributeTargets.Property)]`
- [ ] Optional properties for KeyVaultKeyName and EncryptionAlgorithm
- [ ] Compiles without errors
- [ ] Ready to apply to credential properties

**Definition of Done**:
- Attribute created and compiles
- Ready to apply to protocol settings

---

### T2-009: Apply [Encrypted] Attribute to Properties
**Type**: Implementation  
**Complexity**: Low  
**Depends On**: T2-008  
**Estimated Hours**: 1  
**Assigned To**: Backend Engineer  

**Description**: Apply `[Encrypted]` attribute to three credential properties.

**Acceptance Criteria**:
- [ ] `FtpProtocolSettings.Password` marked with `[Encrypted]`
- [ ] `HttpsProtocolSettings.PasswordOrToken` marked with `[Encrypted]`
- [ ] `AzureBlobProtocolSettings.ConnectionString` marked with `[Encrypted]`
- [ ] Compiles without errors
- [ ] Documentation updated to explain attribute

**Definition of Done**:
- All three properties marked
- Documentation updated

---

## Phase 3: Consumer Updates Across Codebase

### T2-010: Find All Property References via Codebase Search
**Type**: Analysis  
**Complexity**: Medium  
**Depends On**: None (prepare in parallel)  
**Estimated Hours**: 2  
**Assigned To**: Backend Engineer  

**Description**: Identify all references to old property names across codebase.

**Acceptance Criteria**:
- [ ] Codebase searched for `PasswordKeyVaultSecret`
- [ ] Codebase searched for `PasswordOrTokenKeyVaultSecret`
- [ ] Codebase searched for `ConnectionStringKeyVaultSecret`
- [ ] Results documented in task notes
- [ ] File list and line numbers recorded
- [ ] Organized by file/class type (managers, handlers, tests, etc.)

**Deliverable**: Document listing all property references with file/line numbers

**Definition of Done**:
- Complete search results documented
- Ready for targeted updates

---

### T2-011: Update FTP Protocol Domain Manager
**Type**: Implementation  
**Complexity**: Medium  
**Depends On**: T2-005, T2-010  
**Estimated Hours**: 2  
**Assigned To**: Backend Engineer  

**Description**: Update FTP protocol handler/manager to use new `Password` property name.

**Acceptance Criteria**:
- [ ] All references to `PasswordKeyVaultSecret` replaced with `Password`
- [ ] Logic unchanged (simple property name update)
- [ ] Compiles without errors
- [ ] Unit tests for manager pass
- [ ] No references to old property name remain

**Test Cases**:
- Unit test: Manager uses correct property
- Unit test: Manager behavior unchanged after rename

**Definition of Done**:
- Code updated and reviewed
- Tests passing

---

### T2-012: Update HTTPS Protocol Domain Manager
**Type**: Implementation  
**Complexity**: Medium  
**Depends On**: T2-006, T2-010  
**Estimated Hours**: 2  
**Assigned To**: Backend Engineer  

**Description**: Update HTTPS protocol handler/manager to use new `PasswordOrToken` property name.

**Acceptance Criteria**:
- [ ] All references to `PasswordOrTokenKeyVaultSecret` replaced with `PasswordOrToken`
- [ ] Null handling correct (property is nullable)
- [ ] Logic unchanged
- [ ] Compiles without errors
- [ ] Unit tests pass

**Definition of Done**:
- Code updated and reviewed
- Tests passing

---

### T2-013: Update Azure Blob Protocol Domain Manager
**Type**: Implementation  
**Complexity**: Medium  
**Depends On**: T2-007, T2-010  
**Estimated Hours**: 2  
**Assigned To**: Backend Engineer  

**Description**: Update Azure Blob protocol handler/manager to use new `ConnectionString` property name.

**Acceptance Criteria**:
- [ ] All references to `ConnectionStringKeyVaultSecret` replaced with `ConnectionString`
- [ ] Null handling correct
- [ ] Conditional logic for required-if-auth-type-is-connectionstring works
- [ ] Logic unchanged
- [ ] Unit tests pass

**Definition of Done**:
- Code updated and reviewed
- Tests passing

---

### T2-014: Update NServiceBus Event Handlers
**Type**: Implementation  
**Complexity**: Medium  
**Depends On**: T2-005, T2-006, T2-007, T2-010  
**Estimated Hours**: 3  
**Assigned To**: Backend Engineer  

**Description**: Update all NServiceBus message handlers referencing old property names.

**Acceptance Criteria**:
- [ ] All event handlers updated to use new property names
- [ ] All command handlers updated
- [ ] Message mapping code updated
- [ ] JSON deserialization works correctly
- [ ] NServiceBus contracts unchanged (no schema version changes)
- [ ] Handler tests pass
- [ ] No compilation errors

**Specific Areas**:
- Protocol settings changed events
- Configuration update commands
- Any saga code accessing properties
- Message mapping/transformation code

**Definition of Done**:
- All handlers updated
- Handler tests passing
- NServiceBus integration tests passing

---

### T2-015: Update Test Fixture Builders
**Type**: Implementation  
**Complexity**: Medium  
**Depends On**: T2-005, T2-006, T2-007  
**Estimated Hours**: 3  
**Assigned To**: QA Engineer  

**Description**: Update all test data builders and fixtures to use new property names.

**Acceptance Criteria**:
- [ ] `Tests/Integration/Builders/ProtocolSettingsBuilder.cs` updated
- [ ] `Tests/Unit/Domain/ProtocolSettingsTests.cs` updated
- [ ] All test fixtures updated
- [ ] Test data helpers updated
- [ ] Factory methods updated
- [ ] All test builders compile
- [ ] Builder test cases pass

**Files to Update**:
- Protocol settings test builders
- FTP/HTTPS/Azure Blob specific builders
- Test fixture classes
- Test data generators

**Definition of Done**:
- All builders updated
- Tests compile and pass

---

### T2-016: Update Test Data Files
**Type**: Implementation  
**Complexity**: Low  
**Depends On**: T2-010  
**Estimated Hours**: 2  
**Assigned To**: QA Engineer  

**Description**: Update any hardcoded test data files (JSON, XML, etc.) to use new property names.

**Acceptance Criteria**:
- [ ] All JSON test data files updated
- [ ] All XML test data files updated
- [ ] All CSV/other format test data updated
- [ ] Property names match new domain model
- [ ] Test data still realistic and valid
- [ ] No references to old property names remain

**Definition of Done**:
- Test data files updated
- Ready for test execution

---

### T2-017: Update Configuration & Validation Logic
**Type**: Implementation  
**Complexity**: Medium  
**Depends On**: T2-010  
**Estimated Hours**: 2  
**Assigned To**: Backend Engineer  

**Description**: Update configuration deserialization and validation code.

**Acceptance Criteria**:
- [ ] Configuration mappers/deserializers updated
- [ ] Configuration validation logic updated
- [ ] Property validation rules still enforced
- [ ] Configuration binding tests pass
- [ ] No compilation errors

**Specific Areas**:
- Configuration model classes
- Configuration deserialization code
- Validation attribute updates if any
- Configuration binding tests

**Definition of Done**:
- Configuration code updated
- Configuration tests passing

---

### T2-018: Update OpenAPI/Swagger Specifications (if applicable)
**Type**: Documentation  
**Complexity**: Low  
**Depends On**: T2-010  
**Estimated Hours**: 1  
**Assigned To**: Backend Engineer  

**Description**: Update API documentation to reflect new property names.

**Acceptance Criteria**:
- [ ] OpenAPI spec updated (if applicable)
- [ ] Swagger schema reflects new property names
- [ ] API documentation updated
- [ ] Generated API docs correct

**Definition of Done**:
- Documentation updated
- API spec validated

---

### T2-019: Comprehensive Codebase Search for Remaining References
**Type**: Validation  
**Complexity**: Medium  
**Depends On**: T2-011, T2-012, T2-013, T2-014, T2-015, T2-016, T2-017, T2-018  
**Estimated Hours**: 2  
**Assigned To**: Backend Engineer  

**Description**: Perform final verification that no old property names remain in codebase.

**Acceptance Criteria**:
- [ ] Full codebase search finds zero references to `PasswordKeyVaultSecret`
- [ ] Full codebase search finds zero references to `PasswordOrTokenKeyVaultSecret`
- [ ] Full codebase search finds zero references to `ConnectionStringKeyVaultSecret`
- [ ] Any comments mentioning old names are updated
- [ ] Documentation references updated
- [ ] Search results documented

**Definition of Done**:
- Search completed
- No old property names found
- Ready for testing phase

---

## Phase 4: Testing & Validation

### T2-020: Write Unit Tests for Renamed Value Objects
**Type**: Testing  
**Complexity**: Medium  
**Depends On**: T2-005, T2-006, T2-007  
**Estimated Hours**: 3  
**Assigned To**: QA Engineer  

**Description**: Create comprehensive unit tests for renamed properties in protocol settings.

**Acceptance Criteria**:
- [ ] Test file: `Tests/Unit/Domain/ProtocolSettings/RenamedPropertiesTests.cs` exists
- [ ] FTP Password property test (creation, immutability, validation)
- [ ] HTTPS PasswordOrToken property test (nullable, max length validation)
- [ ] Azure Blob ConnectionString property test (nullable, conditional required)
- [ ] Record equality tests with new properties
- [ ] Serialization/deserialization tests
- [ ] All tests pass
- [ ] Code coverage >95%

**Test Cases**:
```
- FTP Password: Create, immutable, non-empty validation
- HTTPS PasswordOrToken: Create, nullable, max length 200
- Azure Blob ConnectionString: Create, nullable, required if auth type
- Equality: Records equal if all properties match
- Serialization: JSON includes new property names
- Hashing: Hash codes include new properties
```

**Definition of Done**:
- All unit tests written and passing
- Code coverage verified

---

### T2-021: Write Domain Manager Tests
**Type**: Testing  
**Complexity**: High  
**Depends On**: T2-011, T2-012, T2-013  
**Estimated Hours**: 4  
**Assigned To**: QA Engineer  

**Description**: Create tests verifying domain managers work correctly with new properties.

**Acceptance Criteria**:
- [ ] FTP manager tests: Uses Password property, connections work
- [ ] HTTPS manager tests: Uses PasswordOrToken property, handles null
- [ ] Azure Blob manager tests: Uses ConnectionString property, auth validation
- [ ] Handler tests: All message handlers work correctly
- [ ] Integration tests: Full domain manager workflows
- [ ] All tests pass

**Test Scenarios**:
- Create settings with credentials
- Update settings with new credentials
- Validate credentials work for connections
- Error handling when credentials invalid

**Definition of Done**:
- Domain manager tests written and passing
- Handler tests passing

---

### T2-022: Write NServiceBus Handler Tests
**Type**: Testing  
**Complexity**: High  
**Depends On**: T2-014  
**Estimated Hours**: 4  
**Assigned To**: QA Engineer  

**Description**: Create tests verifying NServiceBus event/command handlers work correctly.

**Acceptance Criteria**:
- [ ] Event handler tests: Receive events, update properties correctly
- [ ] Command handler tests: Process commands, use new property names
- [ ] Saga tests: Sagas work with new properties
- [ ] Message mapping tests: Deserialization works
- [ ] Backward compatibility tests: Old and new messages handled
- [ ] All tests pass

**Test Scenarios**:
- Receive ProtocolSettingsChanged event, use new property names
- Process UpdateProtocolSettings command
- Saga persistence and retrieval
- Message serialization round-trip

**Definition of Done**:
- Handler tests written and passing
- Message compatibility verified

---

### T2-023: Write Encryption Feature Integration Tests
**Type**: Testing  
**Complexity**: High  
**Depends On**: T2-004, T2-020, T2-021  
**Estimated Hours**: 5  
**Assigned To**: QA Engineer  

**Description**: Create integration tests verifying encryption works with complete domain model.

**Acceptance Criteria**:
- [ ] Test file: `Tests/Integration/Features/CosmosEncryptionFeatureTests.cs` exists
- [ ] FTP credential encryption test (password encrypted/decrypted)
- [ ] HTTPS credential encryption test (token encrypted/decrypted)
- [ ] Azure Blob credential encryption test (connection string encrypted/decrypted)
- [ ] Multiple credentials encrypted simultaneously
- [ ] Unencrypted properties remain plaintext
- [ ] Update encrypted property scenario
- [ ] Key rotation scenario
- [ ] All tests pass
- [ ] Performance acceptable (<100ms per operation)

**Detailed Test Cases**:
```
T2-023-1: FTP Password Encryption
  - Create FTP settings with password
  - Save to Cosmos
  - Verify ciphertext in database
  - Retrieve and verify decryption

T2-023-2: HTTPS PasswordOrToken Encryption
  - Create HTTPS settings with token
  - Save to Cosmos
  - Verify ciphertext in database
  - Retrieve and verify token accessible

T2-023-3: Azure Blob ConnectionString Encryption
  - Create Blob settings with connection string
  - Save to Cosmos
  - Verify ciphertext in database
  - Retrieve and verify works

T2-023-4: Multiple Credentials Encrypted
  - Create document with all three credential types
  - Verify all encrypted in database
  - Retrieve and verify all accessible

T2-023-5: Unencrypted Properties
  - Create document with mixed properties
  - Verify unencrypted visible as plaintext

T2-023-6: Update Credentials
  - Create with password
  - Update to new password
  - Verify new value encrypted
  - Verify old value removed

T2-023-7: Key Rotation
  - Create document with key v1
  - Rotate to key v2
  - Verify v1 document decrypts
  - Create v2 document
  - Verify v2 works
```

**Performance Benchmarks**:
- Encrypt operation: <50ms
- Decrypt operation: <50ms
- Document roundtrip: <100ms

**Definition of Done**:
- All integration tests written and passing
- Performance metrics acceptable
- Ready for cross-service testing

---

### T2-024: Cross-Service Encryption Tests
**Type**: Testing  
**Complexity**: High  
**Depends On**: T2-023  
**Estimated Hours**: 5  
**Assigned To**: QA Engineer  

**Description**: Test encryption works consistently across all services using protocol settings.

**Acceptance Criteria**:
- [ ] FileIntegration encryption tests pass
- [ ] Other dependent services encryption tests pass
- [ ] Encryption policy applied consistently across services
- [ ] No service-specific encryption issues
- [ ] Inter-service communication with encrypted properties works
- [ ] All services handle key rotation correctly

**Services to Test**:
- FileIntegration (primary)
- Any other services with CosmosDB + protocol configs

**Definition of Done**:
- All services tested successfully
- Encryption consistent across services
- Ready for documentation phase

---

### T2-025: Backward Compatibility Validation Tests
**Type**: Testing  
**Complexity**: Medium  
**Depends On**: T2-014, T2-022  
**Estimated Hours**: 3  
**Assigned To**: QA Engineer  

**Description**: Verify NServiceBus contracts unchanged and messages serialize/deserialize correctly.

**Acceptance Criteria**:
- [ ] Event serialization tests pass
- [ ] Command serialization tests pass
- [ ] Event deserialization tests pass
- [ ] Command deserialization tests pass
- [ ] Saga persistence works
- [ ] Message routing correct
- [ ] No NServiceBus contract version changes needed

**Test Scenarios**:
- Serialize events with new property names
- Deserialize events correctly
- Saga reads/writes with new properties
- Handler routing unchanged
- Message version numbers unchanged

**Definition of Done**:
- All backward compatibility tests passing
- No breaking changes to NServiceBus

---

## Phase 5: Documentation & Deployment Preparation

### T2-026: Write Technical Documentation
**Type**: Documentation  
**Complexity**: Medium  
**Depends On**: T2-001 through T2-025  
**Estimated Hours**: 4  
**Assigned To**: Technical Writer  

**Description**: Create comprehensive technical documentation for encryption implementation.

**Deliverable**: `docs/cosmos-encryption-implementation.md`

**Contents**:
- Architecture overview (encryption policy, Key Vault integration)
- Property mapping (old → new names)
- Configuration setup by environment (dev, staging, prod)
- CosmosDB Always Encrypted feature explanation
- Encryption algorithm details
- Key Vault setup requirements
- Performance characteristics
- Troubleshooting guide
- Code examples

**Acceptance Criteria**:
- [ ] Documentation file created
- [ ] Architecture clearly explained
- [ ] Configuration examples provided
- [ ] Property mapping documented
- [ ] Troubleshooting section included
- [ ] Code examples working
- [ ] Reviewed by technical team

**Definition of Done**:
- Documentation complete and reviewed
- Ready for deployment preparation

---

### T2-027: Create Key Rotation Runbook
**Type**: Documentation  
**Complexity**: Medium  
**Depends On**: T2-025  
**Estimated Hours**: 3  
**Assigned To**: DevOps Engineer  

**Description**: Document step-by-step key rotation procedure for operations team.

**Deliverable**: `docs/cosmos-encryption-key-rotation-runbook.md`

**Contents**:
- Prerequisites and access requirements
- Step-by-step rotation procedure
- Verification tests to run after rotation
- Rollback procedures
- Troubleshooting and common issues
- Monitoring and alerting setup
- Timeline estimates

**Key Sections**:
1. Create new key version in Key Vault
2. Test in development environment
3. Update configuration
4. Deploy configuration changes
5. Run verification tests
6. Monitor for issues
7. Rollback procedure if needed

**Acceptance Criteria**:
- [ ] Runbook file created
- [ ] All steps clearly documented
- [ ] Verification test procedures included
- [ ] Rollback steps included
- [ ] Troubleshooting guide included
- [ ] Tested in staging environment

**Definition of Done**:
- Runbook complete and tested
- Ready for operations team

---

### T2-028: Create Deployment Checklist
**Type**: Documentation  
**Complexity**: Low  
**Depends On**: All previous tasks  
**Estimated Hours**: 2  
**Assigned To**: Tech Lead  

**Description**: Create comprehensive deployment checklist for production rollout.

**Deliverable**: `docs/cosmos-encryption-deployment-checklist.md`

**Sections**:
- Pre-deployment validation checklist
- Pre-deployment sign-offs
- Deployment procedure by environment
- Deployment verification steps
- Post-deployment validation
- Rollback procedures
- Monitoring and alerting setup

**Checklist Items**:
- Code review completed
- All tests passing
- Documentation reviewed
- Key Vault configured
- Managed Identity permissions verified
- Configuration updated for all environments
- Smoke tests prepared
- Runbooks tested
- Team trained
- Deployment windows scheduled

**Acceptance Criteria**:
- [ ] Checklist file created
- [ ] All pre-deployment items identified
- [ ] Verification procedures documented
- [ ] Post-deployment monitoring identified
- [ ] Rollback procedures documented
- [ ] Team reviewed and approved

**Definition of Done**:
- Checklist complete
- Ready for deployment execution

---

## Task Dependencies Graph

```
Phase 1 (Foundation):
  T2-001 ──────────────┐
  T2-002 ──────────────┼──> T2-003 ──> T2-004
                        │
Phase 2 (Domain):
  T2-005 (FTP)
  T2-006 (HTTPS)
  T2-007 (Blob)
  T2-008 ──> T2-009
  T2-010 (Search)

Phase 3 (Consumers):
  T2-005 ──> T2-011
  T2-006 ──> T2-012
  T2-007 ──> T2-013
  T2-010, T2-005, T2-006, T2-007 ──> T2-014
  T2-005, T2-006, T2-007 ──> T2-015
  T2-010 ──> T2-016, T2-017, T2-018
  T2-011, T2-012, T2-013, T2-014, T2-015, T2-016, T2-017, T2-018 ──> T2-019

Phase 4 (Testing):
  T2-005, T2-006, T2-007 ──> T2-020
  T2-011, T2-012, T2-013 ──> T2-021
  T2-014 ──> T2-022
  T2-004, T2-020, T2-021 ──> T2-023
  T2-023 ──> T2-024
  T2-014, T2-022 ──> T2-025

Phase 5 (Documentation):
  T2-001 through T2-025 ──> T2-026
  T2-025 ──> T2-027
  All ──> T2-028
```

---

## Recommended Execution Order

### Week 1: Foundation & Domain Model
- **Days 1-2**: T2-001, T2-002, T2-003 (in parallel with T2-005, T2-006, T2-007)
- **Day 3**: T2-004, T2-008, T2-009, T2-010
- **Days 4-5**: T2-011 through T2-019 (parallel execution of consuming code updates)

### Week 2: Testing
- **Days 1-2**: T2-020, T2-021, T2-022 (parallel)
- **Days 3-5**: T2-023, T2-024, T2-025 (parallel)

### Week 3: Documentation & Closure
- **Days 1-2**: T2-026, T2-027, T2-028 (parallel)
- **Days 3-5**: Knowledge transfer, final validation, deployment preparation

---

## Resource Requirements

### Backend Engineers
- T2-001, T2-002, T2-003: 1 engineer × 11 hours
- T2-005, T2-006, T2-007, T2-008, T2-009: 1 engineer × 7 hours
- T2-011, T2-012, T2-013, T2-014, T2-017, T2-018: 1 engineer × 12 hours
- **Total**: ~30 hours backend engineering

### QA Engineers
- T2-004: 1 engineer × 6 hours
- T2-015, T2-016: 1 engineer × 5 hours
- T2-020, T2-021, T2-022, T2-023, T2-024, T2-025: 1 engineer × 19 hours
- **Total**: ~30 hours QA engineering

### DevOps/Infrastructure
- T2-002, T2-003: 2 hours (consulting/setup)
- T2-027: 3 hours (runbook creation)
- **Total**: ~5 hours DevOps

### Technical Writer/Documentation
- T2-026: 4 hours

### Tech Lead/Oversight
- T2-028: 2 hours
- Review: ~5 hours distributed across tasks

---

## Success Metrics

| Metric | Target |
|--------|--------|
| All tasks completed | 100% |
| All tests passing | 100% |
| Code coverage | >95% |
| Zero old property references in code | 100% |
| Documentation complete | 100% |
| Team trained | 100% |

---

## Risks by Task

| Task | Risk | Mitigation |
|------|------|-----------|
| T2-001, T2-002, T2-003 | Key Vault setup complexity | Early coordination with DevOps team |
| T2-004 | Testing encryption tricky | Detailed test planning, use Cosmos Emulator |
| T2-019 | Incomplete search missing references | Use multiple search patterns, manual verification |
| T2-023 | Performance not acceptable | Early performance testing, optimization if needed |
| T2-027 | Key rotation untested | Test rotation in staging before production |

---

## Notes & Considerations

- Tasks can be parallelized within same phase (e.g., T2-005, T2-006, T2-007 in parallel)
- Early integration testing (T2-004) validates foundation before consumer updates
- Consumer updates (Phase 3) can happen while foundation being tested
- Testing (Phase 4) validates all changes before documentation
- Each task includes clear acceptance criteria for quality gates
- Document all decisions and learnings for knowledge transfer

