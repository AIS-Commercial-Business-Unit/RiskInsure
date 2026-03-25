output "cosmosdb_connection_string" {
  description = "Cosmos DB connection string (passed through from shared-services)"
  value       = data.terraform_remote_state.shared_services.outputs.cosmosdb_connection_string
  sensitive   = true
}

output "servicebus_connection_string" {
  description = "Service Bus connection string (passed through from shared-services)"
  value       = data.terraform_remote_state.shared_services.outputs.servicebus_connection_string
  sensitive   = true
}

output "fundstransfermgt_api_url" {
  description = "Funds Transfer Management API URL"
  value       = "https://${azurerm_container_app.fundstransfermgt_api.ingress[0].fqdn}"
}

output "customerrelationshipsmgt_api_url" {
  description = "Customer Relationship Management API URL"
  value       = "https://${azurerm_container_app.crmgt_api.ingress[0].fqdn}"
}

output "policyequityandinvoicingmgt_api_url" {
  description = "Policy Equity and Invoicing Management API URL"
  value       = "https://${azurerm_container_app.peimgt_api.ingress[0].fqdn}"
}

output "policylifecyclemgt_api_url" {
  description = "Policy Life Cycle Management API URL"
  value       = "https://${azurerm_container_app.policylifecyclemgt_api.ingress[0].fqdn}"
}

output "riskratingandunderwriting_api_url" {
  description = "Risk Rating & Underwriting API URL"
  value       = "https://${azurerm_container_app.rru_api.ingress[0].fqdn}"
}