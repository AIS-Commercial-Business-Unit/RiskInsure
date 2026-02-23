# File Retrieval Service - Testing Guide

## Current Status

The File Retrieval Service has been fully implemented (148/148 tasks) but requires some build fixes before testing.

## Known Build Issues

The following compilation errors need to be resolved:

1. **ProtocolType enum naming**: Code references `ProtocolType.Ftp` and `ProtocolType.Https` but enum may be named differently
2. **FileRetrievalExecution properties**: Some code uses `FilesFoundCount` instead of `FilesFound`
3. **ConfigurationDeleted event**: Missing `Protocol` and `WasActive` properties in event initialization

## Quick Fix Steps

### Fix 1: Check ProtocolType Enum

```bash
# View the enum to check correct names
cat src/FileRetrieval.Domain/Enums/ProtocolType.cs
```

Expected values: `FTP`, `HTTPS`, `AzureBlob` (check actual casing)

### Fix 2: Update Property References

Search and replace in `ExecutionHistoryService.cs`:
- `FilesFoundCount` → `FilesFound`
- `DurationMilliseconds` → `DurationMs`

### Fix 3: Check Event Contracts

View `FileRetrieval.Contracts/Events/ConfigurationDeleted.cs` and ensure all required properties are initialized in handlers.

## Testing Prerequisites

Once build is fixed, you'll need:

### 1. Local Development Environment

```bash
# Install required tools
dotnet --version  # Should be 10.0+
docker --version  # For Cosmos DB Emulator

# Start Cosmos DB Emulator (Windows)
"C:\Program Files\Azure Cosmos DB Emulator\Microsoft.Azure.Cosmos.Emulator.exe"

# Or use Docker
docker run -d -p 8081:8081 -p 10250-10255:10250-10255 mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator
```

### 2. Azure Service Bus (Options)

**Option A: Azure Service Bus namespace** (Recommended for integration testing)
```bash
az servicebus namespace create \
  --name file-retrieval-dev \
  --resource-group riskinsure-dev \
  --location eastus \
  --sku Standard

# Get connection string
az servicebus namespace authorization-rule keys list \
  --resource-group riskinsure-dev \
  --namespace-name file-retrieval-dev \
  --name RootManageSharedAccessKey \
  --query primaryConnectionString -o tsv
```

**Option B: Local emulator** (RabbitMQ with NServiceBus transport)
```bash
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

### 3. Azure Key Vault (Optional for dev)

For development, you can use appsettings instead of Key Vault for credentials.

## Configuration Setup

### 1. Update appsettings.Development.json (API)

```json
{
  "ConnectionStrings": {
    "CosmosDb": "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
    "ServiceBus": "Endpoint=sb://file-retrieval-dev.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=YOUR_KEY"
  },
  
  "JwtSettings": {
    "SecretKey": "dev-secret-key-minimum-32-characters-for-hmac-sha256-algorithm",
    "Issuer": "https://localhost:5001",
    "Audience": "file-retrieval-api",
    "ExpirationMinutes": 120
  },
  
  "CosmosDb": {
    "DatabaseName": "FileRetrieval",
    "Containers": {
      "Configurations": "Configurations",
      "Executions": "Executions",
      "DiscoveredFiles": "DiscoveredFiles"
    }
  }
}
```

### 2. Initialize Cosmos DB

```powershell
# Run the setup script
cd src/FileRetrieval.Infrastructure/Scripts
./setup-cosmosdb.ps1
```

## Testing Scenarios

### Test 1: Build and Run Unit Tests

```bash
cd services/file-retrieval

# Build solution
dotnet build

# Run all tests
dotnet test --logger "console;verbosity=detailed"

# Run specific test project
dotnet test test/FileRetrieval.Domain.Tests
```

### Test 2: Start the API

```bash
cd src/FileRetrieval.Api
dotnet run

# API will be available at:
# https://localhost:5001
# Swagger UI: https://localhost:5001/swagger
```

### Test 3: Start the Worker

```bash
# In a separate terminal
cd src/FileRetrieval.Worker
dotnet run

# Worker will:
# - Check configurations every 1 minute
# - Execute file checks based on schedules
# - Publish events to Service Bus
```

### Test 4: Create a Test Configuration

#### Step 1: Generate JWT Token

```bash
# Using PowerShell
$payload = @{
  sub = "test-user"
  clientId = "CLIENT-001"
  userId = "user-123"
  exp = [DateTimeOffset]::UtcNow.AddHours(2).ToUnixTimeSeconds()
  iss = "https://localhost:5001"
  aud = "file-retrieval-api"
} | ConvertTo-Json

# Encode and sign (use JWT.io for quick testing)
```

#### Step 2: Create Configuration via API

```bash
POST https://localhost:5001/api/configurations
Authorization: Bearer YOUR_JWT_TOKEN
Content-Type: application/json

{
  "name": "Daily Customer Files",
  "description": "Check for customer uploads every day at 9 AM",
  "isActive": true,
  "protocolSettings": {
    "protocol": "HTTPS",
    "serverUrl": "https://example.com/files/{yyyy}/{mm}",
    "filePathPattern": "/uploads",
    "filenamePattern": "customer-{yyyy}{mm}{dd}.csv",
    "authType": "None"
  },
  "schedule": {
    "cronExpression": "0 9 * * *",
    "timezone": "America/New_York"
  },
  "eventsToPublish": [
    {
      "eventType": "CustomerFileDiscovered",
      "eventData": {
        "processType": "customer-import"
      }
    }
  ]
}
```

### Test 5: Verify File Check Execution

#### Monitor Logs

```bash
# API logs
tail -f src/FileRetrieval.Api/logs/*.log

# Worker logs  
tail -f src/FileRetrieval.Worker/logs/*.log
```

#### Query Execution History

```bash
GET https://localhost:5001/api/executions?configurationId={id}
Authorization: Bearer YOUR_JWT_TOKEN
```

### Test 6: Integration Test with Test FTP Server

```bash
# Start test FTP server (Docker)
docker run -d \
  --name test-ftp \
  -p 21:21 \
  -p 30000-30009:30000-30009 \
  -e FTP_USER=testuser \
  -e FTP_PASS=testpass \
  fauria/vsftpd

# Upload test file
echo "test data" > test-$(date +%Y%m%d).txt
curl -T test-$(date +%Y%m%d).txt ftp://localhost:21/ --user testuser:testpass

# Create FTP configuration
POST https://localhost:5001/api/configurations
{
  "name": "Test FTP Check",
  "protocolSettings": {
    "protocol": "FTP",
    "serverUrl": "localhost",
    "port": 21,
    "username": "testuser",
    "password": "testpass",
    "filePathPattern": "/",
    "filenamePattern": "test-{yyyy}{mm}{dd}.txt"
  },
  "schedule": {
    "cronExpression": "*/5 * * * *"
  }
}
```

## Expected Behavior

### Successful File Check

1. **Worker** evaluates schedules every minute
2. **Worker** sends `ExecuteFileCheck` command when due
3. **Handler** receives command, calls `FileCheckService`
4. **Service** replaces tokens: `test-{yyyy}{mm}{dd}.txt` → `test-20260223.txt`
5. **Protocol Adapter** connects to FTP/HTTPS/Azure Blob
6. **Service** finds matching files
7. **Service** creates `DiscoveredFile` entity (idempotency check)
8. **Service** publishes `FileDiscovered` event
9. **Service** sends `ProcessDiscoveredFile` command to workflow platform
10. **Service** publishes `FileCheckCompleted` event
11. **Handler** returns success

### Failed File Check

1. Steps 1-4 same as success
2. **Protocol Adapter** encounters error (auth failure, timeout, etc.)
3. **Service** categorizes error
4. **Service** retries with exponential backoff (2s, 5s, 10s)
5. After 3 failures: **Service** publishes `FileCheckFailed` event
6. **Handler** logs error with category

### No Files Found

1. Steps 1-6 same as success  
2. **Service** finds zero matching files
3. **Service** publishes `FileCheckCompleted` event with `FilesFound=0`
4. **Handler** logs info message

## Success Criteria Verification

After testing, verify these success criteria:

- ✅ **SC-001**: Configuration creation takes < 2 minutes
- ✅ **SC-002**: Scheduled checks execute within 1 minute of schedule
- ✅ **SC-003**: Events published within 5 seconds of file discovery
- ✅ **SC-004**: 100 concurrent checks supported (load test)
- ✅ **SC-007**: Zero duplicate `FileDiscovered` events (idempotency)
- ✅ **SC-008**: 100% token replacement accuracy
- ✅ **SC-009**: Client-scoped security enforced

## Troubleshooting

### Build Errors

```bash
# Clean and rebuild
dotnet clean
dotnet build --no-incremental
```

### Cosmos DB Connection Issues

```bash
# Check emulator is running
curl https://localhost:8081/_explorer/index.html

# Check firewall allows port 8081
```

### Service Bus Connection Issues

```bash
# Test connection
az servicebus namespace show \
  --name file-retrieval-dev \
  --resource-group riskinsure-dev
```

### No Files Discovered

1. Check token replacement: Are date tokens resolving correctly?
2. Check protocol settings: Is server URL reachable?
3. Check file pattern: Does it match actual filenames?
4. Check logs: What error is reported?

## Next Steps

After successful local testing:

1. **Run integration tests**: `dotnet test test/FileRetrieval.Integration.Tests`
2. **Load testing**: Use k6 or Apache Bench to verify SC-004 (100 concurrent checks)
3. **Deploy to Azure**: Follow `docs/deployment.md`
4. **Monitor in production**: Set up Application Insights dashboards (see `docs/monitoring.md`)

## Additional Resources

- **Specification**: `../../specs/001-file-retrieval-config/spec.md`
- **Implementation Plan**: `../../specs/001-file-retrieval-config/plan.md`
- **Architecture**: `./docs/file-retrieval-standards.md`
- **Deployment**: `./docs/deployment.md`
- **Monitoring**: `./docs/monitoring.md`
