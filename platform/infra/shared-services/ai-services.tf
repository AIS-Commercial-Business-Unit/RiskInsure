# ==========================================================================
# Azure AI Services (AI Search + Azure OpenAI)
# ==========================================================================
# This file creates:
# - Azure AI Search (Free tier for dev, Basic for prod)
# - Azure OpenAI (Cognitive Account with S0 SKU - minimum required)
# - Model deployments (chat + embedding)
# - Key Vault secrets for endpoints and API keys
# - RBAC for the shared managed identity
# ==========================================================================

# ==========================================================================
# Azure AI Search
# ==========================================================================

resource "azurerm_search_service" "riskinsure" {
  name                = "riskinsure-aisearch-${var.environment}"
  resource_group_name = data.terraform_remote_state.foundation.outputs.resource_group_name
  location            = local.location
  sku                 = var.ai_search_sku # "free" for dev, "basic" for prod

  # Free tier limitations:
  # - 50 MB storage
  # - 10,000 documents
  # - 3 indexes
  # - No scaling options

  replica_count   = var.ai_search_sku == "free" ? 1 : var.ai_search_replica_count
  partition_count = var.ai_search_sku == "free" ? 1 : var.ai_search_partition_count

  public_network_access_enabled = true
  local_authentication_enabled  = true

  tags = local.common_tags
}

# ==========================================================================
# Azure OpenAI (Cognitive Services Account)
# ==========================================================================

resource "azurerm_cognitive_account" "openai" {
  name                = "riskinsure-foundry-${var.environment}"
  resource_group_name = data.terraform_remote_state.foundation.outputs.resource_group_name
  location            = var.ai_foundry_location # OpenAI may have limited regional availability
  kind                = "OpenAI"
  sku_name            = var.ai_foundry_sku # "S0" is the minimum (and only) SKU for OpenAI

  # Network access
  public_network_access_enabled = true

  # Identity for RBAC
  identity {
    type = "SystemAssigned"
  }

  tags = local.common_tags
}

# ==========================================================================
# Azure OpenAI Model Deployments
# ==========================================================================

# Chat model deployment (gpt-4.1-nano for cost efficiency)
resource "azurerm_cognitive_deployment" "chat" {
  name                 = var.ai_foundry_chat_deployment_name
  cognitive_account_id = azurerm_cognitive_account.openai.id

  model {
    format  = "OpenAI"
    name    = var.ai_foundry_chat_model
    version = var.ai_foundry_chat_model_version
  }

  sku {
    name     = "Standard"
    capacity = var.ai_foundry_chat_capacity # Tokens per minute in thousands
  }
}

# Embedding model deployment
resource "azurerm_cognitive_deployment" "embedding" {
  name                 = var.ai_foundry_embedding_deployment_name
  cognitive_account_id = azurerm_cognitive_account.openai.id

  model {
    format  = "OpenAI"
    name    = var.ai_foundry_embedding_model
    version = var.ai_foundry_embedding_model_version
  }

  sku {
    name     = "Standard"
    capacity = var.ai_foundry_embedding_capacity # Tokens per minute in thousands
  }
}

# ==========================================================================
# Key Vault Secrets for AI Services
# ==========================================================================

resource "azurerm_key_vault_secret" "azure_search_endpoint" {
  name         = "AzureSearchEndpoint"
  value        = "https://${azurerm_search_service.riskinsure.name}.search.windows.net"
  key_vault_id = data.terraform_remote_state.foundation.outputs.key_vault_id

  tags = local.common_tags
}

resource "azurerm_key_vault_secret" "azure_search_api_key" {
  name         = "AzureSearchApiKey"
  value        = azurerm_search_service.riskinsure.primary_key
  key_vault_id = data.terraform_remote_state.foundation.outputs.key_vault_id

  tags = local.common_tags
}

resource "azurerm_key_vault_secret" "azure_openai_endpoint" {
  name         = "AzureOpenAIEndpoint"
  value        = azurerm_cognitive_account.openai.endpoint
  key_vault_id = data.terraform_remote_state.foundation.outputs.key_vault_id

  tags = local.common_tags
}

resource "azurerm_key_vault_secret" "azure_openai_api_key" {
  name         = "AzureOpenAIApiKey"
  value        = azurerm_cognitive_account.openai.primary_access_key
  key_vault_id = data.terraform_remote_state.foundation.outputs.key_vault_id

  tags = local.common_tags
}

# ==========================================================================
# RBAC for Shared Managed Identity
# ==========================================================================

# Grant Cognitive Services OpenAI User role to UAMI for Azure OpenAI access
resource "azurerm_role_assignment" "apps_shared_openai" {
  scope                = azurerm_cognitive_account.openai.id
  role_definition_name = "Cognitive Services OpenAI User"
  principal_id         = azurerm_user_assigned_identity.apps_shared.principal_id
}

# Grant Search Index Data Contributor role to UAMI for AI Search access
resource "azurerm_role_assignment" "apps_shared_search_index" {
  scope                = azurerm_search_service.riskinsure.id
  role_definition_name = "Search Index Data Contributor"
  principal_id         = azurerm_user_assigned_identity.apps_shared.principal_id
}

# Grant Search Service Contributor role to UAMI for AI Search management
resource "azurerm_role_assignment" "apps_shared_search_service" {
  scope                = azurerm_search_service.riskinsure.id
  role_definition_name = "Search Service Contributor"
  principal_id         = azurerm_user_assigned_identity.apps_shared.principal_id
}
