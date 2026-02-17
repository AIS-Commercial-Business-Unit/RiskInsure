# Shared Services Layer

This layer provisions **shared infrastructure** that all microservices depend on:

- ✅ **Azure Cosmos DB**: NoSQL database for all services
- ✅ **RabbitMQ**: Message broker for event-driven architecture

## 📋 Prerequisites

The **00-foundation** layer must be deployed first.

## 🚀 Deployment

### Via GitHub Actions (Recommended)

```bash
# Go to: Actions → "1 - Infrastructure Provisioning"
# Select:
#   - Environment: dev
#   - Layer: shared-services
#   - Action: apply
```

### Manual Deployment

```bash
cd platform/infra/shared-services

# Initialize
terraform init

# Plan with environment-specific variables
terraform plan -var-file="dev.tfvars" -out=tfplan

# Apply
terraform apply tfplan
```

## 📊 Resources Created

| Resource | Purpose | Approximate Cost (dev) |
|----------|---------|------------------------|
| Cosmos DB Account | NoSQL database | $25-50/month (400 RU/s) |
| Cosmos DB Database | RiskInsure database | Included |
| Cosmos DB Containers (10x) | One per service + sagas | Included |
| RabbitMQ Broker | Message broker | Varies by hosting option |

**Total: Cosmos cost + broker hosting cost**

## 🔑 Connection Strings

After deployment, connection strings are automatically stored in Key Vault:

```bash
# Retrieve from Key Vault
az keyvault secret show \
  --vault-name riskinsure-dev-kv \
  --name CosmosDbConnectionString \
  --query value -o tsv

az keyvault secret show \
  --vault-name riskinsure-dev-kv \
  --name RabbitMQConnectionString \
  --query value -o tsv
```

## 📤 Outputs

This layer exports values consumed by the Container Apps layer:

- `cosmosdb_endpoint` → Used for Managed Identity authentication
- `cosmosdb_account_name` → Used for RBAC assignments
- `rabbitmq_connection_secret` → Broker connection for services

## 🔧 Configuration

### Development Environment

```bash
terraform apply -var-file="dev.tfvars"
```

Settings:
- Cosmos DB: Free tier enabled (if available)
- Cosmos DB: 400 RU/s per container
- RabbitMQ: Single broker instance for dev
- No geo-replication
- No private endpoints

### Production Environment

```bash
terraform apply -var-file="prod.tfvars"
```

Settings:
- Cosmos DB: 1000+ RU/s per container
- Cosmos DB: Automatic failover enabled
- Cosmos DB: Geo-replication (East US 2 + West US 2)
- RabbitMQ: Highly available broker cluster
- Private endpoints enabled

## 🔄 Updating Resources

### Scale Cosmos DB Throughput

Edit `dev.tfvars`:
```hcl
cosmosdb_throughput = 800  # Increase from 400
```

Then:
```bash
terraform apply -var-file="dev.tfvars"
```

### Add New Container

Edit `cosmosdb.tf` and add:
```hcl
resource "azurerm_cosmosdb_sql_container" "newservice" {
  name                  = "newservice"
  resource_group_name   = local.resource_group_name
  account_name          = azurerm_cosmosdb_account.riskinsure.name
  database_name         = azurerm_cosmosdb_sql_database.riskinsure.name
  partition_key_paths   = ["/id"]
  throughput            = var.cosmosdb_throughput
}
```

## 🗑️ Cleanup

**⚠️ WARNING:** This will DELETE all data!

```bash
terraform destroy -var-file="dev.tfvars"
```

Or use the GitHub Actions workflow: `3-destroy-infrastructure.yaml`

## 🔗 Next Steps

After deploying this layer:

1. ✅ Verify Cosmos DB is accessible: `https://<account-name>.documents.azure.com/`
2. ✅ Verify RabbitMQ broker is reachable
3. ✅ Deploy Container Apps: Run `2-build-and-deploy.yaml`