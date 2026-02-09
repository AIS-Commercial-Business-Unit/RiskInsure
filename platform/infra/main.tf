# Azure Provider
provider "azurerm" {
  features {}
  
  # For local development: subscription_id can be hardcoded or set via Azure CLI
  # For GitHub Actions: ARM_SUBSCRIPTION_ID environment variable is used automatically
  # Uncomment below for local development if Azure CLI doesn't work:
  # subscription_id = "c4fb1c99-fb99-4dc1-9926-a3a4356fd44a"
}
# Azure Provider with backend
# provider "azurerm" {

#   features {}

# subscription_id = var.subscription_id
# client_id       = var.client_id
# client_secret   = var.client_secret
# tenant_id       = var.tenant_id

# }

# # TF State Backend
# terraform {
#   backend "azurerm" {
#     storage_account_name = "storageaccountdemostf"
#     container_name       = "terraform"
#     key                  = "terraform.tfstate"
#     use_msi              = false
#     subscription_id      = var.subscription_id
#     tenant_id            = var.tenant_id
#   }
# }

## An example resource that does nothing.
resource "null_resource" "example" {
  triggers = {
    value = "A example resource that does nothing!"
  }
}

# Use existing resource group (don't create new one)
data "azurerm_resource_group" "vmrg" {
  name = var.rgname
}

resource "random_id" "vm-sa" {
  keepers = {
    vm_hostname = var.vm_hostname
  }
  byte_length = 3
}

resource "random_id" "vm-ca" {
  keepers = {
    vm_hostname = var.vm_hostname
  }
  byte_length = 3
}

# resource "azurerm_storage_account" "storage" {
#   name                     = "bootdsk${lower(random_id.vm-sa.hex)}"
#   resource_group_name      = data.azurerm_resource_group.vmrg.name
#   location                 = data.azurerm_resource_group.vmrg.location
#   access_tier              = "Cool"
#   min_tls_version          = "TLS1_2"
#   account_tier             = element(split("_", var.account_tier), 0)
#   account_replication_type = element(split("_", var.account_tier), 1)
#   #tags = matchkeys(var.tags,"2")
#   tags = var.tags
# }
# resource "azurerm_storage_container" "container1" {
#   count      = 2
#   depends_on = [azurerm_storage_account.storage]
#   name       = "${var.Environment}-${var.Command}-${var.SubCommand}-${random_id.vm-ca.hex}-${count.index}"
#   # storage_account_name  = azurerm_storage_account.storage.name
#   storage_account_id    = azurerm_storage_account.storage.id
#   container_access_type = "private"
# }

# =============================================================================
# Azure Container Apps Environment
# =============================================================================

resource "azurerm_container_app_environment" "main" {
  name                       = "${var.project_name}-cae-${var.environment}"
  location                   = var.location
  resource_group_name        = var.resource_group_name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id

  tags = var.tags
}

resource "azurerm_log_analytics_workspace" "main" {
  name                = "${var.project_name}-logs-${var.environment}"
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = "PerGB2018"
  retention_in_days   = 30

  tags = var.tags
}

# =============================================================================
# Azure Container Registry
# =============================================================================

resource "azurerm_container_registry" "main" {
  name                = "${var.container_registry_name}${var.environment}"
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = "Basic"
  admin_enabled       = true

  tags = var.tags
}

# =============================================================================
# API Container Apps (one per service)
# =============================================================================

resource "azurerm_container_app" "api" {
  for_each = var.services

  name                         = "${var.project_name}-${each.key}-api-${var.environment}"
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = var.resource_group_name
  revision_mode                = "Single"

  template {
    container {
      name   = "${each.key}-api"
      image  = "${azurerm_container_registry.main.login_server}/${var.project_name}-${each.key}-api:latest"
      cpu    = each.value.cpu
      memory = each.value.memory

      # Environment variables matching docker-compose.yml
      env {
        name        = "ASPNETCORE_ENVIRONMENT"
        value       = var.environment == "prod" ? "Production" : "Development"
      }

      env {
        name        = "ASPNETCORE_URLS"
        value       = "http://+:8080"
      }

      env {
        name        = "CosmosDb__DatabaseName"
        value       = var.cosmos_database_name
      }

      env {
        name        = "CosmosDb__ContainerName"
        value       = each.value.container_name
      }

      # Secrets referenced as environment variables
      env {
        name        = "ConnectionStrings__CosmosDb"
        secret_name = "cosmosdb-connection-string"
      }

      env {
        name        = "ConnectionStrings__ServiceBus"
        secret_name = "servicebus-connection-string"
      }

      # Liveness probe
      liveness_probe {
        transport = "HTTP"
        path      = "/health"
        port      = 8080
        initial_delay    = 10
        interval_seconds = 30
        timeout          = 3
        failure_count_threshold = 3
      }

      # Readiness probe
      readiness_probe {
        transport = "HTTP"
        path      = "/health"
        port      = 8080
        interval_seconds = 10
        timeout          = 3
        failure_count_threshold = 3
      }
    }

    min_replicas = each.value.min_replicas
    max_replicas = each.value.max_replicas

    # HTTP scaling rule
    http_scale_rule {
      name                = "http-scaling"
      concurrent_requests = 100
    }
  }

  # External ingress for APIs
  ingress {
    external_enabled = true
    target_port      = 8080
    transport        = "http"

    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }

  # Secrets
  secret {
    name  = "cosmosdb-connection-string"
    value = azurerm_cosmosdb_account.main.primary_sql_connection_string
  }

  secret {
    name  = "servicebus-connection-string"
    value = azurerm_servicebus_namespace.main.default_primary_connection_string
  }

  # Registry credentials
  registry {
    server               = azurerm_container_registry.main.login_server
    username             = azurerm_container_registry.main.admin_username
    password_secret_name = "acr-password"
  }

  secret {
    name  = "acr-password"
    value = azurerm_container_registry.main.admin_password
  }

  tags = var.tags

  depends_on = [
    azurerm_cosmosdb_sql_container.main,
    azurerm_servicebus_namespace.main
  ]
}

# =============================================================================
# Endpoint Container Apps (NServiceBus message processors)
# =============================================================================

resource "azurerm_container_app" "endpoint" {
  for_each = { for k, v in var.services : k => v if v.has_endpoint }

  name                         = "${var.project_name}-${each.key}-endpoint-${var.environment}"
  container_app_environment_id = azurerm_container_app_environment.main.id
  resource_group_name          = var.resource_group_name
  revision_mode                = "Single"

  template {
    container {
      name   = "${each.key}-endpoint"
      image  = "${azurerm_container_registry.main.login_server}/${var.project_name}-${each.key}-endpoint:latest"
      cpu    = each.value.cpu
      memory = each.value.memory

      # Environment variables for NServiceBus endpoints
      env {
        name  = "DOTNET_ENVIRONMENT"
        value = var.environment == "prod" ? "Production" : "Development"
      }

      env {
        name  = "CosmosDb__DatabaseName"
        value = var.cosmos_database_name
      }

      env {
        name  = "CosmosDb__ContainerName"
        value = each.value.container_name
      }

      env {
        name        = "ConnectionStrings__CosmosDb"
        secret_name = "cosmosdb-connection-string"
      }

      env {
        name        = "ConnectionStrings__ServiceBus"
        secret_name = "servicebus-connection-string"
      }

      # For production: Use Managed Identity
      env {
        name  = "AzureServiceBus__FullyQualifiedNamespace"
        value = "${azurerm_servicebus_namespace.main.name}.servicebus.windows.net"
      }

      env {
        name  = "CosmosDb__Endpoint"
        value = azurerm_cosmosdb_account.main.endpoint
      }
    }

    min_replicas = each.value.min_replicas
    max_replicas = each.value.max_replicas

    # KEDA scaling rule for Service Bus queue depth
    custom_scale_rule {
      name             = "servicebus-queue-scaling"
      custom_rule_type = "azure-servicebus"
      metadata = {
        queueName      = "riskinsure.${each.key}.endpoint"
        messageCount   = "5"
        namespace      = azurerm_servicebus_namespace.main.name
      }
    }
  }

  # No ingress for message processors (internal only)
  
  secret {
    name  = "cosmosdb-connection-string"
    value = azurerm_cosmosdb_account.main.primary_sql_connection_string
  }

  secret {
    name  = "servicebus-connection-string"
    value = azurerm_servicebus_namespace.main.default_primary_connection_string
  }

  registry {
    server               = azurerm_container_registry.main.login_server
    username             = azurerm_container_registry.main.admin_username
    password_secret_name = "acr-password"
  }

  secret {
    name  = "acr-password"
    value = azurerm_container_registry.main.admin_password
  }

  tags = var.tags

  depends_on = [
    azurerm_container_app.api,
    azurerm_servicebus_queue.endpoints
  ]
}