# Getting Started with RiskInsure

**Quick start guide for GitHub Codespaces and Azure development** | **Last Updated**: 2026-02-07

This guide walks you through setting up your development environment in GitHub Codespaces with Azure resources.

---

## 🚀 Quick Start (GitHub Codespaces - Recommended)

GitHub Codespaces provides a fully configured development container with all tools pre-installed. This is the **fastest way to get started**.

### Prerequisites

- **GitHub account** with Codespaces access
- **Azure subscription** with permissions to create resources
- **15 minutes** to complete setup

---

## Step-by-Step Setup

### 1️⃣ Launch GitHub Codespace

1. Go to the [RiskInsure repository](https://github.com/AIS-Commercial-Business-Unit/RiskInsure)
2. Click **Code** → **Codespaces** → **Create codespace on main**
3. Wait 2-3 minutes for the container to build
4. Your browser opens VS Code with everything pre-installed:
   - ✅ .NET 10 SDK
   - ✅ Docker (for running services)
   - ✅ Azure CLI
   - ✅ Node.js (for E2E tests)
   - ✅ Git

**💡 Tip**: Use a **4-core • 16GB RAM** machine type for best performance.

---

### 2️⃣ Create Azure Resources

You'll need two Azure Resources: **Cosmos DB** for data persistence and **RabbitMQ** or **Service Bus** for messaging transport.

#### Option A: Azure CLI (Command Line)

```bash
# Login to Azure
az login --use-device-code

# Set your subscription (if you have multiple)
az account set --subscription "YOUR-SUBSCRIPTION-NAME"

# Create resource group
az group create \
  --name riskinsure-dev \
  --location eastus

# Create Cosmos DB account (takes ~5 minutes)
az cosmosdb create \
  --name riskinsure-cosmosdb \
  --resource-group riskinsure-dev \
  --default-consistency-level Session \
  --locations regionName=eastus

# Create Service Bus namespace
az servicebus namespace create \
  --resource-group riskinsure-dev \
  --name acmecorp-dev-servicebus \
  --location eastus \
  --sku Standard
  
# Provision RabbitMQ broker (example: local Docker)
docker run -d --name rabbitmq \
  -p 5672:5672 -p 15672:15672 \
  rabbitmq:3-management
```

#### Option B: Azure Portal (Visual)

**Cosmos DB:**
1. Go to [Azure Portal](https://portal.azure.com)
2. Click **+ Create a resource** → Search "Azure Cosmos DB"
3. Select **Azure Cosmos DB for NoSQL**
4. Fill in:
   - **Subscription**: Your subscription
   - **Resource Group**: `riskinsure-dev` (create new)
   - **Account Name**: `riskinsure-cosmosdb`
   - **Location**: East US
   - **Capacity mode**: Provisioned throughput
   - **Apply Free Tier Discount**: Yes (if available)
5. Click **Review + create** → **Create** (takes ~5 minutes)

**Service Bus:**
1. Click **+ Create a resource** → Search "Service Bus"
2. Fill in:
   - **Subscription**: Your subscription
   - **Resource Group**: `riskinsure-dev`
   - **Namespace name**: `acmecorp-dev-servicebus`
   - **Location**: East US
   - **Pricing tier**: Standard
3. Click **Review + create** → **Create** (takes ~2 minutes)

**RabbitMQ:**
1. Use your RabbitMQ provider of choice (Docker, CloudAMQP, self-hosted VM, etc.)
2. Create a broker and credentials
3. Save connection info in NServiceBus RabbitMQ format, e.g. `host=localhost;username=guest;password=guest`

---

### 3️⃣ Get Connection Strings

#### Cosmos DB Connection String

```bash
az cosmosdb keys list \
  --name riskinsure-cosmosdb \
  --resource-group riskinsure-dev \
  --type connection-strings \
  --query "connectionStrings[0].connectionString" -o tsv
```

**Or via Portal:**
1. Go to your Cosmos DB account → **Keys** (left menu)
2. Copy **PRIMARY CONNECTION STRING**

#### Service Bus Connection String

``bash
az servicebus namespace authorization-rule keys list \
  --resource-group riskinsure-dev \
  --namespace-name acmecorp-dev-servicebus \
  --name RootManageSharedAccessKey \
  --query primaryConnectionString -o tsv
```

**Or via Portal:**
1. Go to your Service Bus namespace → **Shared access policies** (left menu)
2. Click **RootManageSharedAccessKey**
3. Copy **Primary Connection String**
#### RabbitMQ Connection String

```bash
TODO
```

Use the RabbitMQ broker credentials/endpoint you provisioned.

---

### 4️⃣ Create Service Bus Queues

Install the NServiceBus transport CLI tool and create all required queues:

```bash
# Install the ASB transport CLI tool (one-time installation)
dotnet tool install --global NServiceBus.Transport.AzureServiceBus.CommandLine

# Set your Service Bus connection string (use the one from Step 3)
export AzureServiceBus_ConnectionString="YOUR-SERVICEBUS-CONNECTION-STRING-FROM-STEP-3"

# Run the queue setup script for each service
./services/billing/src/Infrastructure/queues.sh

# Or create queues manually:
# Create shared infrastructure queues (run once)
asb-transport queue create error
asb-transport queue create audit
asb-transport queue create particular.monitoring

# Create endpoints for each service
asb-transport endpoint create RiskInsure.Billing.Endpoint
asb-transport endpoint subscribe RiskInsure.Billing.Endpoint RiskInsure.PublicContracts.Events.PolicyBound

asb-transport endpoint create RiskInsure.Customer.Endpoint

asb-transport endpoint create RiskInsure.Policy.Endpoint
asb-transport endpoint subscribe RiskInsure.Policy.Endpoint RiskInsure.PublicContracts.Events.QuoteAccepted

asb-transport endpoint create RiskInsure.RatingAndUnderwriting.Endpoint

asb-transport endpoint create RiskInsure.FundTransferMgt.Endpoint
asb-transport endpoint subscribe RiskInsure.FundTransferMgt.Endpoint RiskInsure.PublicContracts.Events.FundsSettled
```

**💡 Tip**: Each service has a `queues.sh` script in its Infrastructure folder for automated setup.

### 4️⃣ Messaging Topology Notes

RabbitMQ transport does not require legacy queue bootstrap CLI tooling.

- NServiceBus endpoints create required queues/exchanges with installers enabled in development.
- Ensure your RabbitMQ user has permissions to create/bind exchanges and queues.

---

### 5️⃣ Configure Environment Variables

Create a `.env` file in the repository root with your connection strings:

```bash
cd /workspaces/RiskInsure

cat > .env << 'EOF'
# Azure Cosmos DB Connection String
COSMOSDB_CONNECTION_STRING=YOUR-COSMOS-CONNECTION-STRING-HERE

# Azure Service Bus Connection String
SERVICEBUS_CONNECTION_STRING=YOUR-SERVICEBUS-CONNECTION-STRING-HERE

# RabbitMQ Connection String
RABBITMQ_CONNECTION_STRING=host=localhost;username=guest;password=guest
EOF
```

**Replace the placeholder values** with your actual connection strings from Step 3.

**Example `.env` file:**
```bash
COSMOSDB_CONNECTION_STRING=AccountEndpoint=https://YOUR-COSMOSDB-ACCOUNT.documents.azure.com:443/;AccountKey=YOUR-COSMOS-KEY-HERE==;

SERVICEBUS_CONNECTION_STRING=Endpoint=sb://YOUR-SERVICEBUS-NAMESPACE.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=YOUR-SERVICEBUS-KEY-HERE=

RABBITMQ_CONNECTION_STRING=host=localhost;username=guest;password=guest
```

#### Make Environment Variables Persistent

Add them to your shell profile so they survive terminal restarts:

```bash
# Add to .bashrc (recommended for Codespaces)
cat >> ~/.bashrc << 'EOF'

# RiskInsure Azure Connection Strings
export COSMOSDB_CONNECTION_STRING="$(grep COSMOSDB_CONNECTION_STRING /workspaces/RiskInsure/.env | cut -d'=' -f2-)"

export SERVICEBUS_CONNECTION_STRING="$(grep SERVICEBUS_CONNECTION_STRING /workspaces/RiskInsure/.env | cut -d'=' -f2-)"

export RABBITMQ_CONNECTION_STRING="$(grep RABBITMQ_CONNECTION_STRING /workspaces/RiskInsure/.env | cut -d'=' -f2-)"
EOF

# Reload shell
source ~/.bashrc
```

**Verify environment variables:**
```bash
echo $COSMOSDB_CONNECTION_STRING | cut -c1-50
echo $SERVICEBUS_CONNECTION_STRING | cut -c1-50
echo $RABBITMQ_CONNECTION_STRING | cut -c1-50
```

You should see the beginning of your connection strings.

---

### 6️⃣ Build and Start Services

```bash
# Build all services
docker compose build

# Start all services with your Azure resources
docker compose up -d

# Wait 30 seconds for services to initialize
sleep 30
```

---

### 7️⃣ Verify Everything Works

Run the smoke test to verify all services are healthy:

```bash
./scripts/smoke-test.sh
```

**Expected output:**
```
========================================
 RiskInsure Local Smoke Test
========================================

[CONTAINER STATUS]
  ✓ 10/10 containers running

[API CONNECTIVITY]
  ✓ 5/5 APIs responding

[CONFIGURATION]
  ✓ .env file: Found
  ✓ Cosmos DB connection: Valid format
  ✓ Service Bus connection: Valid format
  ✓ RabbitMQ connection: Valid format

[OVERALL RESULT]
✓ PASS - All services operational
```

If you see any failures, check:
1. Environment variables are set: `echo $COSMOSDB_CONNECTION_STRING`
2. Containers have correct config: `docker inspect riskinsure-billing-api-1 | grep ConnectionStrings`
3. Azure resources are accessible: `az cosmosdb show -n riskinsure-cosmosdb -g riskinsure-dev`

---

### 8️⃣ Run End-to-End Tests

```bash
cd test/e2e

# Install dependencies (first time only)
npm install
npx playwright install chromium
npx playwright install-deps chromium

# Run tests
npm test

# View test report
npm run test:report
```

**Expected output:**
```
Running 3 tests using 1 worker

  ✓  1 …complete quote to policy workflow with Class A approval (10.4s)
  ✓  2 …complete quote to policy workflow with Class B approval (5.8s)
  ✓  3 …declined quote does not create policy (5.0s)

  3 passed (21.2s)
```

---

## 🎯 You're Ready!

Your development environment is fully configured:
- ✅ All 5 services running on Azure resources
- ✅ APIs accessible at http://127.0.0.1:707X
- ✅ E2E tests passing
- ✅ Message queues configured
- ✅ Database containers created

### What's Running:

| Service | API Port | OpenAPI Docs |
|---------|----------|--------------|
| Billing | 7071 | http://127.0.0.1:7071/scalar/v1 |
| Customer | 7073 | http://127.0.0.1:7073/scalar/v1 |
| Funds Transfer | 7075 | http://127.0.0.1:7075/scalar/v1 |
| Policy | 7077 | http://127.0.0.1:7077/scalar/v1 |
| Rating & Underwriting | 7079 | http://127.0.0.1:7079/scalar/v1 |

---

## 🔧 Development Workflow

### Starting Services

```bash
# Start all services
docker compose up -d

# Start specific service
docker compose up -d billing-api billing-endpoint

# View logs
docker compose logs -f billing-api

# Restart after code changes
docker compose restart billing-api
```

### Stopping Services

```bash
# Stop all services (keeps containers)
docker compose stop

# Stop and remove all containers
docker compose down

# Stop with cleanup (removes volumes)
docker compose down -v
```

### Rebuilding After Code Changes

```bash
# Rebuild specific service
docker compose build billing-api
docker compose up -d billing-api

# Rebuild all services
docker compose down
docker compose build
docker compose up -d
```

---

## 🐛 Troubleshooting

### Issue: "Connection refused" when testing APIs

**Solution**: Services need time to initialize (30-60 seconds). Wait and try again:
```bash
sleep 30 && ./scripts/smoke-test.sh
```

### Issue: Container exits immediately after startup

**Check logs:**
```bash
docker logs riskinsure-billing-api-1
```

**Common causes:**
- Invalid connection string (check for typos)
- Azure resource not accessible (firewall/network)
- Missing environment variables

**Verify env vars in container:**
```bash
docker inspect riskinsure-billing-api-1 --format '{{range .Config.Env}}{{println .}}{{end}}' | grep ConnectionStrings
```

### Issue: "Name or service not known (cosmos-emulator:8081)"

This means the container is trying to use the local emulator instead of Azure. **Solution:**

```bash
# Ensure environment variables are set
export COSMOSDB_CONNECTION_STRING="YOUR-AZURE-COSMOS-STRING"
export SERVICEBUS_CONNECTION_STRING="YOUR-AZURE-SERVICEBUS-STRING"
export RABBITMQ_CONNECTION_STRING="host=localhost;username=guest;password=guest"

# Restart containers
docker compose down
docker compose up -d
```

### Issue: Tests fail with "Playwright needs system dependencies"

**Solution:**
```bash
cd test/e2e
npx playwright install-deps chromium
npm test
```

### Issue: E2E tests can't run in UI mode in Codespaces

**Expected**: UI mode requires a display server. Use headless mode instead:
```bash
npm test              # Headless (works in Codespaces)
npm run test:report   # View results in browser
```

---

## 📚 Alternative Setup (Local Development)

If you prefer local development instead of Codespaces:

### Prerequisites

- **[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)**
- **[Docker Desktop](https://www.docker.com/products/docker-desktop)**
- **[Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli)**
- **[Git](https://git-scm.com/)**

### Setup Steps

Follow the same steps as Codespaces (above), but:
1. Clone the repo locally: `git clone https://github.com/AIS-Commercial-Business-Unit/RiskInsure.git`
2. Create Azure resources (steps 2-4 above)
3. Create `.env` file in repo root
4. Use local emulators if preferred:

```bash
# Cosmos DB Emulator (Docker)
docker compose --profile infra up -d cosmos-emulator

# RabbitMQ (Docker)
docker compose --profile infra up -d rabbitmq

# Azure Key Vault Emulator (Docker)
docker compose -f docker-compose.infrastructure.yml up -d keyvault-emulator

# Note: Emulators may be less stable than Azure resources
```

**Trust the Key Vault emulator certificate (required for Azure SDK HTTPS):**

**Windows (PowerShell):**
```powershell
docker cp keyvault-emulator:/certs/emulator.crt ./emulator.crt
Import-Certificate -FilePath .\emulator.crt -CertStoreLocation Cert:\CurrentUser\Root
Remove-Item .\emulator.crt
```

**Linux:**
```bash
docker cp keyvault-emulator:/certs/emulator.crt ./emulator.crt
sudo cp ./emulator.crt /usr/local/share/ca-certificates/emulator.crt
sudo update-ca-certificates
rm ./emulator.crt
```

**macOS:**
```bash
docker cp keyvault-emulator:/certs/emulator.crt ./emulator.crt
sudo security add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain ./emulator.crt
rm ./emulator.crt
```

If certificate trust is not configured, Azure SDK clients may fail with SSL trust errors such as `UntrustedRoot`.

**Emulator connection strings:**
```bash
# .env for local emulators
COSMOSDB_CONNECTION_STRING=AccountEndpoint=https://cosmos-emulator:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;DisableServerCertificateValidation=true

RABBITMQ_CONNECTION_STRING=host=rabbitmq;username=guest;password=guest

# Key Vault emulator endpoint
AzureKeyVault__VaultUri=https://localhost:4997
```

**Equivalent `appsettings.Development.json` snippet:**
```json
{
  "AzureKeyVault": {
    "VaultUri": "https://localhost:4997"
  }
}
```

⚠️ **Recommendation**: Use real Azure resources even for local development. Emulators can be memory-intensive and less reliable

---

---

## 📖 Creating Your First Service (Advanced)

<details>
<summary>Click to expand: Step-by-step guide to creating a new bounded context</summary>

### Service Design

Let's create a **Billing** service as an example.

**Responsibilities**:
- Manages billing accounts and invoices
- Publishes `InvoiceCreated` events
- Subscribes to `PolicyBound` events

### 1. Create Project Structure

```bash
cd services
mkdir -p billing/{src/{Domain,Infrastructure,Api,Endpoint.In},test/{Unit.Tests,Integration.Tests},docs}
```

### 2. Domain Layer (Pure Business Logic)

```bash
cd billing/src/Domain
dotnet new classlib -n RiskInsure.Billing.Domain -f net10.0
rm Class1.cs
mkdir -p Contracts/{Commands,Events} Models Services
```

**Message Contract** (`Contracts/Events/InvoiceCreated.cs`):
```csharp
namespace RiskInsure.Billing.Domain.Contracts.Events;

public record InvoiceCreated(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    Guid InvoiceId,
    string PolicyId,
    decimal PremiumAmount,
    string IdempotencyKey
);
```

**Domain Model** (`Models/Invoice.cs`):
```csharp
namespace RiskInsure.Billing.Domain.Models;

public class Invoice
{
    public Guid InvoiceId { get; set; }
    public string PolicyId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public InvoiceStatus Status { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
}

public enum InvoiceStatus { Pending, Paid, Cancelled }
```

### 3. Infrastructure Layer

```bash
cd ../Infrastructure
dotnet new classlib -n RiskInsure.Billing.Infrastructure -f net10.0
rm Class1.cs
dotnet add reference ../Domain/RiskInsure.Billing.Domain.csproj
dotnet add reference ../../../../platform/RiskInsure.PublicContracts/RiskInsure.PublicContracts.csproj
mkdir -p MessageHandlers Repositories
```

**Message Handler** (`MessageHandlers/PolicyBoundHandler.cs`):
```csharp
namespace RiskInsure.Billing.Infrastructure.MessageHandlers;

using NServiceBus;
using RiskInsure.PublicContracts.Events;
using RiskInsure.Billing.Domain.Models;

public class PolicyBoundHandler : IHandleMessages<PolicyBound>
{
    private readonly IInvoiceRepository _repository;
    private readonly ILogger<PolicyBoundHandler> _logger;

    public async Task Handle(PolicyBound message, IMessageHandlerContext context)
    {
        _logger.LogInformation("Creating invoice for policy {PolicyId}", message.PolicyId);
        
        var invoice = new Invoice
        {
            InvoiceId = Guid.NewGuid(),
            PolicyId = message.PolicyId,
            Amount = message.PremiumAmount,
            Status = InvoiceStatus.Pending,
            CreatedUtc = DateTimeOffset.UtcNow
        };
        
        await _repository.CreateAsync(invoice);
        
        await context.Publish(new InvoiceCreated(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            InvoiceId: invoice.InvoiceId,
            PolicyId: message.PolicyId,
            PremiumAmount: message.PremiumAmount,
            IdempotencyKey: $"invoice-{message.PolicyId}"
        ));
    }
}
```

### 4. Add to Solution

```bash
cd ../../../../
dotnet sln add services/billing/src/Domain/RiskInsure.Billing.Domain.csproj
dotnet sln add services/billing/src/Infrastructure/RiskInsure.Billing.Infrastructure.csproj
dotnet build
```

### 5. Create API and Endpoint Projects

Follow the patterns in existing services (Customer, Policy, etc.) for API and Endpoint.In projects.

### 6. Add to Docker Compose

Add service definitions to `docker-compose.yml` following existing patterns.

</details>

---

## 🔍 Monitoring and Debugging

### View Service Logs

```bash
# All services
docker compose logs -f

# Specific service
docker compose logs -f billing-api

# Last 100 lines
docker compose logs --tail=100 billing-endpoint
```

### Azure Portal Monitoring

**Cosmos DB**:
- Go to your Cosmos DB account → **Data Explorer**
- Navigate to `RiskInsure` database → containers
- Query documents: `SELECT * FROM c WHERE c.type = 'Quote'`

**Service Bus**:
- Go to your Service Bus namespace → **Queues**
- View active/dead-letter message counts
- Use **Service Bus Explorer** to inspect messages

**RabbitMQ**:
- Open RabbitMQ management UI (default: `http://localhost:15672`)
- Review queues/exchanges and message rates
- Verify endpoint queues exist and are consuming

### Application Insights (Optional)

Add Application Insights for production monitoring:
```bash
az monitor app-insights component create \
  --app riskinsure-insights \
  --resource-group riskinsure-dev \
  --location eastus
```

---

## 🧪 Testing

### Unit Tests

```bash
# Run all tests
dotnet test

# Specific service
dotnet test services/billing/test/Unit.Tests

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Integration Tests (Playwright)

```bash
cd test/e2e

# Run all tests
npm test

# Run specific test
npx playwright test quote-to-policy-flow.spec.ts

# Debug mode
npx playwright test --debug

# View last run report
npm run test:report
```

### API Testing (Manual)

Use the Scalar UI (built-in):
- Billing: http://127.0.0.1:7071/scalar/v1
- Customer: http://127.0.0.1:7073/scalar/v1
- Policy: http://127.0.0.1:7077/scalar/v1
- Rating: http://127.0.0.1:7079/scalar/v1
- Funds Transfer: http://127.0.0.1:7075/scalar/v1

---

## 🎓 Next Steps

1. **Architecture Deep Dive**: Read [.specify/memory/constitution.md](../.specify/memory/constitution.md)
2. **Study Domain Standards**: Review [platform/fileintegration/docs/filerun-processing-standards.md](../platform/fileintegration/docs/filerun-processing-standards.md)
3. **Understand Project Layout**: See [copilot-instructions/project-structure.md](../copilot-instructions/project-structure.md)
4. **Learn Message Patterns**: Read [copilot-instructions/messaging-patterns.md](../copilot-instructions/messaging-patterns.md)
5. **Explore E2E Tests**: Study `test/e2e/tests/quote-to-policy-flow.spec.ts`

---

## 📚 Quick Reference

### Essential Commands

```bash
# Start all services
docker compose up -d

# Check status
./scripts/smoke-test.sh

# View logs
docker compose logs -f [service-name]

# Restart service
docker compose restart [service-name]

# Rebuild after code changes
docker compose build [service-name]
docker compose up -d [service-name]

# Stop everything
docker compose down

# Run E2E tests
cd test/e2e && npm test

# Check environment variables
echo $COSMOSDB_CONNECTION_STRING | cut -c1-50
env | grep CONNECTION_STRING
```

### Service Ports

| Service | API | Endpoint | 
|---------|-----|----------|
| Billing | 7071 | 7072 |
| Customer | 7073 | 7074 |
| Funds Transfer | 7075 | 7076 |
| Policy | 7077 | 7078 |
| Rating & Underwriting | 7079 | 7080 |

### Azure CLI Shortcuts

```bash
# Show Cosmos DB details
az cosmosdb show -n riskinsure-cosmosdb -g riskinsure-dev

# List Service Bus queues
az servicebus queue list --namespace-name acmecorp-dev-servicebus -g riskinsure-dev -o table

# List RabbitMQ queues (inside container)
docker exec rabbitmq rabbitmqctl list_queues name messages consumers

# Get connection strings
az cosmosdb keys list --name riskinsure-cosmosdb -g riskinsure-dev --type connection-strings --query "connectionStrings[0].connectionString" -o tsv
az servicebus namespace authorization-rule keys list -g riskinsure-dev --namespace-name acmecorp-dev-servicebus --name RootManageSharedAccessKey --query primaryConnectionString -o tsv
echo "host=localhost;username=guest;password=guest"
```

### Useful Docker Commands

```bash
# Check which containers are running
docker ps

# Check environment variables in container
docker inspect riskinsure-billing-api-1 --format '{{range .Config.Env}}{{println .}}{{end}}'

# Execute command in container
docker exec -it riskinsure-billing-api-1 bash

# Clean up everything
docker compose down -v
docker system prune -a
```

---

## ❓ Getting Help

- **Architecture Questions**: See [copilot-instructions/](../copilot-instructions/)
- **Domain Standards**: Check service-specific `docs/` folders
- **Issues**: Review troubleshooting section above
- **Security**: See [SECURITY.md](../SECURITY.md)

---

**🎉 Happy Coding!** You're now ready to develop on the RiskInsure platform.
💰 Cost Management

### Free Tier Options

**Cosmos DB:**
- First 1000 RU/s free with [free tier](https://docs.microsoft.com/azure/cosmos-db/free-tier)
- ~$24/month after free tier

**Service Bus:**
- Standard tier: ~$10/month (includes first 12.5M operations)

**RabbitMQ:**
- Cost depends on hosting option (self-hosted, VM, managed broker)

**Codespaces:**
- First 60 hours/month free for 2-core machines
- 4-core: ~$0.36/hour

**Total estimated cost**: $0-50/month depending on usage

### Clean Up Resources

When done developing:
```bash
# Delete resource group (removes all resources)
az group delete --name riskinsure-dev --yes --no-wait

# Or stop Codespace to avoid charges
# GitHub → Your Codespaces → Stop
