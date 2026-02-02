# RiskInsure

Event-driven .NET 10 monorepo using NServiceBus, Azure Cosmos DB, and Azure Container Apps.

## 🏗️ Architecture

This repository implements an **event-driven architecture** with:
- **NServiceBus 10** for message-based integration via Azure Service Bus
- **Azure Cosmos DB** for single-partition NoSQL persistence
- **Azure Container Apps** for hosting NServiceBus endpoints with KEDA scaling
- **Azure Logic Apps Standard** for orchestration workflows

## 📁 Repository Structure

\\\
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
\\\

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli)
- [Git](https://git-scm.com/)

### First-Time Setup

1. **Clone the repository**
   \\\ash
   git clone https://github.com/your-org/RiskInsure.git
   cd RiskInsure
   \\\

2. **Restore dependencies**
   \\\ash
   dotnet restore
   \\\

3. **Build the solution**
   \\\ash
   dotnet build
   \\\

4. **Run tests**
   \\\ash
   dotnet test
   \\\

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
