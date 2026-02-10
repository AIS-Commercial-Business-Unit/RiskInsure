# --------------------------
# Provider
# --------------------------
provider "azurerm" {
  features {}
}

# --------------------------
# Example null resource
# --------------------------
resource "null_resource" "example" {
  triggers = {
    value = "An example resource that does nothing!"
  }
}

# --------------------------
# Use existing resource group
# --------------------------
data "azurerm_resource_group" "vmrg" {
  name = var.rgname
}

# --------------------------
# Random IDs
# --------------------------
resource "random_id" "vm_sa" {
  keepers = {
    vm_hostname = var.vm_hostname
  }
  byte_length = 3
}

resource "random_id" "vm_ca" {
  keepers = {
    vm_hostname = var.vm_hostname
  }
  byte_length = 3
}

# --------------------------
# Log Analytics Workspace
# --------------------------
resource "azurerm_log_analytics_workspace" "law" {
  name                = "law-${random_id.vm_ca.hex}"
  location            = data.azurerm_resource_group.vmrg.location
  resource_group_name = data.azurerm_resource_group.vmrg.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = var.tags
}

# --------------------------
# Container App Environment
# --------------------------
resource "azurerm_container_app_environment" "env" {
  name                       = "env-${random_id.vm_ca.hex}"
  location                   = data.azurerm_resource_group.vmrg.location
  resource_group_name        = data.azurerm_resource_group.vmrg.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.law.id
  tags                       = var.tags
}

# --------------------------
# Azure Container Registry
# --------------------------
resource "azurerm_container_registry" "acr" {
  name                = "acr${random_id.vm_ca.hex}"
  location            = data.azurerm_resource_group.vmrg.location
  resource_group_name = data.azurerm_resource_group.vmrg.name
  sku                 = "Basic"
  admin_enabled       = true
  tags                = var.tags
}

# --------------------------
# Cosmos DB Account
# --------------------------
resource "azurerm_cosmosdb_account" "cosmos" {
  name                = "cosmos-${random_id.vm_ca.hex}"
  location            = data.azurerm_resource_group.vmrg.location
  resource_group_name = data.azurerm_resource_group.vmrg.name
  offer_type          = "Standard"
  kind                = "GlobalDocumentDB"

  consistency_policy {
    consistency_level = "Session"
  }

  geo_location {
    location          = data.azurerm_resource_group.vmrg.location
    failover_priority = 0
  }

  automatic_failover_enabled = true
  tags                       = var.tags
}

# --------------------------
# Cosmos DB SQL Database
# --------------------------
resource "azurerm_cosmosdb_sql_database" "billing_db" {
  name                = "billing-db"
  resource_group_name = data.azurerm_resource_group.vmrg.name
  account_name        = azurerm_cosmosdb_account.cosmos.name
}

# --------------------------
# Cosmos DB SQL Container
# --------------------------
resource "azurerm_cosmosdb_sql_container" "billing_container" {
  name                = "billing-container"
  resource_group_name = data.azurerm_resource_group.vmrg.name
  account_name        = azurerm_cosmosdb_account.cosmos.name
  database_name       = azurerm_cosmosdb_sql_database.billing_db.name
  partition_key_paths = ["/accountId"]
  throughput          = 400
}

# --------------------------
# Azure Container App
# --------------------------
resource "azurerm_container_app" "app" {
  name                         = "app-${random_id.vm_ca.hex}"
  resource_group_name          = data.azurerm_resource_group.vmrg.name
  container_app_environment_id = azurerm_container_app_environment.env.id
  revision_mode                = "Single"
  tags                         = var.tags

  # ACR Password Secret
  secret {
    name  = "acr-password"
    value = azurerm_container_registry.acr.admin_password
  }

  # Cosmos Connection String Secret
  secret {
    name  = "cosmos-conn"
    value = azurerm_cosmosdb_account.cosmos.primary_sql_connection_string
  }

  secret {
  name  = "servicebus-conn"
  value = azurerm_servicebus_namespace.servicebus.default_primary_connection_string
}

  template {
    container {
      name   = "billing-api"
      image  = "${azurerm_container_registry.acr.login_server}/${var.container_app_image}"
      cpu    = 0.5
      memory = "1Gi"

      # ACR Password (Optional)
      env {
        name        = "ACR_PASSWORD"
        secret_name = "acr-password"
      }

      # Cosmos Connection String (Matches Program.cs)
      env {
        name        = "ConnectionStrings__CosmosDb"
        secret_name = "cosmos-conn"
        }

      # Cosmos Database Name (Matches Program.cs)
      env {
        name  = "CosmosDb__DatabaseName"
        value = var.cosmos_database_name
      }

      # Cosmos Container Name (Matches Program.cs EXACTLY)
      env {
        name  = "CosmosDb__BillingContainerName"
        value = var.cosmos_container_name
      }
      # REQUIRED by NServiceBus (this was missing)
      env {
        name  = "AzureServiceBus__FullyQualifiedNamespace"
        value = "${azurerm_servicebus_namespace.servicebus.name}.servicebus.windows.net"
          }

# Service Bus connection string
      env {
        name        = "AzureServiceBus__ConnectionString"
        secret_name = "servicebus-conn"
          }

    }

    min_replicas = 1
    max_replicas = 3
  }

  ingress {
    external_enabled = true
    target_port      = 8080

    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }

  registry {
    server               = azurerm_container_registry.acr.login_server
    username             = azurerm_container_registry.acr.admin_username
    password_secret_name = "acr-password"
  }

  depends_on = [
    azurerm_cosmosdb_sql_container.billing_container,
    azurerm_container_registry.acr
  ]
}

# --------------------------
# Azure Service Bus Namespace
# --------------------------
resource "azurerm_servicebus_namespace" "servicebus" {
  name                = "servicebus-${random_id.vm_ca.hex}"
  location            = data.azurerm_resource_group.vmrg.location
  resource_group_name = data.azurerm_resource_group.vmrg.name
  sku                 = "Standard"
  tags                = var.tags
}

# --------------------------
# Azure Service Bus Queue
# --------------------------
resource "azurerm_servicebus_queue" "billing_queue" {
  name            = "billing-queue"
  namespace_id    = azurerm_servicebus_namespace.servicebus.id
  max_size_in_megabytes = 1024
  lock_duration   = "PT5M"
}

# --------------------------
# Outputs
# --------------------------
output "acr_login_server" {
  value = azurerm_container_registry.acr.login_server
}

output "container_app_name" {
  value = azurerm_container_app.app.name
}

output "cosmos_endpoint" {
  value = azurerm_cosmosdb_account.cosmos.endpoint
}

output "cosmos_connection_string" {
  value     = azurerm_cosmosdb_account.cosmos.connection_strings[0]
  sensitive = true
}

output "servicebus_namespace" {
  value = azurerm_servicebus_namespace.servicebus.name
}

output "servicebus_connection_string" {
  value     = azurerm_servicebus_namespace.servicebus.default_primary_connection_string
  sensitive = true
}

# Add missing storage account resource
resource "azurerm_storage_account" "storage" {
  name                     = "storage${random_id.vm_ca.hex}"
  resource_group_name      = data.azurerm_resource_group.vmrg.name
  location                 = data.azurerm_resource_group.vmrg.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  tags                     = var.tags
}

# Add missing storage container resource
resource "azurerm_storage_container" "container1" {
  name                  = "examplecontainer"
  storage_account_name  = azurerm_storage_account.storage.name
  container_access_type = "private"
}
