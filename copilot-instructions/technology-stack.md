# Technology Stack

## Runtime & Framework

### .NET 9
- **Target Framework**: `net9.0`
- **Language Features**: C# 13 with nullable reference types enabled
- **Implicit Usings**: Enabled for common namespaces

## Messaging

### NServiceBus
- **Version**: 9.x
- **Purpose**: Distributed messaging framework for commands, events, and sagas
- **Key Packages**:
  - `NServiceBus` (core framework)
  - `NServiceBus.Extensions.Hosting` (host integration)
  - `NServiceBus.Extensions.DependencyInjection` (DI integration)

### Azure Service Bus
- **Version**: 7.x (Azure.Messaging.ServiceBus)
- **Transport**: `NServiceBus.Transport.AzureServiceBus` (v5.x)
- **Purpose**: Message broker for reliable, asynchronous communication
- **Features**:
  - Queues for point-to-point messaging
  - Topics/subscriptions for publish-subscribe
  - Dead-letter queues for failed messages
  - Scheduled delivery

## Database

### Azure Cosmos DB
- **Version**: 3.42.0 (Microsoft.Azure.Cosmos SDK)
- **Purpose**: NoSQL database for domain data storage
- **Features**:
  - One database per bounded context
  - Partition key-based scaling
  - Global distribution capabilities
  - Document model storage

## Containerization & Hosting

### Docker
- **Purpose**: Container packaging for consistent deployments
- **Dockerfiles**: Present in Api and Message projects
- **Base Images**: Microsoft official .NET runtime images

### Azure Container Apps
- **Purpose**: Managed container hosting platform
- **Features**:
  - Auto-scaling based on load
  - Managed ingress for HTTP endpoints
  - Container lifecycle management
  - Environment variable configuration
  - Health check integration

## CI/CD

### GitHub Actions
- **Purpose**: Automated build, test, and deployment pipelines
- **Workflows**: Located in `.github/workflows/`
- **Reusable Workflows**: Organization-level shared workflows
  - `Build and Publish Docker Image.yml`
  - `Deploy API to Azure Container App.yml`
  - `Deploy Messaging to Azure Container App.yml`
- **Triggers**: Push to specific paths, manual dispatch
- **Permissions**: Contents read, packages write, OIDC for Azure

### GitHub Container Registry
- **Registry**: `ghcr.io/acmetickets`
- **Purpose**: Docker image storage and versioning
- **Image Naming**: `{org}/{domain}-{component}` (e.g., `eventmgmt-api`)

## Testing

### xUnit
- **Purpose**: Unit and integration testing framework
- **Test Projects**:
  - `Test.UnitTests`: Fast, isolated unit tests
  - `Test.Mocks`: Reusable fake implementations

## Scripting & Automation

### Bash Scripts
- **Preference**: Bash over PowerShell for build and deployment tasks
- **Rationale**: Cross-platform compatibility, consistency with CI/CD

## Key Libraries & Dependencies

### Serialization
- **Newtonsoft.Json** (v13.0.3): JSON serialization/deserialization
- **Purpose**: API request/response, message payloads, Cosmos DB documents

### API & Documentation
- **Microsoft.AspNetCore.OpenApi** (v9.0.0): OpenAPI specification generation
- **Swashbuckle.AspNetCore** (v6.6.2): Swagger UI for API exploration
- **Purpose**: Interactive API documentation and testing

### Azure SDK
- **Azure.Identity** (v1.x): Managed identity and Azure AD authentication
- **Purpose**: Secure, credential-free access to Azure services

### HTTP
- **Microsoft.Extensions.Http** (v9.0.0): HttpClient factory and configuration
- **Purpose**: Resilient HTTP communication with external services

### Hosting
- **Microsoft.Extensions.Hosting** (v8.x): Generic host for background services
- **Purpose**: Application lifecycle, DI container, configuration

## Public Contracts

### AcmeTickets.Contracts.Public
- **Version**: 1.0.0
- **Purpose**: Shared message contracts across bounded contexts
- **Usage**: Cross-domain integration events

## Version Strategy

### Framework Versions
- **Major versions**: Explicitly specified (e.g., `net9.0`)
- **Package versions**: Use wildcards for minor/patch (e.g., `9.*`, `5.*`)
- **Rationale**: Automatic security patches, avoid breaking changes

### Transitive Dependencies
- Explicitly declared when needed for version control
- Example: `NServiceBus`, `Azure.Messaging.ServiceBus`

## Configuration Management

### Environment Configuration
- **Development**: `local.settings.json` (gitignored)
- **Templates**: `local.settings.json.template` (committed)
- **Production**: Azure Key Vault + App Configuration
- **Environment Variables**: Used in all hosting environments

### Connection Strings
- **Cosmos DB**: `CosmosDb__ConnectionString`
- **Service Bus**: `ServiceBus__ConnectionString`
- **Storage**: `AzureWebJobsStorage` (for Functions)

## Security

### Authentication & Authorization
- **Azure Managed Identity**: Preferred for Azure service access
- **Azure AD**: For user authentication (when applicable)
- **API Keys**: Azure Function authorization levels

### Secrets Management
- **Development**: Local configuration (never committed)
- **Production**: Azure Key Vault
- **CI/CD**: GitHub Secrets

## Observability

### Logging
- **Microsoft.Extensions.Logging**: Structured logging framework
- **Application Insights**: (Configuration ready, see logging.md for standards)

### Health Checks
- **ASP.NET Core Health Checks**: Available for container orchestration
- **(See health-checks.md for implementation details)**

## Why This Stack?

### .NET 9
- Modern, performant runtime
- Cross-platform support
- Strong typing and null safety
- Excellent tooling ecosystem

### NServiceBus + Azure Service Bus
- Proven messaging patterns
- Reliable message delivery
- Built-in retry and error handling
- Saga support for long-running workflows

### Cosmos DB
- Horizontal scalability
- Global distribution
- Flexible schema (NoSQL)
- Partitioning for performance

### Azure Container Apps
- Serverless container hosting
- Auto-scaling without orchestration complexity
- Simplified deployment model
- Cost-effective for variable workloads

### GitHub Actions
- Native GitHub integration
- Reusable workflows reduce duplication
- OIDC for secure Azure deployments
- Free for public repos, affordable for private
