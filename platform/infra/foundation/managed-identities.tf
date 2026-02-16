# ============================================================================
# User-Assigned Managed Identities for Container Apps
# ============================================================================
# These identities are created here (foundation layer) so that ACR role
# assignments can be granted with proper permissions. The services layer
# will reference these identities via remote state outputs.
# ============================================================================

# Billing API Identity
resource "azurerm_user_assigned_identity" "billing_api" {
  name                = "billing-api-identity"
  resource_group_name = data.azurerm_resource_group.riskinsure.name
  location            = data.azurerm_resource_group.riskinsure.location
  tags                = var.tags
}

# Grant AcrPull role to Billing API identity
resource "azurerm_role_assignment" "billing_api_acr_pull" {
  scope                = azurerm_container_registry.riskinsure.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.billing_api.principal_id
}

# Billing Endpoint Identity
resource "azurerm_user_assigned_identity" "billing_endpoint" {
  name                = "billing-endpoint-identity"
  resource_group_name = data.azurerm_resource_group.riskinsure.name
  location            = data.azurerm_resource_group.riskinsure.location
  tags                = var.tags
}

# Grant AcrPull role to Billing Endpoint identity
resource "azurerm_role_assignment" "billing_endpoint_acr_pull" {
  scope                = azurerm_container_registry.riskinsure.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.billing_endpoint.principal_id
}
