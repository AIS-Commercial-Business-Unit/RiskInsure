output "azurerg" {
  value = "nameoftheresourcegroup-${data.azurerm_resource_group.vmrg.name}-at${formatdate("DD MMM YYYY hh:mm ZZZ", timestamp())}"
}
#
output "azurerg1" {
  value = "id-${data.azurerm_resource_group.vmrg.id}"
}

output "storage" {
  value = "id-${azurerm_storage_account.storage.id}"
}

output "container1" {
  value = azurerm_storage_container.container1[*].name
}

# =============================================================================
# Outputs
# =============================================================================

output "container_app_environment_id" {
  description = "Container Apps Environment ID"
  value       = azurerm_container_app_environment.main.id
}

output "container_registry_login_server" {
  description = "ACR login server"
  value       = azurerm_container_registry.main.login_server
}

output "container_registry_admin_username" {
  description = "ACR admin username"
  value       = azurerm_container_registry.main.admin_username
  sensitive   = true
}

output "api_urls" {
  description = "URLs for all API Container Apps"
  value = {
    for k, v in azurerm_container_app.api : k => "https://${v.ingress[0].fqdn}"
  }
}

output "cosmos_endpoint" {
  description = "Cosmos DB endpoint"
  value       = azurerm_cosmosdb_account.main.endpoint
}

output "cosmos_connection_string" {
  description = "Cosmos DB connection string"
  value       = azurerm_cosmosdb_account.main.primary_sql_connection_string
  sensitive   = true
}

output "servicebus_namespace" {
  description = "Service Bus namespace"
  value       = azurerm_servicebus_namespace.main.name
}

output "servicebus_connection_string" {
  description = "Service Bus connection string"
  value       = azurerm_servicebus_namespace.main.default_primary_connection_string
  sensitive   = true
}

output "log_analytics_workspace_id" {
  description = "Log Analytics Workspace ID for monitoring"
  value       = azurerm_log_analytics_workspace.main.id
}