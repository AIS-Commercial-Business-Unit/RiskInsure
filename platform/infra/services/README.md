# Container Apps Layer

This layer deploys the **RiskInsure microservices** as Azure Container Apps.

## 📋 Prerequisites

The following layers must be deployed first:
1. ✅ **foundation**: ACR, VNet, Key Vault, Log Analytics
2. ✅ **shared-services**: Cosmos DB, messaging broker resources

## 🚀 Deployment

### Via GitHub Actions (Recommended)

```bash
# Go to: Actions → "2 - Build & Deploy Microservices"
# Select:
#   - Environment: dev
#   - Services: all (or specific services)
```

This will:
1. Build Docker images for each service
2. Push images to ACR
3. Deploy/update Container Apps

### Manual Deployment

```bash
cd platform/infra/services

# Initialize
terraform init

# Plan with image tag
terraform plan \
  -var="image_tag=latest" \
  -var-file="dev.tfvars" \
  -out=tfplan

# Apply
terraform apply tfplan
```

## 📦 Services Deployed

Each service consists of **two containers**:

| Service | API Container | Endpoint Container | Purpose |
|---------|---------------|-------------------|---------|
| **FundsTransferMgt** | fundstransfermgt-api | fundstransfermgt-endpoint | Fund transfers |
| **CustomerRelationshipsMgt** | crmgt-api | crmgt-endpoint | Customer relationship management |
| **PolicyLifeCycleMgt** | policylifecyclemgt-api | policylifecyclemgt-endpoint | Policy lifecycle management |
| **PolicyEquityAndInvoicingMgt** | peimgt-api | peimgt-endpoint | Billing and invoicing |
| **RiskRatingAndUnderwriting** | rru-api | rru-endpoint | Risk rating and underwriting |

## 🔐 Authentication

All services use **Managed Identity** to access:
- ✅ Azure Cosmos DB (via `CosmosDb__Endpoint`)
- ✅ Messaging broker (credentials from configuration/secrets)

Avoid hard-coded connection strings in configuration files; use secrets/managed configuration. 🎉

## 📊 Resource Configuration

### Development Environment

```hcl
# dev.tfvars
services = {
  "policyequityandinvoicingmgt" = {
    api = {
      cpu          = 0.5
      memory       = "1Gi"
      min_replicas = 1
      max_replicas = 10
    }
    endpoint = {
      cpu          = 0.5
      memory       = "1Gi"
      min_replicas = 1
      max_replicas = 5
    }
  }
}
```

### Production Environment

```hcl
# prod.tfvars
services = {
  "policyequityandinvoicingmgt" = {
    api = {
      cpu          = 1.0
      memory       = "2Gi"
      min_replicas = 2
      max_replicas = 20
    }
    endpoint = {
      cpu          = 1.0
      memory       = "2Gi"
      min_replicas = 2
      max_replicas = 10
    }
  }
}
```

## 🔄 Updating Services

### Update Single Service

```bash
# Via GitHub Actions
# Go to: Actions → "2 - Build & Deploy Microservices"
# Services: peimgt
```

### Update All Services

```bash
# Via GitHub Actions
# Go to: Actions → "2 - Build & Deploy Microservices"
# Services: all
```

### Update Image Tag Only (No Rebuild)

```bash
cd platform/infra/services
terraform apply \
  -var="image_tag=abc123" \
  -var-file="dev.tfvars"
```

## 📤 Outputs

After deployment, access services via:

```bash
# Get API URLs
terraform output customerrelationshipsmgt_api_url
terraform output policylifecyclemgt_api_url
terraform output peimgt_api_url
terraform output riskratingandunderwriting_api_url

# Example output:
# https://peimgt-api.redbeach-12345.eastus2.azurecontainerapps.io
```

## 🔍 Monitoring

### View Logs

```bash
# Via Azure CLI
az containerapp logs show \
  --name peimgt-api \
  --resource-group CAIS-010-RiskInsure \
  --follow

# Via Azure Portal
# Container Apps → peimgt-api → Logs
```

### Health Checks

All APIs expose `/health` endpoint:

```bash
curl https://peimgt-api.xxx.azurecontainerapps.io/health
```

## 🔧 Scaling

### Manual Scaling

```bash
az containerapp update \
  --name peimgt-api \
  --resource-group CAIS-010-RiskInsure \
  --min-replicas 3 \
  --max-replicas 15
```

### Auto-scaling (KEDA)

Endpoints auto-scale based on queue depth metrics (configured via KEDA).

## 💰 Cost Estimate

### Development Environment
- 10 containers × $0.000024/second × 1 vCPU = ~$62/month (always-on)
- Actual cost lower with auto-scaling

### Production Environment
- Higher CPU/memory allocation
- More replicas
- Estimate: $200-500/month depending on traffic

## 🗑️ Cleanup

```bash
# Via GitHub Actions
# Run workflow: "3 - Destroy Infrastructure"

# Or manually
terraform destroy -var-file="dev.tfvars"
```

## 🔗 Service URLs

After deployment, services are accessible at:

```
https://customer-api.<unique-id>.eastus2.azurecontainerapps.io
https://fundstransfermgt-api.<unique-id>.eastus2.azurecontainerapps.io
https://crmgt-api.<unique-id>.eastus2.azurecontainerapps.io
https://policylifecyclemgt-api.<unique-id>.eastus2.azurecontainerapps.io
https://peimgt-api.<unique-id>.eastus2.azurecontainerapps.io
https://rru-api.<unique-id>.eastus2.azurecontainerapps.io
```

Swagger UI available at: `https://<service-url>/swagger`