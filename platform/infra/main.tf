# --------------------------
# Provider
# --------------------------
provider "azurerm" {
  features {}
}

# --------------------------
# Resource Group (existing)
# --------------------------
data "azurerm_resource_group" "vmrg" {
  name = var.rgname
}

# --------------------------
# Random ID
# --------------------------
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
# Cosmos DB
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

resource "azurerm_cosmosdb_sql_database" "billing_db" {
  name                = "billing-db"
  resource_group_name = data.azurerm_resource_group.vmrg.name
  account_name        = azurerm_cosmosdb_account.cosmos.name
}

resource "azurerm_cosmosdb_sql_container" "billing_container" {
  name                = "billing-container"
  resource_group_name = data.azurerm_resource_group.vmrg.name
  account_name        = azurerm_cosmosdb_account.cosmos.name
  database_name       = azurerm_cosmosdb_sql_database.billing_db.name
  partition_key_paths = ["/accountId"]
  throughput          = 400
}

# --------------------------
# Azure Service Bus
# --------------------------
resource "azurerm_servicebus_namespace" "servicebus" {
  name                = "servicebus-${random_id.vm_ca.hex}"
  location            = data.azurerm_resource_group.vmrg.location
  resource_group_name = data.azurerm_resource_group.vmrg.name
  sku                 = "Standard"
  tags                = var.tags
}

resource "azurerm_servicebus_queue" "billing_queue" {
  name         = "billing-queue"
  namespace_id = azurerm_servicebus_namespace.servicebus.id
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

  identity {
    type = "SystemAssigned"
  }

  # ACR secret
  secret {
    name  = "acr-password"
    value = azurerm_container_registry.acr.admin_password
  }

  template {
    container {
      name   = "billing-api"
      image  = "${azurerm_container_registry.acr.login_server}/${var.container_app_image}"
      cpu    = 0.5
      memory = "1Gi"

      # Pass Cosmos DB connection string (for dev/test)
      env {
        name  = "CosmosDb__ConnectionString"
        value = var.cosmos_connection_string  # <- provide it in terraform.tfvars or environment
      }

      # Pass Service Bus connection string (for dev/test)
      env {
        name  = "AzureWebJobsServiceBus"
        value = var.servicebus_connection_string  # <- provide it in terraform.tfvars or environment
      }

      # Keep endpoint and fully qualified namespace if needed
      env {
        name  = "CosmosDb__Endpoint"
        value = azurerm_cosmosdb_account.cosmos.endpoint
      }

      env {
        name  = "AzureServiceBus__FullyQualifiedNamespace"
        value = "${azurerm_servicebus_namespace.servicebus.name}.servicebus.windows.net"
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
    azurerm_container_registry.acr,
    azurerm_servicebus_queue.billing_queue,
    azurerm_servicebus_namespace.servicebus
  ]
}


# --------------------------
# Fetch Container App identity for RBAC
# --------------------------
data "azurerm_container_app" "app_identity" {
  name                = azurerm_container_app.app.name
  resource_group_name = data.azurerm_resource_group.vmrg.name

  depends_on = [
    azurerm_container_app.app
  ]
}

 # required permission
# --------------------------
# RBAC - Service Bus
# --------------------------
# resource "azurerm_role_assignment" "servicebus_role" {
#   principal_id         = azurerm_container_app.app.identity[0].principal_id
#   role_definition_name = "Azure Service Bus Data Sender"
#   scope                = azurerm_servicebus_namespace.servicebus.id

#   depends_on = [
#     azurerm_container_app.app
#   ]
# }

# # --------------------------
# # RBAC - Cosmos DB
# # --------------------------
# resource "azurerm_role_assignment" "cosmos_role" {
#   principal_id         = azurerm_container_app.app.identity[0].principal_id
#   role_definition_name = "Cosmos DB Account Reader Role"
#   scope                = azurerm_cosmosdb_account.cosmos.id

#   depends_on = [
#     azurerm_container_app.app
#   ]
# }


# --------------------------
# Outputs
# --------------------------
output "container_app_name" {
  value = azurerm_container_app.app.name
}

output "servicebus_namespace" {
  value = azurerm_servicebus_namespace.servicebus.name
}

output "cosmos_endpoint" {
  value = azurerm_cosmosdb_account.cosmos.endpoint
}
