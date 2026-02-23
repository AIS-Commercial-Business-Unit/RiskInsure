# Quickstart Guide: Client File Retrieval Configuration

**Feature**: 001-file-retrieval-config  
**Date**: 2025-01-24  
**Audience**: Developers onboarding to this feature

## Overview

This guide helps developers quickly understand, build, and test the Client File Retrieval Configuration feature. It provides step-by-step instructions for local development, testing, and integration with the workflow orchestration platform.

---

## Architecture Summary

The File Retrieval feature is a **new bounded context** that:
- Automatically checks client file locations (FTP, HTTPS, Azure Blob Storage) on scheduled intervals
- Detects files matching configured patterns with date-based tokens (`{yyyy}`, `{mm}`, `{dd}`, `{yy}`)
- Publishes events and sends commands to the workflow orchestration platform when files are discovered
- Enforces client-scoped security trimming (multi-tenancy)
- Tracks discovered files to prevent duplicate workflow triggers (idempotency)

**Key Components**:
1. **FileRetrieval.API** - REST API for CRUD operations on configurations
2. **FileRetrieval.Worker** - Background service that executes scheduled file checks
3. **FileRetrieval.Domain** - Business entities and rules
4. **FileRetrieval.Application** - Services, protocol adapters, message handlers
5. **FileRetrieval.Infrastructure** - Cosmos DB repositories, scheduling

---

## Prerequisites

### Required Tools
- **.NET 10.0 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Visual Studio 2022** (17.12+) or **VS Code** with C# extension
- **Azure CLI** - For local Cosmos DB and Service Bus emulation
- **Docker Desktop** - For local dependencies (Cosmos DB emulator, Azurite)

### Required Azure Resources (Local Development)
- **Cosmos DB Emulator** - [Install](https://learn.microsoft.com/en-us/azure/cosmos-db/how-to-develop-emulator)
- **Azure Service Bus Emulator** (or use development Service Bus namespace)
- **Azurite** - For Azure Blob Storage emulation: `npm install -g azurite`

### Optional Tools
- **Postman** or **REST Client** (VS Code extension) - For API testing
- **Azure Storage Explorer** - For viewing Cosmos DB data
- **Service Bus Explorer** - For viewing Service Bus messages

---

## Project Setup

### 1. Clone Repository

```bash
git clone https://github.com/riskinsure/riskinsure-platform.git
cd riskinsure-platform
git checkout 001-file-retrieval-config
```

### 2. Restore Dependencies

```bash
cd services/file-retrieval
dotnet restore
```

### 3. Start Local Dependencies

**Option A: Docker Compose (Recommended)**

```bash
cd docker
docker-compose up -d cosmos-emulator azurite
```

**Option B: Manual Setup**

```bash
# Start Cosmos DB Emulator (Windows)
"C:\Program Files\Azure Cosmos DB Emulator\Microsoft.Azure.Cosmos.Emulator.exe"

# Start Azurite (Azure Storage Emulator)
azurite --silent --location c:\azurite --debug c:\azurite\debug.log
```

### 4. Configure Local Settings

**FileRetrieval.API/appsettings.Development.json**:
```json
{
  "CosmosDb": {
    "Endpoint": "https://localhost:8081",
    "Key": "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
    "DatabaseName": "FileRetrieval"
  },
  "ServiceBus": {
    "ConnectionString": "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=<your-key>"
  },
  "KeyVault": {
    "VaultUri": "https://localhost:8200" // Local Key Vault mock
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "FileRetrieval": "Debug"
    }
  }
}
```

**FileRetrieval.Worker/appsettings.Development.json**: Same as above

### 5. Initialize Database

Run the setup script to create Cosmos DB containers with partition keys and unique constraints:

```bash
cd scripts
dotnet run --project DatabaseSetup -- --environment Development
```

This creates:
- Container: `file-retrieval-configurations` (partition key: `/clientId`)
- Container: `file-retrieval-executions` (partition key: `/clientId/configurationId`)
- Container: `discovered-files` (partition key: `/clientId/configurationId`, unique key: `(clientId, configurationId, fileUrl, discoveryDate)`)

---

## Build and Run

### Build All Projects

```bash
cd services/file-retrieval
dotnet build
```

### Run API (REST endpoints)

```bash
cd FileRetrieval.API
dotnet run
```

API will start at: `https://localhost:5001` (or configured port)

### Run Worker (Background scheduler)

```bash
cd FileRetrieval.Worker
dotnet run
```

Worker will:
- Poll configurations every minute
- Execute file checks for due schedules
- Publish events/commands when files discovered

---

## Testing the Feature

### 1. Create a Configuration (API)

**Request**: `POST https://localhost:5001/api/configurations`

**Headers**:
```
Authorization: Bearer <your-jwt-token>
Content-Type: application/json
```

**Body** (FTP Example):
```json
{
  "name": "Daily Transaction Files",
  "description": "Check for daily transaction files from ACME Corp FTP",
  "protocol": "Ftp",
  "protocolSettings": {
    "server": "ftp.testserver.com",
    "port": 21,
    "username": "testuser",
    "passwordKeyVaultSecret": "test-ftp-password",
    "useTls": true,
    "usePassiveMode": true,
    "connectionTimeout": "00:00:30"
  },
  "filePathPattern": "/transactions/{yyyy}/{mm}",
  "filenamePattern": "trans_{yyyy}{mm}{dd}.csv",
  "fileExtension": "csv",
  "schedule": {
    "cronExpression": "0 8 * * *",
    "timezone": "America/New_York",
    "description": "Daily at 8:00 AM ET"
  },
  "eventsToPublish": [
    {
      "eventType": "FileDiscovered",
      "eventData": {
        "fileType": "Transaction",
        "priority": "High"
      }
    }
  ],
  "commandsToSend": [
    {
      "commandType": "ProcessTransactionFile",
      "targetEndpoint": "WorkflowOrchestrator",
      "commandData": {
        "workflowType": "TransactionProcessing"
      }
    }
  ]
}
```

**Response**: `201 Created`
```json
{
  "id": "e3a85f64-5717-4562-b3fc-2c963f66afa6",
  "clientId": "client-acme-corp",
  "name": "Daily Transaction Files",
  "isActive": true,
  "nextScheduledRun": "2025-01-25T13:00:00Z",
  "createdAt": "2025-01-24T10:00:00Z"
}
```

### 2. List Configurations (API)

**Request**: `GET https://localhost:5001/api/configurations`

**Response**: `200 OK`
```json
{
  "items": [
    {
      "id": "e3a85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "Daily Transaction Files",
      "protocol": "Ftp",
      "isActive": true,
      "nextScheduledRun": "2025-01-25T13:00:00Z"
    }
  ],
  "totalCount": 1
}
```

### 3. Manually Trigger File Check (API)

For testing without waiting for schedule:

**Request**: `POST https://localhost:5001/api/configurations/{id}/execute`

**Response**: `202 Accepted`
```json
{
  "executionId": "a1b2c3d4-1234-5678-abcd-1234567890ab",
  "status": "Pending",
  "message": "File check queued for execution"
}
```

### 4. Query Execution History (API)

**Request**: `GET https://localhost:5001/api/configurations/{id}/executions`

**Response**: `200 OK`
```json
{
  "items": [
    {
      "id": "a1b2c3d4-1234-5678-abcd-1234567890ab",
      "executionStartedAt": "2025-01-25T13:00:00Z",
      "executionCompletedAt": "2025-01-25T13:00:05Z",
      "status": "Completed",
      "filesFound": 1,
      "filesProcessed": 1,
      "durationMs": 5234,
      "resolvedFilePathPattern": "/transactions/2025/01",
      "resolvedFilenamePattern": "trans_20250125.csv"
    }
  ],
  "totalCount": 1
}
```

---

## Protocol-Specific Testing

### FTP Protocol

**Setup Test FTP Server** (Docker):
```bash
docker run -d -p 21:21 -p 30000-30009:30000-30009 \
  -e FTP_USER=testuser \
  -e FTP_PASS=testpass \
  -e PASV_ADDRESS=127.0.0.1 \
  -e PASV_MIN_PORT=30000 \
  -e PASV_MAX_PORT=30009 \
  stilliard/pure-ftpd
```

**Upload Test File**:
```bash
# Create test directory structure
mkdir -p /transactions/2025/01
echo "test,data,here" > /transactions/2025/01/trans_20250125.csv

# Upload via FTP client (or use FileZilla)
ftp testuser@localhost
put trans_20250125.csv /transactions/2025/01/trans_20250125.csv
```

**Create Configuration**: Use FTP settings pointing to `localhost:21`

### HTTPS Protocol

**Setup Test HTTPS Endpoint** (using json-server):
```bash
npm install -g json-server

# Create mock file endpoint
echo '{"files": [{"name": "report_20250125.xlsx", "size": 524288, "lastModified": "2025-01-25T06:00:00Z"}]}' > files.json

json-server --watch files.json --port 8080 --routes routes.json
```

**routes.json**:
```json
{
  "/files/:year/:month/:day": "/files"
}
```

**Create Configuration**:
```json
{
  "protocol": "Https",
  "protocolSettings": {
    "baseUrl": "http://localhost:8080",
    "authenticationType": "None",
    "connectionTimeout": "00:00:30"
  },
  "filePathPattern": "/files/{yyyy}/{mm}/{dd}",
  "filenamePattern": "report_{yyyy}{mm}{dd}.xlsx"
}
```

### Azure Blob Storage Protocol

**Setup Azurite** (local Azure Blob Storage emulator):
```bash
azurite --location c:\azurite
```

**Create Test Container and Blob**:
```bash
# Using Azure CLI
az storage container create --name test-files --connection-string "UseDevelopmentStorage=true"
az storage blob upload --container test-files --file report.xlsx --name 2025/01/report_20250125.xlsx --connection-string "UseDevelopmentStorage=true"
```

**Create Configuration**:
```json
{
  "protocol": "AzureBlob",
  "protocolSettings": {
    "storageAccountName": "devstoreaccount1",
    "containerName": "test-files",
    "authenticationType": "ConnectionString",
    "connectionStringKeyVaultSecret": "azurite-connection-string",
    "blobPrefix": "2025/01/"
  },
  "filePathPattern": "{yyyy}/{mm}",
  "filenamePattern": "report_{yyyy}{mm}{dd}.xlsx"
}
```

---

## Running Tests

### Unit Tests

```bash
cd tests/FileRetrieval.Domain.Tests
dotnet test

cd ../FileRetrieval.Application.Tests
dotnet test
```

**Expected Coverage**:
- Domain: 90%+ (entities, token replacement logic)
- Application: 80%+ (services, protocol adapters, handlers)

### Integration Tests

```bash
cd tests/FileRetrieval.Integration.Tests
dotnet test
```

**What's Tested**:
- Cosmos DB repositories (CRUD operations)
- Protocol adapters (FTP, HTTPS, Azure Blob with test servers)
- Idempotency (duplicate file checks)
- API endpoints (security trimming)

### Test Doubles (for Protocols)

Use **Testcontainers** for integration tests:
```csharp
// FTP Test Container
await using var ftpContainer = new ContainerBuilder()
    .WithImage("stilliard/pure-ftpd")
    .WithPortBinding(21, true)
    .Build();

await ftpContainer.StartAsync();

// Run tests against containerized FTP server
```

---

## Debugging Tips

### View Cosmos DB Data

Use **Azure Storage Explorer** or **Cosmos DB Data Explorer**:
- Connect to `https://localhost:8081`
- Browse containers: `file-retrieval-configurations`, `file-retrieval-executions`, `discovered-files`

### View Service Bus Messages

Use **Service Bus Explorer**:
- Connect to local Service Bus endpoint
- View queues: `FileRetrieval.Worker`, `WorkflowOrchestrator`
- View topics/subscriptions: `FileDiscovered`, `FileCheckCompleted`, `FileCheckFailed`

### Enable Verbose Logging

**appsettings.Development.json**:
```json
{
  "Logging": {
    "LogLevel": {
      "FileRetrieval": "Trace",
      "NServiceBus": "Debug"
    }
  }
}
```

### Common Issues

| Issue | Solution |
|-------|----------|
| Cosmos DB connection error | Ensure emulator is running: `https://localhost:8081/_explorer/index.html` |
| Service Bus connection error | Check connection string in appsettings, or use emulator |
| FTP connection timeout | Check FTP server is running: `docker ps`, verify passive mode ports |
| Token replacement error | Validate tokens not in server name, check date format |
| Duplicate file events | Check DiscoveredFile unique key constraint, verify idempotency logic |

---

## Development Workflow

### 1. Feature Branch

```bash
git checkout -b feature/file-retrieval-enhancements
```

### 2. Make Changes

- Add new protocol adapter: Implement `IProtocolAdapter`
- Add new token types: Update `TokenReplacementService`
- Add new API endpoint: Add controller method, update OpenAPI spec

### 3. Run Tests

```bash
dotnet test --logger "console;verbosity=detailed"
```

### 4. Check Code Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage -reporttypes:Html
```

Open `coverage/index.html` to view coverage report.

### 5. Commit and Push

```bash
git add .
git commit -m "feat: Add AWS S3 protocol adapter"
git push origin feature/file-retrieval-enhancements
```

### 6. Create Pull Request

- Open PR against `main` branch
- Ensure CI checks pass (build, tests, code coverage)
- Request review from team

---

## Integration with Workflow Platform

### Workflow Platform Subscribes to Events

**In Workflow Service** (`services/workflow/`):

```csharp
public class FileDiscoveredHandler : IHandleMessages<FileDiscovered>
{
    private readonly IWorkflowExecutionService _workflowService;
    
    public async Task Handle(FileDiscovered message, IMessageHandlerContext context)
    {
        _logger.LogInformation(
            "File discovered: {FileUrl} for client {ClientId}",
            message.FileUrl, message.ClientId);
        
        // Start workflow instance
        var workflowInstanceId = await _workflowService.StartWorkflowAsync(
            workflowType: message.EventData?["workflowType"]?.ToString() ?? "FileProcessing",
            inputData: new Dictionary<string, object>
            {
                ["fileUrl"] = message.FileUrl,
                ["filename"] = message.Filename,
                ["fileSize"] = message.FileSize,
                ["configurationId"] = message.ConfigurationId,
                ["clientId"] = message.ClientId
            });
        
        _logger.LogInformation(
            "Workflow started: {WorkflowInstanceId} for file {FileUrl}",
            workflowInstanceId, message.FileUrl);
    }
}
```

### Testing End-to-End Flow

1. **Create configuration** via API (with `commandsToSend` targeting workflow platform)
2. **Trigger file check** manually or wait for schedule
3. **Verify file discovered** - Check Cosmos DB for `DiscoveredFile` record
4. **Verify event published** - Check Service Bus for `FileDiscovered` event
5. **Verify workflow started** - Check workflow platform for new `WorkflowInstance`

---

## API Documentation

### OpenAPI (Swagger)

Access Swagger UI: `https://localhost:5001/swagger`

**Endpoints**:
- `GET /api/configurations` - List configurations for authenticated client
- `POST /api/configurations` - Create new configuration
- `GET /api/configurations/{id}` - Get configuration details
- `PUT /api/configurations/{id}` - Update configuration
- `DELETE /api/configurations/{id}` - Delete (soft) configuration
- `POST /api/configurations/{id}/execute` - Manually trigger file check
- `GET /api/configurations/{id}/executions` - Query execution history

### Authentication

API uses **JWT bearer token authentication**:
```
Authorization: Bearer <jwt-token>
```

JWT must include claims:
- `sub` (Subject) - User ID
- `clientId` - Client ID for multi-tenancy

---

## Next Steps

### Phase 2: Task Implementation

After Phase 1 design is complete, run:
```bash
gh copilot task --specification specs/001-file-retrieval-config/spec.md --plan specs/001-file-retrieval-config/plan.md
```

This generates `tasks.md` with concrete implementation tasks.

### Extending the Feature

**Add New Protocol** (e.g., AWS S3):
1. Create `AwsS3ProtocolAdapter : IProtocolAdapter`
2. Define `AwsS3ProtocolSettings : ProtocolSettings`
3. Add `ProtocolType.AwsS3` enum value
4. Register in DI: `services.AddTransient<IProtocolAdapter, AwsS3ProtocolAdapter>()`
5. Write integration tests with Testcontainers (LocalStack)

**Add New Token Types** (e.g., `{clientId}`):
1. Update `TokenReplacementService.ReplaceTokens()` method
2. Add validation for new token positions
3. Update documentation and examples
4. Write unit tests for new token types

---

## Resources

- **Architecture Diagram**: See `specs/001-file-retrieval-config/plan.md` (Project Structure section)
- **Data Model**: See `specs/001-file-retrieval-config/data-model.md`
- **Message Contracts**: See `specs/001-file-retrieval-config/contracts/`
- **Research Decisions**: See `specs/001-file-retrieval-config/research.md`
- **RiskInsure Constitution**: See `copilot-instructions/constitution.md`

---

## Support

For questions or issues:
- **Slack**: #file-retrieval-dev
- **Email**: dev-team@riskinsure.com
- **Docs**: [Internal Wiki](https://wiki.riskinsure.com/file-retrieval)

---

**Happy Coding! 🚀**
