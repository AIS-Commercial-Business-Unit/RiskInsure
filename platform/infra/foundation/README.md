# Foundation Infrastructure

This Terraform configuration provisions the **core foundation** for the RiskInsure platform. It should be deployed **first** and changes infrequently.

## What's Included

- ✅ **Resource Group**: Central resource group for all RiskInsure resources
- ✅ **Virtual Network**: VNet with subnets for Container Apps and private endpoints
- ✅ **Log Analytics Workspace**: Centralized logging for all services
- ✅ **Application Insights**: Distributed tracing and monitoring
- ✅ **Container Registry**: Docker image repository for microservices
- ✅ **Key Vault**: Secrets management (connection strings, NServiceBus license)
- ✅ **Storage Account**: Terraform state, backups, etc.
- ✅ **Network Security Groups**: Security rules for Container Apps

## Prerequisites

1. Azure CLI authenticated:
   ```bash
   az login
   az account set --subscription <subscription-id>
   ```

2. Terraform state storage account must exist:
   ```bash
   # Create resource group for Terraform state (one-time)
   az group create --name CAIS-010-RiskInsure --location eastus2

   # Create storage account for Terraform state (one-time)
   az storage account create \
     --name riskinsuretfstate \
     --resource-group CAIS-010-RiskInsure \
     --location eastus2 \
     --sku Standard_LRS

   # Create container for state files (one-time)
   az storage container create \
     --name tfstate \
     --account-name riskinsuretfstate
   ```

## Deployment

### Local Deployment

```bash
cd platform/infra/foundation

# Initialize Terraform
terraform init

# Review changes
terraform plan -var="environment=dev"

# Apply changes
terraform apply -var="environment=dev"

# Save outputs for next layers
terraform output -json > foundation-outputs.json
```

### GitHub Actions Deployment

The workflow file `.github/workflows/terraform-foundation.yaml` handles automated deployment.

## Outputs

This module exports critical values consumed by downstream layers:

| Output | Description | Used By |
|--------|-------------|---------|
| `resource_group_name` | RG name | All layers |
| `location` | Azure region | All layers |
| `container_apps_subnet_id` | Subnet for Container Apps | container-apps |
| `log_analytics_workspace_id` | Log Analytics ID | container-apps |
| `acr_login_server` | Container Registry URL | CI/CD pipelines |
| `key_vault_name` | Key Vault name | shared-services |

## Environment-Specific Configuration

Create `.tfvars` files for each environment:

**dev.tfvars:**
```hcl
environment         = "dev"
resource_group_name = "CAIS-010-RiskInsure-Dev"
location            = "eastus2"
vnet_address_space  = "10.0.0.0/16"
```

**prod.tfvars:**
```hcl
environment         = "prod"
resource_group_name = "CAIS-010-RiskInsure-Prod"
location            = "eastus2"
vnet_address_space  = "10.10.0.0/16"
```

Deploy with:
```bash
terraform apply -var-file="prod.tfvars"
```

## Cost Estimate (dev environment)

| Resource | SKU | Monthly Cost (approx) |
|----------|-----|-----------------------|
| Log Analytics | Pay-as-you-go | $2-10 |
| Application Insights | Pay-as-you-go | $2-10 |
| Container Registry | Basic | $5 |
| Key Vault | Standard | $0.03/10k ops |
| Storage Account | Standard LRS | $1-5 |
| **Total** | | **~$10-30/month** |

## Next Steps

After deploying foundation:

1. Deploy **shared-services** (Cosmos DB + Service Bus)
2. Deploy **container-apps** (microservices)
3. Deploy **03-monitoring** (dashboards, alerts)