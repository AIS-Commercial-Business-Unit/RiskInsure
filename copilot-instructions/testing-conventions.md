# Testing Conventions

**Version**: 1.0.0 | **Last Updated**: 2026-02-04

This document defines the standard testing structure and setup for all services in the RiskInsure repository.

---

## Critical Rule: API-Test Synchronization

**MUST**: Integration test endpoints MUST match actual controller routes exactly.

**When to Update Tests**:
- ✅ When adding new API endpoints
- ✅ When modifying route paths (e.g., `/cards` → `/credit-card`)
- ✅ When changing HTTP methods
- ✅ When modifying request/response contracts
- ✅ When adding/removing query parameters

**Verification Steps**:
1. Compare test file routes with controller `[HttpMethod]` and `[Route]` attributes
2. Verify request DTOs match controller parameters
3. Verify response assertions match controller return types
4. Run integration tests after ANY API controller change

**Example Mismatch** (WRONG):
```typescript
// Test uses /cards but controller defines /credit-card
await request.post(`${baseUrl}/payment-methods/cards`); // ❌ Wrong
```

**Correct Alignment**:
```csharp
// Controller: PaymentMethodsController.cs
[HttpPost("credit-card")] // Route is /api/payment-methods/credit-card
```
```typescript
// Test: payment-method-lifecycle.spec.ts
await request.post(`${baseUrl}/payment-methods/credit-card`); // ✅ Correct
```

---

## Test Directory Structure

Each service MUST have two separate test projects under `services/{ServiceName}/test/`:

```
services/
└── {ServiceName}/
    ├── src/
    │   ├── Api/
    │   ├── Domain/
    │   ├── Infrastructure/
    │   └── Endpoint.In/
    └── test/
        ├── Unit.Tests/              # xUnit unit tests (.NET)
        │   ├── Unit.Tests.csproj
        │   └── Managers/
        │       └── *ManagerTests.cs
        └── Integration.Tests/       # Playwright API tests (Node.js)
            ├── package.json
            ├── playwright.config.ts
            ├── README.md
            └── tests/
                └── *.spec.ts
```

---

## Unit Tests (xUnit)

### Purpose
Test business logic, domain rules, and manager orchestration **without external dependencies**.

### Project Setup

**File**: `test/Unit.Tests/Unit.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="FluentAssertions" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Moq" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="FluentAssertions" />
    <Using Include="Moq" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Domain\Domain.csproj" />
  </ItemGroup>
</Project>
```

### Test File Pattern

**Naming**: `{ClassName}Tests.cs`  
**Location**: `test/Unit.Tests/{Layer}/{ClassName}Tests.cs`  
**Namespace**: `RiskInsure.{ServiceName}.Unit.Tests.{Layer}`

**Example**:
```csharp
namespace RiskInsure.Billing.Unit.Tests.Managers;

public class BillingPaymentManagerTests
{
    private readonly Mock<IRepository> _mockRepository;
    private readonly Mock<IMessageSession> _mockMessageSession;
    private readonly Mock<ILogger<Manager>> _mockLogger;
    private readonly Manager _manager;

    public ManagerTests()
    {
        _mockRepository = new Mock<IRepository>();
        _mockMessageSession = new Mock<IMessageSession>();
        _mockLogger = new Mock<ILogger<Manager>>();
        
        _manager = new Manager(
            _mockRepository.Object,
            _mockMessageSession.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task MethodName_Scenario_ExpectedResult()
    {
        // Arrange
        var input = new TestData();
        _mockRepository.Setup(r => r.GetAsync(It.IsAny<string>()))
            .ReturnsAsync(expectedValue);

        // Act
        var result = await _manager.MethodAsync(input);

        // Assert
        result.Should().NotBeNull();
        result.Property.Should().Be(expectedValue);
        _mockRepository.Verify(r => r.SaveAsync(It.IsAny<Entity>()), Times.Once);
    }
}
```

### Running Unit Tests

```bash
# From service root
cd services/{ServiceName}/test/Unit.Tests
dotnet test

# With coverage
dotnet test --collect:"XPlat Code Coverage"

# Specific test
dotnet test --filter "FullyQualifiedName~MethodName"
```

---

## Integration Tests (Playwright)

### Purpose
Test HTTP API endpoints end-to-end with real API running, validating request/response contracts and business workflows.

### First-Time Setup

```bash
cd services/{ServiceName}/test/Integration.Tests

# 1. Create package.json
npm init -y

# 2. Install Playwright
npm install -D @playwright/test @types/node

# 3. Install browsers (first time only)
npx playwright install chromium

# 4. Create playwright.config.ts (see template below)
# 5. Create tests/ directory
mkdir tests
```

### Required Files

#### package.json

```json
{
  "name": "{servicename}-integration-tests",
  "version": "1.0.0",
  "description": "Playwright integration tests for {ServiceName} API",
  "scripts": {
    "test": "playwright test",
    "test:headed": "playwright test --headed",
    "test:debug": "playwright test --debug",
    "test:report": "playwright show-report",
    "test:ui": "playwright test --ui"
  },
  "devDependencies": {
    "@playwright/test": "^1.48.0",
    "@types/node": "^22.0.0"
  }
}
```

#### playwright.config.ts

```typescript
import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  timeout: 60 * 1000, // 60 seconds
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  
  reporter: [
    ['html'],
    ['list'],
    ['junit', { outputFile: 'test-results/junit.xml' }]
  ],
  
  use: {
    baseURL: process.env.API_BASE_URL || 'http://localhost:707X/api',
    trace: 'on-first-retry',
    extraHTTPHeaders: {
      'Accept': 'application/json',
      'Content-Type': 'application/json'
    }
  },

  projects: [
    { name: 'api-tests', testMatch: '**/*.spec.ts' }
  ],
});
```

**Replace** `707X` with your service's port (e.g., 7071 for billing, 7075 for payments).

#### README.md Template

```markdown
# Integration Tests

Playwright-based integration tests for {ServiceName} API.

## Prerequisites
- Node.js 18+
- API running on `http://localhost:707X`
- Cosmos DB Emulator running locally

## Setup
\`\`\`bash
npm install
npx playwright install chromium
\`\`\`

## Running Tests
\`\`\`bash
# Start API first
cd ../../src/Api
dotnet run

# In another terminal
cd test/Integration.Tests
npm test              # Headless
npm run test:ui       # Interactive (recommended)
npm run test:headed   # Browser visible
\`\`\`
```

### Test File Pattern

**Naming**: `{feature}-lifecycle.spec.ts`  
**Location**: `test/Integration.Tests/tests/{feature}.spec.ts`

**Example**:
```typescript
import { test, expect } from '@playwright/test';
import { randomUUID } from 'crypto';

test.describe('Feature Lifecycle', () => {
  let entityId: string;
  const baseUrl = process.env.API_BASE_URL || 'http://localhost:707X/api';

  test.beforeEach(() => {
    entityId = randomUUID();
  });

  test('Complete workflow', async ({ request }) => {
    // Create
    const createResponse = await request.post(`${baseUrl}/entities`, {
      data: { id: entityId, name: 'Test' }
    });
    expect(createResponse.status()).toBe(201);

    // Verify
    const getResponse = await request.get(`${baseUrl}/entities/${entityId}`);
    expect(getResponse.status()).toBe(200);
    const entity = await getResponse.json();
    expect(entity.id).toBe(entityId);
  });
});
```

### Running Integration Tests

```bash
# Start API (required)
cd services/{ServiceName}/src/Api
dotnet run

# In separate terminal - run tests
cd services/{ServiceName}/test/Integration.Tests
npm test                  # Headless
npm run test:ui          # Interactive UI (RECOMMENDED)
npm run test:headed      # Browser visible
npm run test:debug       # Step-through debugger
npm run test:report      # View HTML report
```

---

## Port Assignment

Each service uses consistent port numbers:

| Service | API Port | Endpoint.In Port |
|---------|----------|------------------|
| Billing | 7071 | 7072 |
| Payments | 7075 | 7076 |
| Medical | 7073 | 7074 |

**Rule**: API uses `707X`, Endpoint.In uses `707X+1`

---

## Common Patterns

### Unit Test Patterns

**Mock Setup**:
```csharp
_mockRepository.Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(testEntity);
```

**Verify Calls**:
```csharp
_mockRepository.Verify(r => r.CreateAsync(
    It.Is<Entity>(e => e.Id == expectedId), 
    It.IsAny<CancellationToken>()), 
    Times.Once);
```

**FluentAssertions**:
```csharp
result.Should().NotBeNull();
result.Status.Should().Be(ExpectedStatus.Active);
result.Amount.Should().BeGreaterThan(0);
```

### Integration Test Patterns

**Unique Test Data**:
```typescript
const entityId = randomUUID();
const customerId = `CUST-${Date.now()}`;
```

**API Calls with Assertions**:
```typescript
const response = await request.post(`${baseUrl}/entities`, {
  data: { id: entityId, value: 100 }
});
expect(response.status()).toBe(201);
const body = await response.json();
expect(body.id).toBe(entityId);
```

**Multi-Step Workflows**:
```typescript
// 1. Create
await request.post(...);

// 2. Verify created
const getResponse = await request.get(...);
expect(getResponse.status()).toBe(200);

// 3. Update
await request.put(...);

// 4. Verify updated
const finalResponse = await request.get(...);
expect(finalResponse.status()).toBe(200);
```

---

## Checklist for New Service

### Unit Tests Setup
- [ ] Create `test/Unit.Tests/` directory
- [ ] Create `Unit.Tests.csproj` with proper references
- [ ] Add using statements (Xunit, FluentAssertions, Moq)
- [ ] Create `Managers/` subdirectory
- [ ] Write tests for each manager class
- [ ] Run: `dotnet test`

### Integration Tests Setup
- [ ] Create `test/Integration.Tests/` directory
- [ ] Run: `npm init -y` to create package.json
- [ ] Install Playwright: `npm install -D @playwright/test @types/node`
- [ ] Create `playwright.config.ts` (use template, update port)
- [ ] Install browsers: `npx playwright install chromium`
- [ ] Create `tests/` directory
- [ ] Create README.md with setup instructions
- [ ] Write lifecycle tests for main workflows
- [ ] Verify: Start API, run `npm run test:ui`

### Common Mistakes to Avoid
- ❌ Don't mix unit and integration tests in one project
- ❌ Don't forget to update port numbers in playwright.config.ts
- ❌ Don't commit `node_modules/` or `playwright-report/`
- ❌ Don't run integration tests without API running
- ❌ Don't forget unique test data (GUIDs, timestamps)

---

## Related Documents

- [testing-standards.md](testing-standards.md) - Testing best practices and coverage requirements
- [playwright-integration-testing.md](playwright-integration-testing.md) - Detailed Playwright patterns
- [constitution.md](../.specify/memory/constitution.md) - Test coverage thresholds (Principle VIII)
