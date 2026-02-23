# RiskInsure

Event-driven .NET 10 monorepo using NServiceBus, RabbitMQ transport, Azure Cosmos DB, and Azure Container Apps.

## 🏗️ Architecture

This repository implements an **event-driven architecture** with:
- **NServiceBus 9.x** for message-based integration via RabbitMQ transport
- **Azure Cosmos DB** for single-partition NoSQL persistence
- **Azure Container Apps** for hosting NServiceBus endpoints with KEDA scaling
- **Azure Logic Apps Standard** for orchestration workflows

## ☁️ GitHub Codespaces (Recommended for New Developers)

**Get started in minutes with zero local setup!**

[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://github.com/codespaces/new?hide_repo_select=true&ref=main)

GitHub Codespaces provides a fully configured development environment with:
- ✅ RabbitMQ broker (local message queuing)
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
   docker-compose up -d rabbitmq cosmos-emulator
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

    # Provision RabbitMQ broker (example: local Docker)
    docker run -d --name rabbitmq \
       -p 5672:5672 -p 15672:15672 \
       rabbitmq:3-management

   # Create Cosmos DB account (free tier)
   az cosmosdb create \
     --resource-group riskinsure-dev \
     --name riskinsure-dev-cosmos \
     --enable-free-tier true
   ```

3. **Get connection strings and create .env file**:
   ```bash
   # Create .env file
   cat > .env << EOF
   RABBITMQ_CONNECTION_STRING="host=localhost;username=guest;password=guest"
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

## Docker Compose Development

### Start Infrastructure Only (SQL Server and Emulators)

```bash
# Start Cosmos DB and RabbitMQ infrastructure
docker-compose --profile infra up -d

# Verify emulators are healthy
docker-compose ps
```

### Start All Services

```bash
# Build and start all services (including emulators)
docker-compose up --build

# Or start in detached mode
docker-compose up -d --build
```

### Access Services

- **Billing API**: http://localhost:7071
- **Customer API**: http://localhost:7073
- **Funds Transfer API**: http://localhost:7075
- **Policy API**: http://localhost:7077
- **Rating & Underwriting API**: http://localhost:7079
- **Cosmos DB Emulator**: https://localhost:8081/_explorer/index.html

### Stop Services

```bash
# Stop all services
docker-compose down

# Stop and remove volumes (clean slate)
docker-compose down -v
```

### Troubleshooting

**Cosmos Emulator SSL Certificate**:
The Cosmos emulator uses a self-signed certificate. In production code, you'll need to configure the `CosmosClient` to accept the emulator certificate or import it into your trust store.

**RabbitMQ Connection**:
If you encounter broker connection issues, verify `RABBITMQ_CONNECTION_STRING` in `.env` and check the RabbitMQ container health (`docker-compose ps`).

## 📖 Documentation

### Architecture & Governance
- **[Constitution](.specify/memory/constitution.md)** - Non-negotiable architectural rules (read first!)
- **[Project Structure](copilot-instructions/project-structure.md)** - Bounded context template
- **[Copilot Instructions](.github/copilot-instructions.md)** - Coding assistant rules

### Feature Development
- **[Documentation Philosophy](docs/DOCUMENTATION-PHILOSOPHY.md)** - How domain docs and feature specs work together
- **[Spec Kit Quickstart](docs/SPEC-KIT-QUICKSTART.md)** - 5-step workflow for adding features
- **[Feature Specifications](specs/README.md)** - Historical record of all features (organized by domain)

### Setup & Operations
- **[Template Initialization](docs/TEMPLATE-INITIALIZATION.md)** - How this template was initialized

## 🧪 Testing Strategy

- **Domain Layer**: 90%+ coverage (pure business logic)
- **Application Layer**: 80%+ coverage (services, handlers)
- **Infrastructure**: Integration tests with Cosmos DB emulator
- **Framework**: xUnit with AAA pattern

## 🔐 Security

See [SECURITY.md](SECURITY.md) for vulnerability reporting.

## 📋 Contributing

1. Review [constitution.md](.specify/memory/constitution.md) principles
2. Follow [project-structure.md](copilot-instructions/project-structure.md) template
3. Ensure test coverage meets thresholds
4. All PRs require review from @your-org/contributors

## 📄 License

[Your License Here]
