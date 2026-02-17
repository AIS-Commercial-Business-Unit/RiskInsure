# Deployment

## Overview

This bounded context uses GitHub Actions for CI/CD with Docker containerization and Azure Container Apps for hosting. The pipeline builds, publishes, and deploys both API and Message processor components.

## CI/CD Pipeline

### Workflow Location
`.github/workflows/CICD-Pipeline.yml`

### Trigger Conditions
```yaml
on:
  push:
    paths:
      - 'src/Api/**'
      - 'src/Message/**'
  workflow_dispatch:
```

- **Automatic**: Triggered on push to Api or Message project paths
- **Manual**: Can be triggered via workflow_dispatch

### Pipeline Permissions
```yaml
permissions:
  contents: read
  packages: write
  id-token: write
```

- **contents: read**: Access repository code
- **packages: write**: Publish to GitHub Container Registry
- **id-token: write**: OIDC authentication with Azure

## Reusable Workflows

### Build and Publish Docker Image
**Workflow**: `AcmeTickets/.github/.github/workflows/Build and Publish Docker Image.yml@main`

**Purpose**: Builds .NET project, creates Docker image, pushes to GHCR

**Inputs**:
- `project_path`: Path to .csproj file
- `dockerfile_path`: Path to Dockerfile
- `image_name`: Full image name with registry
- `target_port`: (Optional) Container port

**Outputs**:
- `image_tag`: Generated image tag for deployment

### Deploy API to Azure Container App
**Workflow**: `AcmeTickets/.github/.github/workflows/Deploy API to Azure Container App.yml@main`

**Purpose**: Deploys API container to Azure Container Apps

**Inputs**:
- `image_tag`: Docker image tag from build step
- `container_app_name`: Target Container App name
- `target_port`: Container port for ingress

### Deploy Messaging to Azure Container App
**Workflow**: `AcmeTickets/.github/.github/workflows/Deploy Messaging to Azure Container App.yml@main`

**Purpose**: Deploys message processor container to Azure Container Apps

**Inputs**:
- `image_tag`: Docker image tag from build step
- `container_app_name`: Target Container App name

## Pipeline Jobs

### 1. Build and Publish API
```yaml
build-and-publish-api:
  uses: AcmeTickets/.github/.github/workflows/Build and Publish Docker Image.yml@main
  secrets: inherit
  with:
    project_path: src/Api/Api.csproj
    dockerfile_path: src/Api/Dockerfile
    image_name: ghcr.io/acmetickets/eventmgmt-api
    target_port: 5271
```

### 2. Deploy API
```yaml
deploy-api:
  needs: build-and-publish-api
  uses: AcmeTickets/.github/.github/workflows/Deploy API to Azure Container App.yml@main
  secrets: inherit
  with:
    image_tag: ${{ needs.build-and-publish-api.outputs.image_tag }}
    container_app_name: eventmgmt-api
    target_port: 5271
```

### 3. Build and Publish Message Processor
```yaml
build-and-publish-message:
  uses: AcmeTickets/.github/.github/workflows/Build and Publish Docker Image.yml@main
  secrets: inherit
  with:
    project_path: src/Message/Message.csproj
    dockerfile_path: src/Message/Dockerfile
    image_name: ghcr.io/acmetickets/eventmgmt-msg
```

### 4. Deploy Message Processor
```yaml
deploy-message:
  needs: build-and-publish-message
  uses: AcmeTickets/.github/.github/workflows/Deploy Messaging to Azure Container App.yml@main
  secrets: inherit
  with:
    image_tag: ${{ needs.build-and-publish-message.outputs.image_tag }}
    container_app_name: eventmgmt-msg
```

## Docker Containerization

### Dockerfile Structure

**API Dockerfile** (`src/Api/Dockerfile`):
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5271

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/Api/Api.csproj", "Api/"]
COPY ["src/Application/Application.csproj", "Application/"]
COPY ["src/Domain/Domain.csproj", "Domain/"]
COPY ["src/Infrastructure/Infrastructure.csproj", "Infrastructure/"]
RUN dotnet restore "Api/Api.csproj"
COPY src/ .
WORKDIR "/src/Api"
RUN dotnet build "Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Api.dll"]
```

**Message Dockerfile** (`src/Message/Dockerfile`):
- Similar structure to API
- No EXPOSE directive (no HTTP ingress)
- Background processing workload

### Multi-Stage Build Benefits
- **Smaller images**: Final image contains only runtime + app
- **Build caching**: Faster subsequent builds
- **Security**: No build tools in production image

## GitHub Container Registry

### Image Naming Convention
```
ghcr.io/{organization}/{domain}-{component}
```

**Examples**:
- `ghcr.io/acmetickets/eventmgmt-api`
- `ghcr.io/acmetickets/eventmgmt-msg`

### Image Tagging
- **SHA-based**: `sha-{git-commit-sha}` (default)
- **Latest**: Updated on main branch deployments
- **Version tags**: Can be added for releases

### Authentication
- GitHub Actions authenticates automatically
- GHCR credentials required for local pulls
- Public visibility or package permissions for consumers

## Azure Container Apps Deployment

### Container App Configuration

**API Container App**:
- **Name**: `eventmgmt-api`
- **Port**: 5271
- **Ingress**: External, HTTP enabled
- **Scaling**: Auto-scale based on HTTP requests
- **Health Probes**: HTTP GET to health endpoint

**Message Container App**:
- **Name**: `eventmgmt-msg`
- **Port**: N/A (background processor)
- **Ingress**: None
- **Scaling**: Auto-scale based on queue depth or CPU
- **Health Probes**: Liveness check

### Environment Variables
Set via Container App configuration:
- `CosmosDb__ConnectionString`
- `RabbitMQ__ConnectionString`
- `ASPNETCORE_ENVIRONMENT`
- Azure Managed Identity configurations

### Secrets Management
- **Development**: Local environment variables
- **Production**: Azure Key Vault references
- **CI/CD**: GitHub Secrets

## Deployment Flow

```
Code Push
    ↓
GitHub Actions Triggered
    ↓
Build .NET Project
    ↓
Create Docker Image
    ↓
Push to GHCR
    ↓
Authenticate with Azure (OIDC)
    ↓
Update Container App
    ↓
Pull New Image
    ↓
Rolling Update
    ↓
Health Checks Pass
    ↓
Deployment Complete
```

## Environment Strategy

### Environments
1. **Development**: Local development, Cosmos emulator, local RabbitMQ
2. **Staging**: Azure resources, pre-production testing
3. **Production**: Azure resources, live traffic

### Configuration Per Environment
- Separate Container Apps per environment
- Environment-specific secrets
- Different resource group per environment
- Separate Cosmos DB databases

## Rollback Strategy

### Container App Revisions
- Azure Container Apps maintains revision history
- Can quickly revert to previous revision
- No re-build required

### Rollback Process
```bash
az containerapp revision list --name eventmgmt-api --resource-group rg-name
az containerapp revision activate --revision revision-name --resource-group rg-name
```

### Image Tags
- Can deploy specific image tag via workflow_dispatch
- Maintains image history in GHCR
- Pin to known-good versions if needed

## Monitoring Deployment

### GitHub Actions
- Real-time logs in Actions tab
- Job status and duration
- Artifact and image information

### Azure Container Apps
- Deployment logs in Azure Portal
- Revision status and traffic routing
- Container logs and metrics

### Health Checks
- HTTP health endpoint (`/health` or `/healthz`)
- Container readiness probes
- Liveness checks for restart on failure

## Local Development

### Building Locally
```bash
# Build API
docker build -f src/Api/Dockerfile -t eventmgmt-api:local .

# Build Message Processor
docker build -f src/Message/Dockerfile -t eventmgmt-msg:local .
```

### Running Locally
```bash
# Run API
docker run -p 5271:5271 \
  -e CosmosDb__ConnectionString="..." \
  -e RabbitMQ__ConnectionString="..." \
  eventmgmt-api:local

# Run Message Processor
docker run \
  -e CosmosDb__ConnectionString="..." \
  -e RabbitMQ__ConnectionString="..." \
  eventmgmt-msg:local
```

### Docker Compose (Optional)
```yaml
version: '3.8'
services:
  api:
    build:
      context: .
      dockerfile: src/Api/Dockerfile
    ports:
      - "5271:5271"
    environment:
      - CosmosDb__ConnectionString=${COSMOS_CONNECTION}
      - RabbitMQ__ConnectionString=${RABBITMQ_CONNECTION}
  
  message:
    build:
      context: .
      dockerfile: src/Message/Dockerfile
    environment:
      - CosmosDb__ConnectionString=${COSMOS_CONNECTION}
      - RabbitMQ__ConnectionString=${RABBITMQ_CONNECTION}
```

## Secrets Management

### GitHub Secrets
Required secrets for CI/CD:
- `AZURE_CLIENT_ID`: Service principal for OIDC
- `AZURE_TENANT_ID`: Azure AD tenant
- `AZURE_SUBSCRIPTION_ID`: Target subscription
- GitHub token (automatic)

### Azure Key Vault
Production secrets stored in Key Vault:
- Cosmos DB connection strings
- RabbitMQ connection strings
- External API keys

### Container App Secret References
```bash
az containerapp secret set \
  --name eventmgmt-api \
  --secrets cosmos-connection-string=keyvaultref:...
```

## Best Practices

### Do's ✅
1. Use reusable workflows for consistency
2. Tag images with commit SHA
3. Separate build and deploy stages
4. Use OIDC for Azure authentication (no stored credentials)
5. Monitor deployment metrics
6. Implement health checks
7. Use managed identity where possible
8. Keep Dockerfiles optimized
9. Test locally with Docker before pushing
10. Document environment-specific configurations

### Don'ts ❌
1. Don't commit secrets or credentials
2. Don't skip health checks
3. Don't deploy untested changes to production
4. Don't use `latest` tag for production
5. Don't ignore deployment failures
6. Don't skip Docker image vulnerability scanning
7. Don't hardcode environment-specific values
8. Don't forget to clean up old images/revisions

## Troubleshooting

### Build Failures
- Check project references and paths
- Verify Dockerfile COPY paths
- Ensure NuGet package restore succeeds
- Check .NET SDK version compatibility

### Deployment Failures
- Verify image exists in GHCR
- Check Container App configuration
- Validate environment variables
- Review Azure authentication/permissions

### Runtime Issues
- Check container logs in Azure Portal
- Verify environment variables set correctly
- Test health endpoints
- Review Application Insights (if configured)

## Additional Resources
- GitHub Actions Documentation
- Azure Container Apps Documentation
- Docker Best Practices
- GHCR Authentication Guide
