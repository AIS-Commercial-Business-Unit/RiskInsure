# ==========================================================================
# Billing API (HTTP REST)
# ==========================================================================
# User-assigned identity and ACR role assignment are created in the
# foundation layer to ensure proper permissions for role assignments.
# ==========================================================================

resource "azurerm_container_app" "billing_api" {
  name                         = "billing-api"
  container_app_environment_id = azurerm_container_app_environment.riskinsure.id
  resource_group_name          = data.terraform_remote_state.foundation.outputs.resource_group_name
  revision_mode                = "Single"

  identity {
    type         = "UserAssigned"
    identity_ids = [data.terraform_remote_state.foundation.outputs.billing_api_identity_id]
  }

  registry {
    server   = data.terraform_remote_state.foundation.outputs.acr_login_server
    identity = data.terraform_remote_state.foundation.outputs.billing_api_identity_id
  }

  secret {
    name  = "cosmos-connection-string"
    value = data.terraform_remote_state.shared_services.outputs.cosmosdb_connection_string
  }

  secret {
    name  = "servicebus-connection-string"
    value = data.terraform_remote_state.shared_services.outputs.servicebus_connection_string
  }

  template {
    min_replicas = var.services["billing"].api.min_replicas
    max_replicas = var.services["billing"].api.max_replicas

    container {
      name   = "billing-api"
      image  = "${data.terraform_remote_state.foundation.outputs.acr_login_server}/billing-api:${var.image_tag}"
      cpu    = var.services["billing"].api.cpu
      memory = var.services["billing"].api.memory

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = var.environment == "prod" ? "Production" : "Development"
      }

      env {
        name  = "ASPNETCORE_URLS"
        value = "http://+:8080"
      }

      # Use Managed Identity for production
      env {
        name  = "CosmosDb__Endpoint"
        value = data.terraform_remote_state.shared_services.outputs.cosmosdb_endpoint
      }

      env {
        name  = "AzureServiceBus__FullyQualifiedNamespace"
        value = data.terraform_remote_state.shared_services.outputs.servicebus_namespace_fqdn
      }

      env {
        name        = "ConnectionStrings__CosmosDb"
        secret_name = "cosmos-connection-string"
      }

      env {
        name        = "ConnectionStrings__ServiceBus"
        secret_name = "servicebus-connection-string"
      }

      env {
        name  = "CosmosDb__DatabaseName"
        value = "RiskInsure"
      }

      env {
        name  = "CosmosDb__BillingContainerName"
        value = "Billing"
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
resource "azurerm_cosmosdb_sql_role_assignment" "billing_api_cosmos" {
  resource_group_name = data.terraform_remote_state.foundation.outputs.resource_group_name
  account_name        = data.terraform_remote_state.shared_services.outputs.cosmosdb_account_name
  role_definition_id  = "${data.terraform_remote_state.shared_services.outputs.cosmosdb_account_id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002"
  principal_id        = data.terraform_remote_state.foundation.outputs.billing_api_identity_principal_id
  scope               = data.terraform_remote_state.shared_services.outputs.cosmosdb_account_id
}

# Grant Service Bus access
resource "azurerm_role_assignment" "billing_api_servicebus" {
  scope                = data.terraform_remote_state.shared_services.outputs.servicebus_namespace_id
  role_definition_name = "Azure Service Bus Data Owner"
  principal_id         = data.terraform_remote_state.foundation.outputs.billing_api_identity_principal_id
}

# ==========================================================================
# Billing Endpoint (NServiceBus)
# ==========================================================================
# User-assigned identity and ACR role assignment are created in the
# foundation layer to ensure proper permissions for role assignments.
# ==========================================================================

resource "azurerm_container_app" "billing_endpoint" {
  name                         = "billing-endpoint"
  container_app_environment_id = azurerm_container_app_environment.riskinsure.id
  resource_group_name          = data.terraform_remote_state.foundation.outputs.resource_group_name
  revision_mode                = "Single"

  identity {
    type         = "UserAssigned"
    identity_ids = [data.terraform_remote_state.foundation.outputs.billing_endpoint_identity_id]
  }

  registry {
    server   = data.terraform_remote_state.foundation.outputs.acr_login_server
    identity = data.terraform_remote_state.foundation.outputs.billing_endpoint_identity_id
  }

  secret {
    name  = "cosmos-connection-string"
    value = data.terraform_remote_state.shared_services.outputs.cosmosdb_connection_string
  }

  secret {
    name  = "servicebus-connection-string"
    value = data.terraform_remote_state.shared_services.outputs.servicebus_connection_string
  }

  template {
    min_replicas = var.services["billing"].endpoint.min_replicas
    max_replicas = var.services["billing"].endpoint.max_replicas

    container {
      name   = "billing-endpoint"
      image  = "${data.terraform_remote_state.foundation.outputs.acr_login_server}/billing-endpoint:${var.image_tag}"
      cpu    = var.services["billing"].endpoint.cpu
      memory = var.services["billing"].endpoint.memory

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
        name        = "ConnectionStrings__CosmosDb"
        secret_name = "cosmos-connection-string"
      }

      env {
        name        = "ConnectionStrings__ServiceBus"
        secret_name = "servicebus-connection-string"
      }

      env {
        name  = "CosmosDb__DatabaseName"
        value = "RiskInsure"
      }

      env {
        name  = "CosmosDb__BillingContainerName"
        value = "Billing"
      }
    }
  }

  tags = var.tags
}

# Grant permissions
resource "azurerm_cosmosdb_sql_role_assignment" "billing_endpoint_cosmos" {
  resource_group_name = data.terraform_remote_state.foundation.outputs.resource_group_name
  account_name        = data.terraform_remote_state.shared_services.outputs.cosmosdb_account_name
  role_definition_id  = "${data.terraform_remote_state.shared_services.outputs.cosmosdb_account_id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002"
  principal_id        = data.terraform_remote_state.foundation.outputs.billing_endpoint_identity_principal_id
  scope               = data.terraform_remote_state.shared_services.outputs.cosmosdb_account_id
}

resource "azurerm_role_assignment" "billing_endpoint_servicebus" {
  scope                = data.terraform_remote_state.shared_services.outputs.servicebus_namespace_id
  role_definition_name = "Azure Service Bus Data Owner"
  principal_id         = data.terraform_remote_state.foundation.outputs.billing_endpoint_identity_principal_id
}