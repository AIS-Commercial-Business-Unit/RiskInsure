output "billing_api_url" {
  description = "Billing API URL"
  value       = "https://${azurerm_container_app.billing_api.ingress[0].fqdn}"
}

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

# output "customer_api_url" {
#   description = "Customer API URL"
#   value       = "https://${azurerm_container_app.customer_api.ingress[0].fqdn}"
# }

# output "policy_api_url" {
#   description = "Policy API URL"
#   value       = "https://${azurerm_container_app.policy_api.ingress[0].fqdn}"
# }

# output "fundstransfermgt_api_url" {
#   description = "Funds Transfer Management API URL"
#   value       = "https://${azurerm_container_app.fundstransfermgt_api.ingress[0].fqdn}"
# }

# output "ratingandunderwriting_api_url" {
#   description = "Rating & Underwriting API URL"
#   value       = "https://${azurerm_container_app.ratingandunderwriting_api.ingress[0].fqdn}"
# }
