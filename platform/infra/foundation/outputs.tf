# ============================================================================
# Resource Group Outputs
# ============================================================================

output "resource_group_name" {
  description = "Name of the resource group"
  value       = data.azurerm_resource_group.riskinsure.name
}

output "resource_group_id" {
  description = "ID of the resource group"
  value       = data.azurerm_resource_group.riskinsure.id
}

output "location" {
  description = "Azure region"
  value       = data.azurerm_resource_group.riskinsure.location
}

# ============================================================================
# Network Outputs
# ============================================================================

output "vnet_id" {
  description = "Virtual network ID"
  value       = azurerm_virtual_network.riskinsure.id
}

output "vnet_name" {
  description = "Virtual network name"
  value       = azurerm_virtual_network.riskinsure.name
}

output "container_apps_subnet_id" {
  description = "Container Apps subnet ID"
  value       = azurerm_subnet.container_apps.id
}

output "private_endpoints_subnet_id" {
  description = "Private endpoints subnet ID"
  value       = azurerm_subnet.private_endpoints.id
}

# ============================================================================
# Log Analytics & App Insights Outputs
# ============================================================================

output "log_analytics_workspace_id" {
  description = "Log Analytics workspace ID"
  value       = azurerm_log_analytics_workspace.riskinsure.id
}

output "log_analytics_workspace_key" {
  description = "Log Analytics workspace primary key"
  value       = azurerm_log_analytics_workspace.riskinsure.primary_shared_key
  sensitive   = true
}

output "application_insights_instrumentation_key" {
  description = "Application Insights instrumentation key"
  value       = azurerm_application_insights.riskinsure.instrumentation_key
  sensitive   = true
}

output "application_insights_connection_string" {
  description = "Application Insights connection string"
  value       = azurerm_application_insights.riskinsure.connection_string
  sensitive   = true
}

# ============================================================================
# Container Registry Outputs
# ============================================================================

output "acr_name" {
  description = "Container Registry name"
  value       = azurerm_container_registry.riskinsure.name
}

output "acr_login_server" {
  description = "Container Registry login server URL"
  value       = azurerm_container_registry.riskinsure.login_server
}

output "acr_id" {
  description = "Container Registry resource ID"
  value       = azurerm_container_registry.riskinsure.id
}

# ============================================================================
# Key Vault Outputs
# ============================================================================

output "key_vault_id" {
  description = "Key Vault resource ID"
  value       = azurerm_key_vault.riskinsure.id
}

output "key_vault_name" {
  description = "Key Vault name"
  value       = azurerm_key_vault.riskinsure.name
}

output "key_vault_uri" {
  description = "Key Vault URI"
  value       = azurerm_key_vault.riskinsure.vault_uri
}

# ============================================================================
# Storage Outputs
# ============================================================================

output "storage_account_name" {
  description = "Storage account name"
  value       = azurerm_storage_account.riskinsure.name
}

output "storage_account_primary_connection_string" {
  description = "Storage account primary connection string"
  value       = azurerm_storage_account.riskinsure.primary_connection_string
  sensitive   = true
}