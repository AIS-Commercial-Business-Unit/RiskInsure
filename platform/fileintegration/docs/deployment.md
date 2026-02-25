# Deployment Guide: File Retrieval Service

**T134**: Comprehensive deployment guide for Azure Container Apps

## Prerequisites

- Azure subscription with appropriate permissions
- Azure CLI installed (`az version >= 2.50.0`)
- Docker installed for local testing
- .NET 10 SDK installed
- Access to Azure Key Vault for secrets

## Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                  Azure Container Apps                    │
│                                                          │
│  ┌──────────────────┐      ┌─────────────────────────┐ │
│  │  API Container   │      │  Worker Container        │ │
│  │  (Public HTTP)   │      │  (Background Service)    │ │
│  │                  │      │  - Scheduler             │ │
│  │  - REST API      │      │  - Message Handlers      │ │
│  │  - JWT Auth      │      │  - File Check Execution  │ │
│  └──────────────────┘      └─────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
                     │                │
                     ├────────────────┼────────────────┐
                     ▼                ▼                ▼
        ┌───────────────────┐  ┌──────────────┐  ┌──────────────┐
        │  Cosmos DB        │  │ Service Bus  │  │  Key Vault   │
        │  - Configurations │  │ - Commands   │  │  - Secrets   │
        │  - Executions     │  │ - Events     │  │  - Creds     │
        │  - Files          │  └──────────────┘  └──────────────┘
        └───────────────────┘
```

## Step 1: Infrastructure Setup

### 1.1 Create Resource Group

```bash
RESOURCE_GROUP="riskinsure-file-retrieval-prod"
LOCATION="eastus"
ENVIRONMENT="production"

az group create \
  --name $RESOURCE_GROUP \
  --location $LOCATION \
  --tags environment=$ENVIRONMENT component=file-retrieval
```

### 1.2 Create Cosmos DB Account

```bash
COSMOS_ACCOUNT="riskinsure-fileretr-cosmos-prod"

az cosmosdb create \
  --name $COSMOS_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --locations regionName=$LOCATION failoverPriority=0 isZoneRedundant=False \
  --default-consistency-level Session \
  --enable-automatic-failover false \
  --tags environment=$ENVIRONMENT

# Get connection details
COSMOS_ENDPOINT=$(az cosmosdb show --name $COSMOS_ACCOUNT --resource-group $RESOURCE_GROUP --query documentEndpoint -o tsv)
COSMOS_KEY=$(az cosmosdb keys list --name $COSMOS_ACCOUNT --resource-group $RESOURCE_GROUP --query primaryMasterKey -o tsv)
```

### 1.3 Create Cosmos DB Database and Containers

Run the setup script:

```bash
cd services/file-retrieval/scripts/DatabaseSetup
pwsh ./setup-cosmosdb.ps1 -CosmosEndpoint $COSMOS_ENDPOINT -CosmosKey $COSMOS_KEY -DatabaseName "file-retrieval"
```

Or use Azure CLI (follow instructions from setup script output).

### 1.4 Create Azure Service Bus Namespace

```bash
SERVICEBUS_NAMESPACE="riskinsure-fileretr-sb-prod"

az servicebus namespace create \
  --name $SERVICEBUS_NAMESPACE \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku Standard \
  --tags environment=$ENVIRONMENT

# Get connection string
SERVICEBUS_CONNECTION=$(az servicebus namespace authorization-rule keys list \
  --resource-group $RESOURCE_GROUP \
  --namespace-name $SERVICEBUS_NAMESPACE \
  --name RootManageSharedAccessKey \
  --query primaryConnectionString -o tsv)
```

### 1.5 Create Azure Key Vault

```bash
KEY_VAULT_NAME="riskinsure-fr-kv-prod"

az keyvault create \
  --name $KEY_VAULT_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku standard \
  --tags environment=$ENVIRONMENT

# Store connection strings
az keyvault secret set --vault-name $KEY_VAULT_NAME --name "CosmosDbConnectionString" --value "$COSMOS_ENDPOINT|$COSMOS_KEY"
az keyvault secret set --vault-name $KEY_VAULT_NAME --name "ServiceBusConnectionString" --value "$SERVICEBUS_CONNECTION"
```

### 1.6 Create Application Insights

```bash
APP_INSIGHTS_NAME="riskinsure-fileretr-ai-prod"

az monitor app-insights component create \
  --app $APP_INSIGHTS_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --tags environment=$ENVIRONMENT

# Get instrumentation key
APPINSIGHTS_KEY=$(az monitor app-insights component show \
  --app $APP_INSIGHTS_NAME \
  --resource-group $RESOURCE_GROUP \
  --query instrumentationKey -o tsv)
```

## Step 2: Build Container Images

### 2.1 Build API Image

```bash
cd services/file-retrieval

# Build API container
docker build -f src/FileRetrieval.API/Dockerfile -t riskinsure-fileretr-api:latest .

# Tag for Azure Container Registry
CONTAINER_REGISTRY="riskinsureacr"
docker tag riskinsure-fileretr-api:latest $CONTAINER_REGISTRY.azurecr.io/file-retrieval-api:1.0.0
docker tag riskinsure-fileretr-api:latest $CONTAINER_REGISTRY.azurecr.io/file-retrieval-api:latest
```

### 2.2 Build Worker Image

```bash
# Build Worker container
docker build -f src/FileRetrieval.Worker/Dockerfile -t riskinsure-fileretr-worker:latest .

# Tag for Azure Container Registry
docker tag riskinsure-fileretr-worker:latest $CONTAINER_REGISTRY.azurecr.io/file-retrieval-worker:1.0.0
docker tag riskinsure-fileretr-worker:latest $CONTAINER_REGISTRY.azurecr.io/file-retrieval-worker:latest
```

### 2.3 Push Images to Registry

```bash
# Login to ACR
az acr login --name $CONTAINER_REGISTRY

# Push images
docker push $CONTAINER_REGISTRY.azurecr.io/file-retrieval-api:1.0.0
docker push $CONTAINER_REGISTRY.azurecr.io/file-retrieval-api:latest
docker push $CONTAINER_REGISTRY.azurecr.io/file-retrieval-worker:1.0.0
docker push $CONTAINER_REGISTRY.azurecr.io/file-retrieval-worker:latest
```

## Step 3: Deploy to Azure Container Apps

### 3.1 Create Container Apps Environment

```bash
CONTAINERAPPS_ENV="riskinsure-fileretr-env-prod"

az containerapp env create \
  --name $CONTAINERAPPS_ENV \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --logs-workspace-id $(az monitor app-insights component show --app $APP_INSIGHTS_NAME --resource-group $RESOURCE_GROUP --query workspaceResourceId -o tsv)
```

### 3.2 Create Managed Identity

```bash
MANAGED_IDENTITY="riskinsure-fileretr-identity"

az identity create \
  --name $MANAGED_IDENTITY \
  --resource-group $RESOURCE_GROUP

# Get identity details
IDENTITY_ID=$(az identity show --name $MANAGED_IDENTITY --resource-group $RESOURCE_GROUP --query id -o tsv)
IDENTITY_CLIENT_ID=$(az identity show --name $MANAGED_IDENTITY --resource-group $RESOURCE_GROUP --query clientId -o tsv)

# Grant Key Vault access
az keyvault set-policy \
  --name $KEY_VAULT_NAME \
  --object-id $(az identity show --name $MANAGED_IDENTITY --resource-group $RESOURCE_GROUP --query principalId -o tsv) \
  --secret-permissions get list
```

### 3.3 Deploy API Container App

```bash
API_APP_NAME="riskinsure-fileretr-api"

az containerapp create \
  --name $API_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --environment $CONTAINERAPPS_ENV \
  --image $CONTAINER_REGISTRY.azurecr.io/file-retrieval-api:latest \
  --registry-server $CONTAINER_REGISTRY.azurecr.io \
  --cpu 1.0 \
  --memory 2.0Gi \
  --min-replicas 2 \
  --max-replicas 10 \
  --ingress external \
  --target-port 8080 \
  --user-assigned $IDENTITY_ID \
  --env-vars \
    ASPNETCORE_ENVIRONMENT=Production \
    CosmosDb__Endpoint=$COSMOS_ENDPOINT \
    CosmosDb__DatabaseName=file-retrieval \
    ServiceBus__ConnectionString=secretref:servicebus-connection \
    KeyVault__VaultUri=https://$KEY_VAULT_NAME.vault.azure.net/ \
    ApplicationInsights__InstrumentationKey=$APPINSIGHTS_KEY \
  --secrets \
    servicebus-connection=$SERVICEBUS_CONNECTION

# Enable health probes
az containerapp update \
  --name $API_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --health-probe-type liveness \
  --health-probe-path /health/live \
  --health-probe-interval 30 \
  --health-probe-timeout 10 \
  --health-probe-failure-threshold 3
```

### 3.4 Deploy Worker Container App

```bash
WORKER_APP_NAME="riskinsure-fileretr-worker"

az containerapp create \
  --name $WORKER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --environment $CONTAINERAPPS_ENV \
  --image $CONTAINER_REGISTRY.azurecr.io/file-retrieval-worker:latest \
  --registry-server $CONTAINER_REGISTRY.azurecr.io \
  --cpu 2.0 \
  --memory 4.0Gi \
  --min-replicas 1 \
  --max-replicas 5 \
  --user-assigned $IDENTITY_ID \
  --env-vars \
    ASPNETCORE_ENVIRONMENT=Production \
    CosmosDb__Endpoint=$COSMOS_ENDPOINT \
    CosmosDb__DatabaseName=file-retrieval \
    ServiceBus__ConnectionString=secretref:servicebus-connection \
    KeyVault__VaultUri=https://$KEY_VAULT_NAME.vault.azure.net/ \
    ApplicationInsights__InstrumentationKey=$APPINSIGHTS_KEY \
  --secrets \
    servicebus-connection=$SERVICEBUS_CONNECTION

# Enable health probes
az containerapp update \
  --name $WORKER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --health-probe-type liveness \
  --health-probe-path /health/live \
  --health-probe-interval 30 \
  --health-probe-timeout 10 \
  --health-probe-failure-threshold 3
```

## Step 4: Post-Deployment Configuration

### 4.1 Configure API CORS (if needed)

```bash
az containerapp update \
  --name $API_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --set properties.configuration.cors.allowedOrigins="https://riskinsure.com","https://admin.riskinsure.com"
```

### 4.2 Enable Container App Metrics

```bash
az monitor diagnostic-settings create \
  --name ContainerAppMetrics \
  --resource $(az containerapp show --name $API_APP_NAME --resource-group $RESOURCE_GROUP --query id -o tsv) \
  --workspace $(az monitor app-insights component show --app $APP_INSIGHTS_NAME --resource-group $RESOURCE_GROUP --query workspaceResourceId -o tsv) \
  --metrics '[{"category":"AllMetrics","enabled":true}]'
```

### 4.3 Get API URL

```bash
API_URL=$(az containerapp show --name $API_APP_NAME --resource-group $RESOURCE_GROUP --query properties.configuration.ingress.fqdn -o tsv)
echo "API URL: https://$API_URL"
```

## Step 5: Verification

### 5.1 Check Health Endpoints

```bash
curl https://$API_URL/health/live
curl https://$API_URL/health/ready
```

### 5.2 Verify Container Logs

```bash
# API logs
az containerapp logs show --name $API_APP_NAME --resource-group $RESOURCE_GROUP --follow

# Worker logs
az containerapp logs show --name $WORKER_APP_NAME --resource-group $RESOURCE_GROUP --follow
```

### 5.3 Test API Endpoint

```bash
# Get JWT token (implement your auth flow)
TOKEN="your-jwt-token"

# Create test configuration
curl -X POST https://$API_URL/api/configuration \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Test Configuration",
    "protocol": "HTTPS",
    "filePathPattern": "/files/{yyyy}/{mm}",
    "filenamePattern": "data_{yyyy}{mm}{dd}.csv",
    "schedule": {
      "cronExpression": "0 2 * * *",
      "timezone": "America/New_York"
    },
    "eventsToPublish": [{
      "eventType": "FileDiscovered",
      "eventData": {}
    }]
  }'
```

## Step 6: Scaling Configuration

### 6.1 Configure Auto-Scaling Rules

```bash
# API autoscaling based on HTTP requests
az containerapp update \
  --name $API_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --scale-rule-name http-rule \
  --scale-rule-type http \
  --scale-rule-http-concurrency 50

# Worker autoscaling based on Service Bus queue length
az containerapp update \
  --name $WORKER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --scale-rule-name queue-rule \
  --scale-rule-type azure-servicebus \
  --scale-rule-metadata queueName=file-check-commands messageCount=10 \
  --scale-rule-auth secretRef=servicebus-connection
```

## Monitoring and Maintenance

### Log Queries (Application Insights)

```kql
// Recent errors
traces
| where severityLevel >= 3
| where timestamp > ago(1h)
| project timestamp, message, customDimensions
| order by timestamp desc

// API request duration
requests
| summarize avg(duration), percentile(duration, 95) by name
| order by avg_duration desc
```

### Backup Strategy

- **Cosmos DB**: Continuous backup enabled (30-day retention)
- **Key Vault**: Soft-delete enabled (90-day retention)
- **Container Images**: Retain last 10 tagged versions in ACR

### Update Strategy

1. Build and push new image version
2. Update container app with new image tag
3. Monitor health probes and logs
4. Rollback if issues detected: `az containerapp revision list` and `az containerapp revision activate`

---

**Last Updated**: 2025-01-24  
**Owner**: Platform Engineering Team  
**Related**: [monitoring.md](./monitoring.md), [runbook.md](./runbook.md)
