# ==========================================================================
# Modernization Patterns - Chat API & Reindex Worker
# ==========================================================================
# This file defines Container Apps for the Modernization Patterns system:
# - Chat API: RAG-based chatbot for querying modernization patterns
# - Reindex Worker: Indexes content into Azure AI Search with embeddings
# ==========================================================================

# ==========================================================================
# Key Vault Secrets for Azure AI Services
# ==========================================================================

data "azurerm_key_vault_secret" "azure_search_endpoint" {
  name         = "AzureSearchEndpoint"
  key_vault_id = data.azurerm_key_vault.riskinsure.id
}

data "azurerm_key_vault_secret" "azure_search_api_key" {
  name         = "AzureSearchApiKey"
  key_vault_id = data.azurerm_key_vault.riskinsure.id
}

data "azurerm_key_vault_secret" "azure_openai_endpoint" {
  name         = "AzureOpenAIEndpoint"
  key_vault_id = data.azurerm_key_vault.riskinsure.id
}

data "azurerm_key_vault_secret" "azure_openai_api_key" {
  name         = "AzureOpenAIApiKey"
  key_vault_id = data.azurerm_key_vault.riskinsure.id
}

# ==========================================================================
# Modernization Patterns Chat API (HTTP REST)
# ==========================================================================

resource "azurerm_container_app" "modernizationpatterns_chat_api" {
  name                         = "modernizationpatterns-chat-api"
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
    name                = "azure-search-endpoint"
    key_vault_secret_id = data.azurerm_key_vault_secret.azure_search_endpoint.id
    identity            = data.terraform_remote_state.shared_services.outputs.apps_shared_identity_id
  }

  secret {
    name                = "azure-search-api-key"
    key_vault_secret_id = data.azurerm_key_vault_secret.azure_search_api_key.id
    identity            = data.terraform_remote_state.shared_services.outputs.apps_shared_identity_id
  }

  secret {
    name                = "azure-openai-endpoint"
    key_vault_secret_id = data.azurerm_key_vault_secret.azure_openai_endpoint.id
    identity            = data.terraform_remote_state.shared_services.outputs.apps_shared_identity_id
  }

  secret {
    name                = "azure-openai-api-key"
    key_vault_secret_id = data.azurerm_key_vault_secret.azure_openai_api_key.id
    identity            = data.terraform_remote_state.shared_services.outputs.apps_shared_identity_id
  }

  secret {
    name                = "cosmos-connection-string"
    key_vault_secret_id = data.azurerm_key_vault_secret.cosmos_db_connection_string.id
    identity            = data.terraform_remote_state.shared_services.outputs.apps_shared_identity_id
  }

  template {
    min_replicas = var.modernizationpatterns_chat_api.min_replicas
    max_replicas = var.modernizationpatterns_chat_api.max_replicas

    container {
      name   = "modernizationpatterns-chat-api"
      image  = "${data.terraform_remote_state.foundation.outputs.acr_login_server}/modernizationpatterns-chat-api:${var.image_tag}"
      cpu    = var.modernizationpatterns_chat_api.cpu
      memory = var.modernizationpatterns_chat_api.memory

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = var.environment == "prod" ? "Production" : "Development"
      }

      env {
        name  = "ASPNETCORE_URLS"
        value = "http://+:8080"
      }

      # Azure AI Search configuration
      env {
        name        = "AzureSearch__Endpoint"
        secret_name = "azure-search-endpoint"
      }

      env {
        name        = "AzureSearch__ApiKey"
        secret_name = "azure-search-api-key"
      }

      env {
        name  = "AzureSearch__IndexName"
        value = var.modernizationpatterns_search_index_name
      }

      # Azure OpenAI configuration (for chat completions)
      env {
        name        = "AzureOpenAI__Endpoint"
        secret_name = "azure-openai-endpoint"
      }

      env {
        name        = "AzureOpenAI__ApiKey"
        secret_name = "azure-openai-api-key"
      }

      env {
        name  = "AzureOpenAI__ChatDeploymentName"
        value = var.modernizationpatterns_chat_deployment
      }

      env {
        name  = "AzureOpenAI__EmbeddingDeploymentName"
        value = "text-embedding-3-small"
      }

      # Cosmos DB configuration
      env {
        name        = "ConnectionStrings__CosmosDb"
        secret_name = "cosmos-connection-string"
      }

      env {
        name  = "CosmosDb__DatabaseName"
        value = "modernization-patterns-db"
      }

      # Application Insights
      env {
        name  = "ApplicationInsights__ConnectionString"
        value = data.terraform_remote_state.foundation.outputs.application_insights_connection_string
      }

      env {
        name  = "APPLICATIONINSIGHTS_CONNECTION_STRING"
        value = data.terraform_remote_state.foundation.outputs.application_insights_connection_string
      }

      # Health probes for Chat API on port 8080
      liveness_probe {
        path             = "/health"
        port             = 8080
        transport        = "HTTP"
        initial_delay    = 10
        interval_seconds = 30
      }

      readiness_probe {
        path             = "/health/ready"
        port             = 8080
        transport        = "HTTP"
        initial_delay    = 5
        interval_seconds = 10
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

  tags = merge(var.tags, {
    Service   = "ModernizationPatterns"
    Component = "ChatAPI"
  })
}

# ==========================================================================
# Modernization Patterns Reindex Worker
# ==========================================================================

resource "azurerm_container_app" "modernizationpatterns_reindex_worker" {
  name                         = "modernizationpatterns-reindex"
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
    name                = "azure-search-endpoint"
    key_vault_secret_id = data.azurerm_key_vault_secret.azure_search_endpoint.id
    identity            = data.terraform_remote_state.shared_services.outputs.apps_shared_identity_id
  }

  secret {
    name                = "azure-search-api-key"
    key_vault_secret_id = data.azurerm_key_vault_secret.azure_search_api_key.id
    identity            = data.terraform_remote_state.shared_services.outputs.apps_shared_identity_id
  }

  secret {
    name                = "azure-openai-endpoint"
    key_vault_secret_id = data.azurerm_key_vault_secret.azure_openai_endpoint.id
    identity            = data.terraform_remote_state.shared_services.outputs.apps_shared_identity_id
  }

  secret {
    name                = "azure-openai-api-key"
    key_vault_secret_id = data.azurerm_key_vault_secret.azure_openai_api_key.id
    identity            = data.terraform_remote_state.shared_services.outputs.apps_shared_identity_id
  }

  template {
    min_replicas = var.modernizationpatterns_reindex_worker.min_replicas
    max_replicas = var.modernizationpatterns_reindex_worker.max_replicas

    # HTTP scaling rule - required for scale-to-zero to work
    # Without this, container won't scale up on incoming HTTP requests
    http_scale_rule {
      name                = "http-scaling"
      concurrent_requests = 10 # Scale up when 10+ concurrent requests
    }

    container {
      name   = "modernizationpatterns-reindex"
      image  = "${data.terraform_remote_state.foundation.outputs.acr_login_server}/modernizationpatterns-reindex:${var.image_tag}"
      cpu    = var.modernizationpatterns_reindex_worker.cpu
      memory = var.modernizationpatterns_reindex_worker.memory

      env {
        name  = "DOTNET_ENVIRONMENT"
        value = var.environment == "prod" ? "Production" : "Development"
      }

      # Azure AI Search configuration
      env {
        name        = "AzureSearch__Endpoint"
        secret_name = "azure-search-endpoint"
      }

      env {
        name        = "AzureSearch__ApiKey"
        secret_name = "azure-search-api-key"
      }

      env {
        name  = "AzureSearch__IndexName"
        value = var.modernizationpatterns_search_index_name
      }

      # Azure OpenAI configuration (for embeddings)
      env {
        name        = "AzureOpenAI__Endpoint"
        secret_name = "azure-openai-endpoint"
      }

      env {
        name        = "AzureOpenAI__ApiKey"
        secret_name = "azure-openai-api-key"
      }

      env {
        name  = "AzureOpenAI__EmbeddingDeploymentName"
        value = var.modernizationpatterns_embedding_deployment
      }

      # Application Insights
      env {
        name  = "ApplicationInsights__ConnectionString"
        value = data.terraform_remote_state.foundation.outputs.application_insights_connection_string
      }

      env {
        name  = "APPLICATIONINSIGHTS_CONNECTION_STRING"
        value = data.terraform_remote_state.foundation.outputs.application_insights_connection_string
      }

      # Port binding
      env {
        name  = "ASPNETCORE_URLS"
        value = "http://+:5010"
      }

      # Health probes
      liveness_probe {
        path             = "/health"
        port             = 5010
        transport        = "HTTP"
        initial_delay    = 10
        interval_seconds = 30
      }

      readiness_probe {
        path             = "/health/ready"
        port             = 5010
        transport        = "HTTP"
        initial_delay    = 5
        interval_seconds = 10
      }
    }
  }

  # Reindex worker has HTTP endpoint for triggering reindex via API
  ingress {
    external_enabled = true # External access required for GitHub Actions workflow
    target_port      = 5010

    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }

  tags = merge(var.tags, {
    Service   = "ModernizationPatterns"
    Component = "ReindexWorker"
  })
}

# ==========================================================================
# Outputs
# ==========================================================================

output "modernizationpatterns_chat_api_fqdn" {
  description = "FQDN of the Modernization Patterns Chat API"
  value       = azurerm_container_app.modernizationpatterns_chat_api.latest_revision_fqdn
}

output "modernizationpatterns_chat_api_url" {
  description = "URL of the Modernization Patterns Chat API"
  value       = "https://${azurerm_container_app.modernizationpatterns_chat_api.latest_revision_fqdn}"
}

output "modernizationpatterns_reindex_worker_fqdn" {
  description = "FQDN of the Modernization Patterns Reindex Worker (internal)"
  value       = azurerm_container_app.modernizationpatterns_reindex_worker.latest_revision_fqdn
}
