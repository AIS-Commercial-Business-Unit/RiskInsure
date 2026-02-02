# Getting Started with RiskInsure

**Quick start guide for local development** | **Last Updated**: 2026-02-02

This guide walks you through setting up your local development environment and building your first service.

---

## Prerequisites

### Required Software

- **[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)** - Latest version
- **[Docker Desktop](https://www.docker.com/products/docker-desktop)** - For Cosmos DB emulator and containerization
- **[Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli)** - For Azure Service Bus setup
- **[Git](https://git-scm.com/)** - Version control
- **[Visual Studio 2025](https://visualstudio.microsoft.com/)** or **[VS Code](https://code.visualstudio.com/)** - IDE

### Recommended Tools

- **[Azure Storage Explorer](https://azure.microsoft.com/features/storage-explorer/)** - Browse Cosmos DB data
- **[Service Bus Explorer](https://github.com/paolosalvatori/ServiceBusExplorer)** - Monitor Service Bus messages
- **[Postman](https://www.postman.com/)** or **[REST Client VS Code extension](https://marketplace.visualstudio.com/items?itemName=humao.rest-client)** - API testing

---

## Local Development Setup

### Step 1: Clone and Build

```powershell
# Clone repository
git clone https://github.com/AIS-Commercial-Business-Unit/RiskInsure.git
cd RiskInsure

# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run tests
dotnet test
```

Expected output: `Build succeeded. 0 Error(s)`

---

### Step 2: Azure Cosmos DB Emulator

#### Option A: Windows Native
```powershell
# Install via Chocolatey
choco install azure-cosmosdb-emulator

# Start emulator
Start-CosmosDbEmulator
```

Access at: https://localhost:8081/_explorer/

#### Option B: Docker (Mac/Linux/Windows)
```bash
docker run -p 8081:8081 -p 10251-10254:10251-10254 \
  --name cosmos-emulator \
  -e AZURE_COSMOS_EMULATOR_PARTITION_COUNT=10 \
  -e AZURE_COSMOS_EMULATOR_ENABLE_DATA_PERSISTENCE=true \
  mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest
```

**Connection String** (emulator default):
```
AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==
```

---

### Step 3: Azure Service Bus

**Create development namespace**:
```bash
# Login to Azure
az login

# Create resource group (if needed)
az group create --name riskinsure-dev --location eastus

# Create Service Bus namespace
az servicebus namespace create \
  --resource-group riskinsure-dev \
  --name riskinsure-dev-bus \
  --location eastus \
  --sku Standard

# Get connection string
az servicebus namespace authorization-rule keys list \
  --resource-group riskinsure-dev \
  --namespace-name riskinsure-dev-bus \
  --name RootManageSharedAccessKey \
  --query primaryConnectionString -o tsv
```

**Alternative: Use Azurite for local testing** (not full Service Bus replacement):
```bash
docker run -p 10000:10000 -p 10001:10001 -p 10002:10002 \
  mcr.microsoft.com/azure-storage/azurite
```

---

### Step 4: Configuration Files

Each service has configuration templates. Copy and customize:

```powershell
# Example for a service named "Billing"
cd services/billing/src/Api
Copy-Item appsettings.Development.json.template appsettings.Development.json

# Edit appsettings.Development.json
code appsettings.Development.json
```

**Example `appsettings.Development.json`**:
```json
{
  "ConnectionStrings": {
    "CosmosDb": "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
    "ServiceBus": "Endpoint=sb://riskinsure-dev-bus.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=YOUR_KEY_HERE"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "NServiceBus": "Information"
    }
  }
}
```

⚠️ **Never commit `appsettings.Development.json`** - It's in .gitignore

---

## Creating Your First Service

### Step 1: Choose a Service

Let's create a **Billing** service as an example.

**What it does**:
- Manages invoices
- Publishes `InvoiceCreated` events
- Subscribes to `PaymentProcessed` events

---

### Step 2: Create Folder Structure

```powershell
# Navigate to services folder
cd services/billing

# Create project folders
mkdir -p src/Domain, src/Infrastructure, src/Api, src/Endpoint.In
mkdir -p test/Domain.Tests, test/Infrastructure.Tests, test/Api.Tests, test/Endpoint.Tests
mkdir -p docs
```

---

### Step 3: Create Domain Layer (Zero Dependencies)

**Create Domain project**:
```powershell
cd src/Domain
dotnet new classlib -n RiskInsure.Billing.Domain -f net10.0
Remove-Item Class1.cs

# Create folder structure
mkdir -p Contracts/Commands, Contracts/Events, Models, Services, Exceptions
```

**Define message contracts** (`Contracts/Events/InvoiceCreated.cs`):
```csharp
namespace RiskInsure.Billing.Domain.Contracts.Events;

public record InvoiceCreated(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    Guid InvoiceId,
    string CustomerId,
    decimal Amount,
    string IdempotencyKey
);
```

**Define domain model** (`Models/Invoice.cs`):
```csharp
namespace RiskInsure.Billing.Domain.Models;

public class Invoice
{
    public Guid InvoiceId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public InvoiceStatus Status { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
}

public enum InvoiceStatus
{
    Pending,
    Paid,
    Cancelled
}
```

**Add Domain project to solution**:
```powershell
cd ../../../..  # Back to root
dotnet sln add services/billing/src/Domain/RiskInsure.Billing.Domain.csproj
```

---

### Step 4: Create Infrastructure Layer

```powershell
cd services/billing/src/Infrastructure
dotnet new classlib -n RiskInsure.Billing.Infrastructure -f net10.0
Remove-Item Class1.cs

# Add reference to Domain
dotnet add reference ../Domain/RiskInsure.Billing.Domain.csproj

# Add reference to PublicContracts
dotnet add reference ../../../../platform/publiccontracts/RiskInsure.PublicContracts/RiskInsure.PublicContracts.csproj

# Create folder structure
mkdir -p Repositories, MessageHandlers, Services
```

**Create repository** (`Repositories/IInvoiceRepository.cs`):
```csharp
namespace RiskInsure.Billing.Infrastructure.Repositories;

using RiskInsure.Billing.Domain.Models;

public interface IInvoiceRepository
{
    Task<Invoice?> GetByIdAsync(Guid invoiceId);
    Task CreateAsync(Invoice invoice);
    Task UpdateAsync(Invoice invoice);
}
```

**Add to solution**:
```powershell
cd ../../../..
dotnet sln add services/billing/src/Infrastructure/RiskInsure.Billing.Infrastructure.csproj
```

---

### Step 5: Build and Verify

```powershell
# Build just this service
dotnet build services/billing/src/Domain
dotnet build services/billing/src/Infrastructure

# Or build entire solution
dotnet build
```

---

## Running Tests

```powershell
# Run all tests
dotnet test

# Run specific project tests
dotnet test services/billing/test/Domain.Tests

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## Running Services Locally

### API Project (HTTP endpoints)
```powershell
cd services/billing/src/Api
dotnet run
```

Access at: http://localhost:5001

### Endpoint.In Project (Message handlers)
```powershell
cd services/billing/src/Endpoint.In
dotnet run
```

Processes messages from Azure Service Bus.

---

## Debugging

### Visual Studio 2025
1. Open `RiskInsure.slnx`
2. Set startup project (e.g., `RiskInsure.Billing.Api`)
3. Press **F5**

### VS Code
```json
// .vscode/launch.json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Billing API",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/services/billing/src/Api/bin/Debug/net10.0/RiskInsure.Billing.Api.dll",
      "cwd": "${workspaceFolder}/services/billing/src/Api",
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  ]
}
```

---

## Common Issues

### Issue: Cosmos DB Emulator SSL Certificate Error
**Solution**:
```powershell
# Trust emulator certificate (Windows)
$cert = Get-ChildItem Cert:\LocalMachine\Root | Where-Object {$_.Subject -like "*Cosmos*"}
Export-Certificate -Cert $cert -FilePath cosmos-emulator.cer
Import-Certificate -FilePath cosmos-emulator.cer -CertStoreLocation Cert:\CurrentUser\Root
```

### Issue: Service Bus Connection Timeout
**Solution**:
- Verify connection string in `appsettings.Development.json`
- Check firewall rules allow outbound port 5671/5672
- Ensure Service Bus namespace is running

### Issue: Build Fails with "Package X not found"
**Solution**:
```powershell
dotnet restore --force
dotnet clean
dotnet build
```

---

## Next Steps

1. **Review Architecture**: Read [copilot-instructions/constitution.md](../copilot-instructions/constitution.md)
2. **Study Examples**: See [platform/fileintegration/docs/filerun-processing-standards.md](../platform/fileintegration/docs/filerun-processing-standards.md)
3. **Create More Services**: Follow [copilot-instructions/project-structure.md](../copilot-instructions/project-structure.md)
4. **Deploy to Azure**: Use Bicep/Terraform templates in `platform/infra/`

---

## Additional Resources

- **[Constitution](../copilot-instructions/constitution.md)** - Architectural principles
- **[Project Structure](../copilot-instructions/project-structure.md)** - Service template
- **[NServiceBus Documentation](https://docs.particular.net/nservicebus/)** - Messaging framework
- **[Cosmos DB Documentation](https://docs.microsoft.com/azure/cosmos-db/)** - Database platform
- **[Container Apps Documentation](https://docs.microsoft.com/azure/container-apps/)** - Hosting platform

---

**Need Help?** Check [SECURITY.md](../SECURITY.md) for contact information.
