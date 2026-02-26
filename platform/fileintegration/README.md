# File Retrieval Service

**Bounded Context**: File Retrieval Configuration  
**Specification**: [specs/001-file-retrieval-config](../../specs/001-file-retrieval-config/)  
**Status**: In Development

## Overview

The File Retrieval Service enables the RiskInsure platform to automatically check client file locations (FTP, HTTPS, Azure Blob Storage) on scheduled intervals, detect files matching configured patterns with date-based tokens, and trigger workflow orchestration events/commands when files are discovered.

This service integrates with the Distributed Workflow Orchestration Platform as a new bounded context, using the same messaging infrastructure (Azure Service Bus, NServiceBus) and architectural principles.

## Architecture

### Projects

- **FileRetrieval.Domain** - Core domain entities, value objects, and repository interfaces
- **FileRetrieval.Application** - Use cases, services, message handlers, and protocol adapters
- **FileRetrieval.Infrastructure** - Data access (Cosmos DB), scheduling, and external integrations
- **FileRetrieval.API** - REST API for configuration management (CRUD operations)
- **FileRetrieval.Worker** - Background worker service for scheduled file checks
- **FileRetrieval.Contracts** - Shared message contracts for NServiceBus integration

### Key Features

- **Multi-Protocol Support**: FTP, HTTPS, Azure Blob Storage (extensible architecture)
- **Date Token Replacement**: Dynamic path/filename patterns with `{yyyy}`, `{mm}`, `{dd}`, etc.
- **Cron Scheduling**: Flexible schedule configuration using cron expressions
- **Multi-Tenancy**: Client-scoped data access with security trimming
- **Idempotent Processing**: Zero duplicate workflow triggers (SC-007)
- **Message-Based Integration**: Event sourcing with workflow platform via Azure Service Bus

## Getting Started

### Prerequisites

- .NET 10.0 SDK
- Azure Cosmos DB Emulator or Azure Cosmos DB account
- Azure Service Bus namespace
- Azure Key Vault (for secrets management)

### Build

```bash
cd services/file-retrieval
dotnet build
```

### Run Tests

```bash
dotnet test
```

### Run Scheduled FTP E2E Test

This scenario validates end-to-end scheduled file discovery:
- Seeds a sample file into `ftp-server` (`file-retrieval-ftp`)
- Creates config via `POST /api/v1/configuration` with 5-second schedule
- Polls `GET /api/v1/configuration/{configurationId}/executionhistory` every 5 seconds
- Passes when `filesFound > 0` (fails after 2 minutes)

1. Start dependencies:

```powershell
cd platform/fileintegration
docker compose up -d
```

2. Set test environment values (PowerShell):

```powershell
$env:FILE_RETRIEVAL_API_BASE_URL = "http://localhost:7090"
$env:FILE_RETRIEVAL_JWT_SECRET = "REPLACE_WITH_SECRET_KEY_AT_LEAST_32_CHARS_LONG_FOR_PRODUCTION"
$env:FILE_RETRIEVAL_JWT_ISSUER = "https://riskinsure.com"
$env:FILE_RETRIEVAL_JWT_AUDIENCE = "file-retrieval-api"
$env:FILE_RETRIEVAL_FTP_PASSWORD_SECRET_NAME = "test-ftp-password"
```

3. Run only this test:

```powershell
dotnet test .\test\FileRetrieval.Integration.Tests\FileRetrieval.Integration.Tests.csproj --filter "FullyQualifiedName~ConfigurationScheduledFtpE2ETests"
```

Note: The secret referenced by `FILE_RETRIEVAL_FTP_PASSWORD_SECRET_NAME` must exist in the Key Vault configured by FileRetrieval runtime, and its value should match FTP password (`testpass`) in test compose.

#### Troubleshooting (E2E)

- `Configuration creation accepted but no files found`:
	- Confirm Key Vault secret exists and matches FTP password (`testpass`).
	- Confirm `FILE_RETRIEVAL_FTP_PASSWORD_SECRET_NAME` points to that secret.
- `FTP setup step fails`:
	- Verify container is running:

```powershell
docker inspect -f "{{.State.Running}}" file-retrieval-ftp
```

	- Ensure compose stack is up:

```powershell
cd platform/fileintegration
docker compose ps
```

### Run API (Development)

```bash
cd src/FileRetrieval.API
dotnet run
```

API will be available at: `https://localhost:5001`

### Run Worker (Development)

```bash
cd src/FileRetrieval.Worker
dotnet run
```

## Configuration

See `appsettings.json` in API and Worker projects for configuration templates.

Key configuration sections:
- **CosmosDb**: Cosmos DB endpoint and container names
- **AzureServiceBus**: Service Bus connection and endpoint name
- **AzureKeyVault**: Key Vault URI for secrets
- **Scheduler**: Worker polling interval and concurrency limits
- **ProtocolDefaults**: Timeout and retry settings per protocol

## Documentation

- [Specification](../../specs/001-file-retrieval-config/spec.md)
- [Implementation Plan](../../specs/001-file-retrieval-config/plan.md)
- [Data Model](../../specs/001-file-retrieval-config/data-model.md)
- [Research & Decisions](../../specs/001-file-retrieval-config/research.md)
- [Contracts (Commands & Events)](../../specs/001-file-retrieval-config/contracts/)
- [Quickstart Guide](../../specs/001-file-retrieval-config/quickstart.md)

## Development Standards

- **Domain Language**: Use consistent terminology from `docs/file-retrieval-standards.md`
- **Single-Partition Model**: All queries within client partition
- **Idempotent Handlers**: All message handlers must be idempotent
- **Structured Logging**: Include clientId, configurationId, executionId, protocol in logs
- **Test Coverage**: 90%+ domain, 80%+ application

## Deployment

Deployed as Docker containers to Azure Container Apps.

See `docs/deployment.md` for deployment instructions.

## License

Copyright © RiskInsure. All rights reserved.
