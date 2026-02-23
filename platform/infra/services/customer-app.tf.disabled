# ==========================================================================
# Customer API (HTTP REST)
# ==========================================================================

resource "azurerm_container_app" "customer_api" {
  name                         = "customer-api"
  container_app_environment_id = azurerm_container_app_environment.riskinsure.id
  resource_group_name          = data.terraform_remote_state.foundation.outputs.resource_group_name
  revision_mode                = "Single"

  identity {
    type = "SystemAssigned"
  }

  template {
    min_replicas = var.services["customer"].api.min_replicas
    max_replicas = var.services["customer"].api.max_replicas

    container {
      name   = "customer-api"
      image  = "${data.terraform_remote_state.foundation.outputs.acr_login_server}/customer-api:${var.image_tag}"
      cpu    = var.services["customer"].api.cpu
      memory = var.services["customer"].api.memory

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = var.environment == "prod" ? "Production" : "Development"
      }

      env {
        name  = "ASPNETCORE_URLS"
        value = "http://+:8080"
      }

      env {
        name  = "CosmosDb__Endpoint"
        value = data.terraform_remote_state.shared_services.outputs.cosmosdb_endpoint
      }

      env {
        name  = "AzureServiceBus__FullyQualifiedNamespace"
        value = data.terraform_remote_state.shared_services.outputs.servicebus_namespace_fqdn
      }

      env {
        name  = "CosmosDb__DatabaseName"
        value = "RiskInsure"
      }

      env {
        name  = "CosmosDb__ContainerName"
        value = "customer"
      }
    }
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    
    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }

  tags = var.tags
}

# Grant Cosmos DB access
resource "azurerm_cosmosdb_sql_role_assignment" "customer_api_cosmos" {
  resource_group_name = data.terraform_remote_state.foundation.outputs.resource_group_name
  account_name        = data.terraform_remote_state.shared_services.outputs.cosmosdb_account_name
  role_definition_id  = "${data.terraform_remote_state.shared_services.outputs.cosmosdb_account_id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002"
  principal_id        = azurerm_container_app.customer_api.identity[0].principal_id
  scope               = data.terraform_remote_state.shared_services.outputs.cosmosdb_account_id
}

# Grant Service Bus access
resource "azurerm_role_assignment" "customer_api_servicebus" {
  scope                = data.terraform_remote_state.shared_services.outputs.servicebus_namespace_id
  role_definition_name = "Azure Service Bus Data Owner"
  principal_id         = azurerm_container_app.customer_api.identity[0].principal_id
}

# ==========================================================================
# Customer Endpoint (NServiceBus)
# ==========================================================================

resource "azurerm_container_app" "customer_endpoint" {
  name                         = "customer-endpoint"
  container_app_environment_id = azurerm_container_app_environment.riskinsure.id
  resource_group_name          = data.terraform_remote_state.foundation.outputs.resource_group_name
  revision_mode                = "Single"

  identity {
    type = "SystemAssigned"
  }

  template {
    min_replicas = var.services["customer"].endpoint.min_replicas
    max_replicas = var.services["customer"].endpoint.max_replicas

    container {
      name   = "customer-endpoint"
      image  = "${data.terraform_remote_state.foundation.outputs.acr_login_server}/customer-endpoint:${var.image_tag}"
      cpu    = var.services["customer"].endpoint.cpu
      memory = var.services["customer"].endpoint.memory

      env {
        name  = "DOTNET_ENVIRONMENT"
        value = var.environment == "prod" ? "Production" : "Development"
      }

      env {
        name  = "CosmosDb__Endpoint"
        value = data.terraform_remote_state.shared_services.outputs.cosmosdb_endpoint
      }

      env {
        name  = "AzureServiceBus__FullyQualifiedNamespace"
        value = data.terraform_remote_state.shared_services.outputs.servicebus_namespace_fqdn
      }

      env {
        name  = "CosmosDb__DatabaseName"
        value = "RiskInsure"
      }

      env {
        name  = "CosmosDb__ContainerName"
        value = "customer"
      }
    }
  }

  tags = var.tags
}

# Grant permissions
resource "azurerm_cosmosdb_sql_role_assignment" "customer_endpoint_cosmos" {
  resource_group_name = data.terraform_remote_state.foundation.outputs.resource_group_name
  account_name        = data.terraform_remote_state.shared_services.outputs.cosmosdb_account_name
  role_definition_id  = "${data.terraform_remote_state.shared_services.outputs.cosmosdb_account_id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002"
  principal_id        = azurerm_container_app.customer_endpoint.identity[0].principal_id
  scope               = data.terraform_remote_state.shared_services.outputs.cosmosdb_account_id
}

resource "azurerm_role_assignment" "customer_endpoint_servicebus" {
  scope                = data.terraform_remote_state.shared_services.outputs.servicebus_namespace_id
  role_definition_name = "Azure Service Bus Data Owner"
  principal_id         = azurerm_container_app.customer_endpoint.identity[0].principal_id
}