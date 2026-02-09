# This is part of infrastructure.yaml (rarely changes)

resource "azurerm_container_app_environment" "riskinsure" {
  name                = "riskinsure-${var.environment}-env"
  location            = var.location
  resource_group_name = var.resource_group_name
  
  log_analytics_workspace_id = data.terraform_remote_state.foundation.outputs.log_analytics_workspace_id
  
  tags = var.tags
}