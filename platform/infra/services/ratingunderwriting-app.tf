# ==========================================================================
# Rating & Underwriting API (HTTP REST)
# ==========================================================================

resource "azurerm_container_app" "ratingandunderwriting_api" {
  name                         = "ratingandunderwriting-api"
  container_app_environment_id = azurerm_container_app_environment.riskinsure.id
  resource_group_name          = data.terraform_remote_state.foundation.outputs.resource_group_name
  revision_mode                = "Single"

  identity {
    type         = "UserAssigned"
    identity_ids = [data.terraform_remote_state.shared_services.outputs.apps_shared_identity_id]
  }

  registry {
    server   = data.terraform_remote_state.foundation.outputs.acr_login_server
    identity = data.terraform_remote_state.shared_services.outputs.apps_shared_identity_id
  }

  secret {
    name                = "cosmos-connection-string"
    key_vault_secret_id = data.azurerm_key_vault_secret.cosmos_db_connection_string.id
    identity            = data.terraform_remote_state.shared_services.outputs.apps_shared_identity_id
  }

  secret {
    name                = "servicebus-connection-string"
    key_vault_secret_id = data.azurerm_key_vault_secret.service_bus_connection_string.id
    identity            = data.terraform_remote_state.shared_services.outputs.apps_shared_identity_id
  }

  template {
    min_replicas = var.services["ratingandunderwriting"].api.min_replicas
    max_replicas = var.services["ratingandunderwriting"].api.max_replicas

    container {
      name   = "ratingandunderwriting-api"
      image  = "${data.terraform_remote_state.foundation.outputs.acr_login_server}/ratingandunderwriting-api:${var.image_tag}"
      cpu    = var.services["ratingandunderwriting"].api.cpu
      memory = var.services["ratingandunderwriting"].api.memory

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
        value = "ratingunderwriting"
      }

      env {
        name        = "ConnectionStrings__CosmosDb"
        secret_name = "cosmos-connection-string"
      }

      env {
        name        = "ConnectionStrings__ServiceBus"
        secret_name = "servicebus-connection-string"
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

# Note: Rating & Underwriting API uses shared UAMI which already has Cosmos DB and Service Bus roles assigned

# ==========================================================================
# Rating & Underwriting Endpoint (NServiceBus)
# ==========================================================================

resource "azurerm_container_app" "ratingandunderwriting_endpoint" {
  name                         = "ratingandunderwriting-endpoint"
  container_app_environment_id = azurerm_container_app_environment.riskinsure.id
  resource_group_name          = data.terraform_remote_state.foundation.outputs.resource_group_name
  revision_mode                = "Single"

  identity {
    type         = "UserAssigned"
    identity_ids = [data.terraform_remote_state.shared_services.outputs.apps_shared_identity_id]
  }

  registry {
    server   = data.terraform_remote_state.foundation.outputs.acr_login_server
    identity = data.terraform_remote_state.shared_services.outputs.apps_shared_identity_id
  }

  secret {
    name                = "cosmos-connection-string"
    key_vault_secret_id = data.azurerm_key_vault_secret.cosmos_db_connection_string.id
    identity            = data.terraform_remote_state.shared_services.outputs.apps_shared_identity_id
  }

  secret {
    name                = "servicebus-connection-string"
    key_vault_secret_id = data.azurerm_key_vault_secret.service_bus_connection_string.id
    identity            = data.terraform_remote_state.shared_services.outputs.apps_shared_identity_id
  }

  template {
    min_replicas = var.services["ratingandunderwriting"].endpoint.min_replicas
    max_replicas = var.services["ratingandunderwriting"].endpoint.max_replicas

    container {
      name   = "ratingandunderwriting-endpoint"
      image  = "${data.terraform_remote_state.foundation.outputs.acr_login_server}/ratingandunderwriting-endpoint:${var.image_tag}"
      cpu    = var.services["ratingandunderwriting"].endpoint.cpu
      memory = var.services["ratingandunderwriting"].endpoint.memory

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
        value = "ratingunderwriting"
      }

      env {
        name        = "ConnectionStrings__CosmosDb"
        secret_name = "cosmos-connection-string"
      }

      env {
        name        = "ConnectionStrings__ServiceBus"
        secret_name = "servicebus-connection-string"
      }
    }

  }

  tags = var.tags
}

# Note: Rating & Underwriting Endpoint uses shared UAMI which already has Cosmos DB and Service Bus roles assigned

# ==========================================================================
# Rating & Underwriting Endpoint KEDA Scaler
# ==========================================================================

resource "azurerm_container_app_custom_scaler" "ratingandunderwriting_endpoint_scaler" {
  count = var.enable_keda_scaling ? 1 : 0

  container_app_id = azurerm_container_app.ratingandunderwriting_endpoint.id
  name             = "ratingandunderwriting-endpoint-queue-scaler"

  authentication {
    secret_name       = "servicebus-connection-string"
    trigger_parameter = "connection"
  }

  custom {
    type = "azure-servicebus"

    metadata = {
      namespace           = split("/", data.terraform_remote_state.shared_services.outputs.servicebus_namespace_fqdn)[0]
      queueName           = "RiskInsure.RatingUnderwriting.Endpoint.In"
      messageCount        = tostring(var.keda_service_bus_queue_length)
      sharedAccessKeyName = "RootManageSharedAccessKey"
      connection          = "servicebus-connection-string"
    }
  }

  depends_on = [azurerm_container_app.ratingandunderwriting_endpoint]
}
