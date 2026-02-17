# Domain Builder Agent

**Purpose**: Build, compile, and verify complete bounded context implementation with all layers (Domain, Infrastructure, API, Endpoint.In) and tests based on technical specifications.

**Agent Type**: Full-Stack Code Generation & Execution

---

## Capabilities

This agent **EXECUTES** all steps to create a working domain:
- ✅ Create ALL project files (API, Domain, Infrastructure, Endpoint.In, Tests)
- ✅ Generate ALL source code files based on technical specifications
- ✅ Add projects to solution file (RiskInsure.slnx)
- ✅ Verify port assignments and scan for conflicts
- ✅ **BUILD all projects** and verify compilation succeeds
- ✅ Copy connection strings from existing services
- ✅ Create launchSettings.json with correct ports
- ✅ **RUN unit tests** and verify they pass
- ✅ Install Playwright dependencies for integration tests
- ✅ Generate startup instructions and verification checklist
- ✅ Follow constitutional principles and architectural standards

**This is NOT a planning agent** - it creates actual working code and verifies functionality.

---

## Prerequisites

Before invoking this agent, ensure:
1. **Technical specification exists**: `services/{domain}/docs/technical/{domain}-technical-spec.md`
2. **Business requirements exist**: `services/{domain}/docs/business/{domain}-business-requirements.md`
3. **Cosmos DB Emulator running**: - this can be pulled from other domain appsettings.Development.json files
4. **Azure Service Bus namespace created**: Connection string available - this can be pulled from other domain appsettings.Development.json files
5. **.NET 10 SDK installed**
6. **Node.js installed** (for Playwright tests)

---

## Input Requirements

Provide the following information:
- **Domain Name**: Name of the bounded context (e.g., "Customer", "Policy", "RatingAndUnderwriting")
- **Technical Spec Path**: Path to technical specification document
- **Port Numbers**: API port and Endpoint.In port (agent will check for conflicts)

---

## Implementation Plan

The agent **EXECUTES** this structured approach (actual file creation and builds):

### Phase 1: Port Assignment & Structure
1. **SCAN** existing services for port conflicts using PowerShell
2. **ASSIGN** next available ports (API and Endpoint.In)
3. **CREATE** directory structure: `services/{domain}/src/{Api,Domain,Infrastructure,Endpoint.In}`
4. **CREATE** test structure: `services/{domain}/test/{Unit.Tests,Integration.Tests}`
5. **CREATE** all `.csproj` files with correct namespaces and dependencies

**CREATE** actual files using `create_file` tool:
1. **Models/** - Domain entities and value objects (C# classes)
2. **Contracts/Events/** - Event definitions (past tense, C# records)
3. **Contracts/Commands/** - Command definitions (imperative, C# records)
4. **Repositories/** - Interface and Cosmos DB implementation (C# classes)
5. **Managers/** - Business logic orchestration (C# classes)
6. **Domain.csproj** - Project file with package references

### Phase 3: Infrastructure Layer
**CREATE** actual files:
1. **CosmosDbInitializer.cs** - Database and container setup
2. **Infrastructure.csproj** - Project file with dependencies

### Phase 4: API Layer
**CREATE** actual files:
1. **Program.cs** - DI, NServiceBus (send-only), Swagger/Scalar, Serilog
2. **Models/** - Request/Response DTOs (C# classes)
3. **Properties/launchSettings.json** - Port configuration (use assigned port)
4. **appsettings.json** - Base configuration
5. **appsettings.Development.json.template** - Template with placeholders
6. **Api.csproj** - Project file

### Phase 5: Endpoint.In Layer
**CREATE** actual files:
1. **Handlers/** - Message handlers if domain subscribes to events (C# classes)
2. **Program.cs** - NServiceBus host with routing
3. **Properties/launchSettings.json** - Port configuration with DOTNET_ENVIRONMENT=Development
4. **appsettings.json** - Base configuration
5. **appsettings.Development.json.template** - Template with placeholders
6. **Endpoint.In.csproj** - Project file

### Phase 5a: Service Bus Queue Setup
**CREATE** queue creation script:
1. **../Infrastructure/queues.sh** - Bash script to create Service Bus queues and subscriptions
   - Include shared infrastructure queues (error, audit, particular.monitoring)
   - Create endpoint for this domain
   - Add subscriptions for any PublicContracts events this domain subscribes to
   - Make script executable with `chmod +x`
   - Add usage instructions in comments

### Phase 6: Unit Tests
**CREATE** actual test files:
1. **Manager tests** - Business orchestration (mocked repos)
2. **Repository tests** - Cosmos DB CRUD (requires emulator)
3. **Unit.Tests.csproj** - Test project file

### Phase 7: Integration Tests (Playwright)
**CREATE** actual files:
1. **package.json** - Playwright dependencies
2. **playwright.config.ts** - Test configuration (baseURL with assigned port)
3. **tests/*.spec.ts** - API test scenarios from technical spec
4. **README.md** - How to run tests

**⚠️ CRITICAL: Test Only What Domain Controls**

**Domain Data Creation Dependencies**:
- Some domains can create their own test data via direct API calls (e.g., Customer domain: POST /api/customers)
- Other domains rely on **event-driven creation** from external domains (e.g., Policy domain: policies created via QuoteAccepted events from Rating & Underwriting)
- **RULE**: Integration tests should ONLY test endpoints this domain has full control over

**What to Test in Domain Integration Tests**:
1. ✅ **404 errors** for non-existent entities (use `crypto.randomUUID()` for test IDs)
2. ✅ **Validation errors** on malformed requests (missing required fields, invalid formats)
3. ✅ **Empty collection responses** when no data exists (domain controls this response)
4. ✅ **Operations that don't require existing data** (e.g., creating entities if domain supports direct creation)

**What to DEFER to Enterprise Integration Tests**:
1. ❌ "Happy path" scenarios requiring data from **other domains** (cross-domain events)
2. ❌ Testing state transitions on existing entities when domain cannot create them directly
3. ❌ Testing queries that return populated data when data creation requires external events
4. ❌ Any test requiring coordination between multiple bounded contexts

**Example - Policy Domain**:
- ✅ Test: GET /{id} returns 404 for non-existent policy (domain controls 404 response)
- ✅ Test: POST /{id}/cancel validates required fields (domain controls validation)
- ✅ Test: GET /customers/{id}/policies returns empty array for new customer (domain controls empty response)
- ❌ Defer: POST /{id}/issue succeeds for Bound policy (requires QuoteAccepted event from Rating & Underwriting)
- ❌ Defer: POST /{id}/cancel succeeds for Active policy (requires policy creation via external event)

**Implementation Pattern**:
```typescript
// ✅ CORRECT - Testing 404 (domain controls error response)
test('should return 404 when policy not found', async ({ request }) => {
  const nonExistentId = crypto.randomUUID();
  const response = await request.get(`/api/policies/${nonExistentId}`);
  expect(response.status()).toBe(404);
});

// ✅ CORRECT - Testing validation (domain controls validation logic)
test('should validate required fields', async ({ request }) => {
  const policyId = crypto.randomUUID();
  const response = await request.post(`/api/policies/${policyId}/cancel`, {
    data: { /* missing required fields */ }
  });
  expect(response.status()).toBe(400);
  expect(error.errors.CancellationReason).toBeDefined();
});

// ❌ INCORRECT - Requires policy created via QuoteAccepted event (cross-domain)
test.skip('should cancel active policy', async ({ request }) => {
  // This test requires Rating & Underwriting to publish QuoteAccepted
  // Deferred to enterprise integration tests
});
```

**Documentation Requirement**:
- Add comment in test files: `// NOTE: Tests requiring cross-domain data deferred to enterprise integration tests`
- Document in test README.md which scenarios are deferred and why

**⚠️ CRITICAL: Verify Test Assertions Match API Response Format**
5. **REVIEW** validation error assertions:
   - ASP.NET Core returns **ProblemDetails** format (not custom error objects)
   - Validation errors are in `error.errors.FieldName` as **arrays**, not strings
   - Standard fields: `status`, `title`, `errors` (not `error`, `message`)
   - Example: `expect(error.errors.Email).toBeDefined()` and `expect(Array.isArray(error.errors.Email)).toBe(true)`
6. **TEST** one validation scenario manually before writing all tests to confirm response format

### Phase 8: Build & Verify
**EXECUTE** build commands:
1. **RUN** `dotnet sln add` for all projects
2. **RUN** `dotnet restore` on solution
3. **RUN** `dotnet build` on each project
4. **VERIFY** all builds succeed (exit code 0)
5. **COPY** connection strings from billing/fundstransfermgt appsettings.Development.json
6. **CREATE** actual appsettings.Development.json files (not templates)

### Phase 9: Test Execution
**EXECUTE** tests:
1. **RUN** `dotnet test` on Unit.Tests project
2. **VERIFY** all tests pass
3. **RUN** `npm install` in Integration.Tests directory
4. **RUN** `npx playwright install chromium`
5. Document test execution status

### Phase 10: Verification & Documentation
**VERIFY** and **DOCUMENT**:
1. Confirm all builds succeeded
2. Confirm unit tests passed
3. **START API and test one validation endpoint manually** to verify response format
4. **UPDATE integration test assertions** if API returns different format than expected
5. Generate startup instructions
6. Document port assignments
7. List all files created
8. Provide next steps for manual testing
3. Create startup instructions in domain README

---

## Code Generation Standards

### Following Constitutional Principles

**Principle I: Domain Language Consistency**
- Use domain terminology from business requirements
- No technical jargon in domain layer
- Consistent naming across all files

**Principle II: Single-Partition Data Model**
- Cosmos DB container partitioned by processing unit ID
- Document type discriminator field
- Co-located related documents

**Principle III: Atomic State Transitions**
- Use ETags for optimistic concurrency
- Update counts atomically with state changes

**Principle IV: Idempotent Message Handlers**
- Check for existing state before creating
- Handle duplicate messages gracefully
- Log idempotency skips

**Principle V: Structured Observability**
- Include correlation IDs in all logs
- Log entity identifiers
- Use structured logging (not string concatenation)

**Principle VI: Message-Based Integration**
- Commands published to Service Bus
- Events for cross-domain communication
- All messages include MessageId, OccurredUtc, IdempotencyKey

**Principle VII: Thin Message Handlers**
- Validate → Delegate to Manager → Publish events
- No business logic in handlers

**Principle VIII: Test Coverage Requirements**
- Domain layer: 90%+ coverage
- Application layer: 80%+ coverage
- Integration tests for all APIs

**Principle IX: Technology Constraints**
- .NET 10, C# 13
- NServiceBus 9.x
- Cosmos DB (no EF Core)
- xUnit for tests

**Principle X: Naming Conventions**
- Commands: Verb + Noun
- Events: Noun + VerbPastTense
- Handlers: MessageName + Handler

---

## File Templates

### Domain Entity (Customer Example)
```csharp
namespace RiskInsure.{Domain}.Domain.Models;

using System.Text.Json.Serialization;

public class {Entity}
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    
    [JsonPropertyName("{partitionKey}")]
    public string {PartitionKey} { get; set; }  // Partition key
    
    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = "{Entity}";
    
    // Business fields from technical spec
    
    [JsonPropertyName("createdUtc")]
    public DateTimeOffset CreatedUtc { get; set; }
    
    [JsonPropertyName("updatedUtc")]
    public DateTimeOffset UpdatedUtc { get; set; }
    
    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }
}
```

### Event Contract
```csharp
namespace RiskInsure.{Domain}.Domain.Contracts.Events;

public record {Entity}{Action}(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string {Entity}Id,
    // Business fields
    string IdempotencyKey
);
```

### Repository Interface
```csharp
namespace RiskInsure.{Domain}.Domain.Repositories;

public interface I{Entity}Repository
{
    Task<{Entity}?> GetByIdAsync(string {entity}Id);
    Task<{Entity}> CreateAsync({Entity} {entity});
    Task<{Entity}> UpdateAsync({Entity} {entity});
    Task DeleteAsync(string {entity}Id);
}
```

### API Controller
```csharp
namespace RiskInsure.{Domain}.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using RiskInsure.{Domain}.Domain.Managers;

[ApiController]
[Route("api/{route}")]
[Produces("application/json")]
public class {Entity}Controller : ControllerBase
{
    private readonly I{Entity}Manager _manager;
    private readonly ILogger<{Entity}Controller> _logger;

    public {Entity}Controller(
        I{Entity}Manager manager,
        ILogger<{Entity}Controller> logger)
    {
        _manager = manager;
        _logger = logger;
    }

    // Endpoints from technical spec
}
```

### Message Handler
```csharp
namespace RiskInsure.{Domain}.Endpoint.In.Handlers;

using NServiceBus;
using RiskInsure.PublicContracts.Events;

public class {Event}Handler : IHandleMessages<{Event}>
{
    private readonly I{Entity}Manager _manager;
    private readonly ILogger<{Event}Handler> _logger;

    public async Task Handle({Event} message, IMessageHandlerContext context)
    {
        // Idempotency check
        // Call manager
        // Publish events
    }
}
```

### Service Bus Queue Setup Script (queues.sh)
```bash
#!/bin/bash
# Azure Service Bus Queue Setup for {Domain} Service
# Run this script after creating a Service Bus namespace to set up queues and subscriptions

set -e  # Exit on error

# Check if connection string is set
if [ -z "$AzureServiceBus_ConnectionString" ]; then
    echo "❌ Error: AzureServiceBus_ConnectionString environment variable is not set"
    echo ""
    echo "Set it with:"
    echo "  export AzureServiceBus_ConnectionString='Endpoint=sb://YOUR-NAMESPACE.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=YOUR-KEY'"
    echo ""
    exit 1
fi

echo "========================================="
echo " Setting up {Domain} Service Queues"
echo "========================================="
echo ""

# Shared error and audit queues (run once per namespace)
echo "Creating shared infrastructure queues..."
asb-transport queue create error || echo "  ℹ️  Queue 'error' may already exist"
asb-transport queue create audit || echo "  ℹ️  Queue 'audit' may already exist"
asb-transport queue create particular.monitoring || echo "  ℹ️  Queue 'particular.monitoring' may already exist"
echo ""

# {Domain} service endpoint
echo "Creating {Domain} endpoint..."
asb-transport endpoint create RiskInsure.{Domain}.Endpoint
echo ""

# {Domain} service subscriptions (add one line per PublicContracts event subscribed to)
echo "Creating {Domain} subscriptions..."
# asb-transport endpoint subscribe RiskInsure.{Domain}.Endpoint RiskInsure.PublicContracts.Events.{EventName}
echo "  ℹ️  No cross-domain subscriptions for {Domain} service"

echo ""
echo "✅ {Domain} service queues created successfully!"
```

**⚠️ IMPORTANT**: When creating new domains:
1. Create `queues.sh` in `services/{domain}/src/Infrastructure/`
2. Make it executable: `chmod +x queues.sh`
3. Add `asb-transport endpoint subscribe` lines for each PublicContracts event the domain subscribes to
4. Document internal domain events (they don't need Service Bus subscriptions)

The agent **EXECUTES AND VERIFIES** (not just checks):

### Build & Compilation ✅ AUTOMATED
- [x] **RUN** `dotnet restore` - verify exit code 0
- [x] **RUN** `dotnet build` on each project - verify exit code 0
- [x] **VERIFY** solution file includes all projects
- [x] **CHECK** no circular dependencies (build failure would indicate)

### Configuration ✅ AUTOMATED
- [x] **SCAN** existing services for port conflicts
- [x] **ASSIGN** next available ports
- [x] **CREATE** launchSettings.json in both API and Endpoint.In
- [x] **SET** DOTNET_ENVIRONMENT=Development in Endpoint.In
- [x] **COPY** connection strings from existing services
- [x] **CREATE** actual appsettings.Development.json files
- [x] **CREATE** queues.sh script in Infrastructure folder with correct subscriptions
- [x] **MAKE** queues.sh executable (chmod +x)

### Code Quality ✅ AUTOMATED
- [x] All classes use correct RiskInsure.{Domain}.* namespaces
- [x] Dependency injection configured in Program.cs
- [x] Structured logging with correlation fields
- [x] ETag optimistic concurrency in repository
- [x] Idempotency checks in handlers

### Tests ✅ AUTOMATED
- [x] **RUN** `dotnet test` - verify all unit tests pass
- [x] **INSTALL** Playwright dependencies via npm
- [x] Integration test files created
- [x] Test README with run instructions

### Documentation ✅ AUTOMATED
- [x] API endpoints match technical spec
- [x] Event contracts follow naming conventions
- [x] Startup instructions in output summary
- [ ] Solution file includes all projects
- [ ] Package references resolve correctly
- [ ] No circular dependencies

### agent **VERIFIES** domain is complete when:
1. ✅ All files created (50+ files across 5 layers)
2. ✅ **BUILD VERIFIED**: `dotnet build` exit code 0 for all projects
3. ✅ **TESTS VERIFIED**: `dotnet test` exit code 0, all tests pass
4. ✅ **SOLUTION UPDATED**: All projects added to RiskInsure.slnx
5. ✅ **PORTS ASSIGNED**: launchSettings.json created with conflict-free ports
6. ✅ **CONFIG COPIED**: appsettings.Development.json created from existing services
7. ✅ **PLAYWRIGHT READY**: npm install completed successfully
8. ✅ Startup instructions provided for manual API testing
9. ✅ No constitutional principle violations in generated code
10. ✅ Summary report with file counts and next steps

**The agent does NOT manually start services** - connection strings may need validation.fields
- [ ] ETag optimistic concurrency implemented
- [ ] Idempotency checks in place

### Tests
- [ ] Unit tests created and pass
- [ ] Integration tests created with realistic scenarios
- [ ] **Integration test assertions match actual API response format** (ProblemDetails for validation errors)
- [ ] Validation error tests expect `errors.FieldName` as arrays, not strings
- [ ] Playwright config points to correct port
- [ ] Test README documents how to run

### Documentation
- [ ] API endpoints match technical spec table
- [ ] Event contracts match specification
- [ ] README created with startup instructions

---

## Success Criteria

The domain is complete when:
1. ✅ All 5 layers created (Domain, Infrastructure, API, Endpoint.In, Tests)
2. ✅ `dotnet build` succeeds for all projects
3. ✅ Unit tests pass with 80%+ coverage
4. ✅ API starts on assigned port
5. ✅ Endpoint.In starts without "Production requires..." error
6. ✅ Integration tests pass (Playwright UI mode)
7. ✅ Swagger/Scalar UI accessible at `/scalar/v1`
8. ✅ Events publish to Service Bus
9. ✅ Cosmos DB container created with correct partition key
10. ✅ No constitutional principle violations

---
 **VERIFIED RESULTS**:

1. **Build Verification Report**
   ```
   ✅ Domain Layer: BUILT (12 files created)
   ✅ Infrastructure Layer: BUILT (2 files created)
   ✅ API Layer: BUILT (8 files created)
   ✅ Endpoint.In Layer: BUILT (5 files created)
   ✅ Unit Tests: BUILT & PASSED (X tests, Y assertions)
   ✅ Integration Tests: READY (Playwright installed)
   ✅ Solution Updated: All 5 projects added
   ```

2. **Port Assignments**
   - API: http://localhost:{port}
   - Endpoint.In: {port+1}
   - No conflicts detected

3. **Build Logs**
   - Restore output: `dotnet restore` exit code
   - Build output: Each project build status
   - Test output: Test results summary

4. **Startup Instructions**
   ```powershell
   # Verify connection strings first
   code services/{domain}/src/Api/appsettings.Development.json
   code services/{domain}/src/Endpoint.In/appsettings.Development.json
   
   # Terminal 1: Start API
   cd services/{domain}/src/Api
The agent **EXECUTES** error recovery:

**Port Conflict**:
- **SCAN** all services using `Get-ChildItem` PowerShell command
- **IDENTIFY** next available port in 707X range
- **ASSIGN** and document new ports

**Build Errors**:
- **CAPTURE** build output and error messages
- **REPORT** specific file and line number
- **CHECK** Directory.Packages.props for version mismatches
- **VERIFY** project references are correct
- **SUGGEST** fixes based on error type

**Missing Specification**:
- **READ** technical spec file
- **VALIDATE** contains required sections (API table, data models, events)
- **PROMPT** user if missing critical information

**Test Failures**:
- **CAPTURE** test output
- **REPORT** which tests failed and why
- **CONTINUE** with remaining steps (don't halt on test failures)

**Connection String Issues**:
- **COPY** from existing services (billing/fundstransfermgt)
- **WARN** if connection strings are templates (contain `<<>>`)
- **PROVIDE** instructions to update with actual valuesequired (connection strings)
   - Integration points with other domains
   - Future enhancements from technical spec

---

## Error Handling

If agent encounters issues:

**Port Conflict**:
- Scan all services for ports
- Suggest next available port
- Update launchSettings.json

**Missing Specification**:
- Prompt user for technical spec path
- Validate spec has API table and data models

**Build Errors**:
- Check package versions in Directory.Packages.props
- Verify project references
- Report specific error with file location

**Cosmos Connection Issues**:
- Verify emulator running on localhost:8081
- Provide connection string template
- Suggest starting emulator

---

## Reference Documents
Index the solution since there are additions into other projects for cross domain events.

The agent uses these sources:
1. **[copilot-instructions/project-structure.md](../copilot-instructions/project-structure.md)** - Layer responsibilities
2. **[.specify/memory/constitution.md](../.specify/memory/constitution.md)** - Core principles
3. **[copilot-instructions/init.api.md](../copilot-instructions/init.api.md)** - API project setup
4. **Technical specification** - Domain-specific requirements
5. **Existing implementations** - Billing and FundsTransferMgt as patterns

---

## Example Usage

See [domain-builder.prompt.md](../prompts/domain-builder.prompt.md) for invocation examples.

---

**Version**: 1.0.0  
**Last Updated**: February 5, 2026  
**Maintained By**: Architecture Team
