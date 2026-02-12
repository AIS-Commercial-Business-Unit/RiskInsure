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
  description = "Service Bus connection string (sensitive - for dev/test)"
  value       = var.environment == "dev" ? data.azurerm_servicebus_namespace_authorization_rule.root_manage.primary_connection_string : null
  sensitive   = true
}

output "servicebus_primary_key" {
  description = "Service Bus primary key (sensitive)"
  value       = var.environment == "dev" ? data.azurerm_servicebus_namespace_authorization_rule.root_manage.primary_key : null
  sensitive   = true
}
