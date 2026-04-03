# ==========================================================================
# Shared Managed Identity Outputs
# ==========================================================================

output "apps_shared_identity_id" {
  description = "Shared Managed Identity ID (used by all Container Apps)"
  value       = azurerm_user_assigned_identity.apps_shared.id
}

output "apps_shared_identity_principal_id" {
  description = "Shared Managed Identity Principal ID"
  value       = azurerm_user_assigned_identity.apps_shared.principal_id
}

output "apps_shared_identity_client_id" {
  description = "Shared Managed Identity Client ID"
  value       = azurerm_user_assigned_identity.apps_shared.client_id
}

output "apps_shared_identity_name" {
  description = "Shared Managed Identity name"
  value       = azurerm_user_assigned_identity.apps_shared.name
}

# ==========================================================================
# Cosmos DB Outputs
# ==========================================================================

output "cosmosdb_endpoint" {
  description = "Cosmos DB account endpoint (for Managed Identity)"
  value       = azurerm_cosmosdb_account.riskinsure.endpoint
}

output "cosmosdb_account_name" {
  description = "Cosmos DB account name"
  value       = azurerm_cosmosdb_account.riskinsure.name
}

output "cosmosdb_account_id" {
  description = "Cosmos DB account resource ID"
  value       = azurerm_cosmosdb_account.riskinsure.id
}

output "cosmosdb_database_name" {
  description = "Cosmos DB database name"
  value       = azurerm_cosmosdb_sql_database.riskinsure.name
}

output "cosmosdb_primary_key" {
  description = "Cosmos DB primary key (sensitive - for dev/test)"
  value       = azurerm_cosmosdb_account.riskinsure.primary_key
  sensitive   = true
}

output "cosmosdb_connection_string" {
  description = "Cosmos DB connection string (sensitive - for dev/test)"
  value       = "AccountEndpoint=${azurerm_cosmosdb_account.riskinsure.endpoint};AccountKey=${azurerm_cosmosdb_account.riskinsure.primary_key};"
  sensitive   = true
}

# ==========================================================================
# Service Bus Outputs
# ==========================================================================

output "servicebus_namespace_fqdn" {
  description = "Service Bus fully qualified domain name (for Managed Identity)"
  value       = "${azurerm_servicebus_namespace.riskinsure.name}.servicebus.windows.net"
}

output "servicebus_namespace_name" {
  description = "Service Bus namespace name"
  value       = azurerm_servicebus_namespace.riskinsure.name
}

output "servicebus_namespace_id" {
  description = "Service Bus namespace resource ID"
  value       = azurerm_servicebus_namespace.riskinsure.id
}

output "servicebus_connection_string" {
  description = "Service Bus connection string (sensitive)"
  value       = azurerm_servicebus_namespace.riskinsure.default_primary_connection_string
  sensitive   = true
}

output "servicebus_primary_key" {
  description = "Service Bus primary key (sensitive)"
  value       = azurerm_servicebus_namespace.riskinsure.default_primary_key
  sensitive   = true
}

# ==========================================================================
# Azure AI Search Outputs
# ==========================================================================

output "ai_search_endpoint" {
  description = "Azure AI Search endpoint URL"
  value       = "https://${azurerm_search_service.riskinsure.name}.search.windows.net"
}

output "ai_search_name" {
  description = "Azure AI Search service name"
  value       = azurerm_search_service.riskinsure.name
}

output "ai_search_id" {
  description = "Azure AI Search resource ID"
  value       = azurerm_search_service.riskinsure.id
}

output "ai_search_primary_key" {
  description = "Azure AI Search primary admin key (sensitive)"
  value       = azurerm_search_service.riskinsure.primary_key
  sensitive   = true
}

# ==========================================================================
# Azure OpenAI (AI Foundry) Outputs
# ==========================================================================

output "ai_foundry_endpoint" {
  description = "Azure OpenAI endpoint URL"
  value       = azurerm_cognitive_account.openai.endpoint
}

output "ai_foundry_name" {
  description = "Azure OpenAI account name"
  value       = azurerm_cognitive_account.openai.name
}

output "ai_foundry_id" {
  description = "Azure OpenAI resource ID"
  value       = azurerm_cognitive_account.openai.id
}

output "ai_foundry_primary_key" {
  description = "Azure OpenAI primary access key (sensitive)"
  value       = azurerm_cognitive_account.openai.primary_access_key
  sensitive   = true
}

output "ai_foundry_chat_deployment_name" {
  description = "Name of the chat model deployment"
  value       = azurerm_cognitive_deployment.chat.name
}

output "ai_foundry_embedding_deployment_name" {
  description = "Name of the embedding model deployment"
  value       = azurerm_cognitive_deployment.embedding.name
}

# ==========================================================================
# Key Vault Secret IDs (for Container Apps secret references)
# ==========================================================================

output "kv_secret_azure_search_endpoint_id" {
  description = "Key Vault secret ID for Azure Search endpoint"
  value       = azurerm_key_vault_secret.azure_search_endpoint.id
}

output "kv_secret_azure_search_api_key_id" {
  description = "Key Vault secret ID for Azure Search API key"
  value       = azurerm_key_vault_secret.azure_search_api_key.id
}

output "kv_secret_azure_openai_endpoint_id" {
  description = "Key Vault secret ID for Azure OpenAI endpoint"
  value       = azurerm_key_vault_secret.azure_openai_endpoint.id
}

output "kv_secret_azure_openai_api_key_id" {
  description = "Key Vault secret ID for Azure OpenAI API key"
  value       = azurerm_key_vault_secret.azure_openai_api_key.id
}
