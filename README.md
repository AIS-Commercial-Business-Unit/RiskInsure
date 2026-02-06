# RiskInsure

Event-driven .NET 10 monorepo using NServiceBus, Azure Cosmos DB, and Azure Container Apps.

## 🏗️ Architecture

This repository implements an **event-driven architecture** with:
- **NServiceBus 10** for message-based integration via Azure Service Bus
- **Azure Cosmos DB** for single-partition NoSQL persistence
- **Azure Container Apps** for hosting NServiceBus endpoints with KEDA scaling
- **Azure Logic Apps Standard** for orchestration workflows

## ☁️ GitHub Codespaces (Recommended for New Developers)

**Get started in minutes with zero local setup!**

[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://github.com/codespaces/new?hide_repo_select=true&ref=main)

GitHub Codespaces provides a fully configured development environment with:
- ✅ Azure Service Bus Emulator (local message queuing)
- ✅ Cosmos DB Emulator (local database)
- ✅ All 10 microservices pre-configured
- ✅ .NET 10 SDK, Docker, and Node.js
- ✅ GitHub Copilot Chat enabled
- ✅ **No Azure subscription required!**

**Quick Start:**
1. Click the badge above or go to **Code** → **Codespaces** → **Create codespace**
2. Wait ~5-10 minutes for setup
3. Run: `docker-compose up -d`
4. Start coding!

See [.devcontainer/README.md](.devcontainer/README.md) for full documentation.

## 📁 Repository Structure

```
RiskInsure/
├── platform/                    # Cross-cutting concerns
│   ├── publiccontracts/         # Shared message contracts (events/commands)
│   ├── ui/                      # Shared UI components
│   └── infra/                   # Infrastructure templates (Bicep, Terraform)
├── services/                    # Business-specific bounded contexts
│   ├── billing/                 # Billing domain service
│   ├── payments/                # Payments domain service
│   └── [your-service]/          # Add your services here
├── copilot-instructions/        # Architectural governance
│   ├── constitution.md          # Non-negotiable architectural principles
│   └── project-structure.md     # Bounded context template
└── scripts/                     # Automation scripts
```

## 🚀 Getting Started

### Option 1: GitHub Codespaces (Easiest)

See [☁️ GitHub Codespaces](#️-github-codespaces-recommended-for-new-developers) section above - **no local setup required!**

### Option 2: Local Development with Emulators

**No Azure subscription needed!** Run everything locally with emulators.

1. **Clone and setup**:
   ```bash
   git clone https://github.com/your-org/RiskInsure.git
   cd RiskInsure
   cp .env.emulator .env
   ```

2. **Start emulators**:
   ```bash
   docker-compose up -d servicebus-emulator cosmos-emulator
   ```

3. **Start all services**:
   ```bash
   docker-compose up -d
   ```

4. **Run tests**:
   ```bash
   cd test/e2e
   npm install
   npm test
   ```

See [docs/EMULATOR-SETUP.md](docs/EMULATOR-SETUP.md) for detailed instructions.

### Option 3: Local Development with Azure Resources

**For production-like testing:**

#### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli)
- [Git](https://git-scm.com/)
- Azure subscription

#### First-Time Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/your-org/RiskInsure.git
   cd RiskInsure
   ```

2. **Create Azure resources** (one-time):
   ```bash
   # Create resource group
   az group create --name riskinsure-dev --location eastus

   # Create Service Bus namespace
   az servicebus namespace create \
     --resource-group riskinsure-dev \
     --name riskinsure-dev-bus \
     --sku Standard

   # Create Cosmos DB account (free tier)
   az cosmosdb create \
     --resource-group riskinsure-dev \
     --name riskinsure-dev-cosmos \
     --enable-free-tier true
   ```

3. **Get connection strings and create .env file**:
   ```bash
   # Get Service Bus connection string
   az servicebus namespace authorization-rule keys list \
     --resource-group riskinsure-dev \
     --namespace-name riskinsure-dev-bus \
     --name RootManageSharedAccessKey \
     --query primaryConnectionString -o tsv

   # Create .env file
   cat > .env << EOF
   SERVICEBUS_CONNECTION_STRING="<paste-service-bus-connection-string>"
   COSMOSDB_CONNECTION_STRING="<paste-cosmos-connection-string>"
   EOF
   ```

4. **Build the solution**
   ```bash
   dotnet restore
   dotnet build
   ```

5. **Run tests**
   ```bash
   dotnet test
   ```

### Creating Your First Service

See [copilot-instructions/project-structure.md](copilot-instructions/project-structure.md) for the bounded context template.

**Quick steps**:
1. Create folder structure: \services/yourservice/src/{Api,Domain,Infrastructure,Endpoint.In}\
2. Start with Domain layer (contracts, models, interfaces)
3. Add Infrastructure layer (handlers, repositories)
4. Add API layer (HTTP endpoints)
5. Configure Endpoint.In (NServiceBus hosting)
6. Add all projects to solution: \dotnet sln add <path-to-csproj>\

## 📖 Documentation

- **[Constitution](copilot-instructions/constitution.md)** - Non-negotiable architectural rules
- **[Project Structure](copilot-instructions/project-structure.md)** - Bounded context template
- **[Copilot Instructions](.github/copilot-instructions.md)** - Coding assistant rules
- **[Template Initialization](docs/TEMPLATE-INITIALIZATION.md)** - How this template was initialized

## 🧪 Testing Strategy

- **Domain Layer**: 90%+ coverage (pure business logic)
- **Application Layer**: 80%+ coverage (services, handlers)
- **Infrastructure**: Integration tests with Cosmos DB emulator
- **Framework**: xUnit with AAA pattern

## 🔐 Security

See [SECURITY.md](SECURITY.md) for vulnerability reporting.

## 📋 Contributing

1. Review [constitution.md](copilot-instructions/constitution.md) principles
2. Follow [project-structure.md](copilot-instructions/project-structure.md) template
3. Ensure test coverage meets thresholds
4. All PRs require review from @your-org/contributors

## 📄 License

[Your License Here]
