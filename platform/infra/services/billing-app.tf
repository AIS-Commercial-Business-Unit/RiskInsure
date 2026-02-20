# ==========================================================================
# Billing API (HTTP REST)
# ==========================================================================

resource "azurerm_container_app" "billing_api" {
  name                         = "billing-api"
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

# ==========================================================================
# Billing Endpoint (NServiceBus)
# ==========================================================================

resource "azurerm_container_app" "billing_endpoint" {
  name                         = "billing-endpoint"
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

    # KEDA Scale Rules for NServiceBus Endpoint
    dynamic "scale_rule" {
      for_each = var.enable_keda_scaling ? [1] : []
      content {
        name             = "billing-endpoint-queue-scaler"
        custom_rule_type = "azure-servicebus"
        custom_rule_data = {
          "namespace"      = split("/", data.terraform_remote_state.shared_services.outputs.servicebus_namespace_fqdn)[0]
          "queueName"      = "billing.payment-instruction-received"
          "messageCount"   = tostring(var.keda_service_bus_queue_length)
          "sharedAccessKeyName" = "RootManageSharedAccessKey"
          "sharedAccessKey"     = "@Microsoft.KeyVault(SecretUri=${data.azurerm_key_vault_secret.service_bus_connection_string.id})"
        }
      }
    }
  }

  tags = var.tags
}
