terraform {
  required_version = ">= 1.11.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = ">= 3.0.0"
    }
  }

  backend "azurerm" {
    resource_group_name  = "CAIS-010-RiskInsure"
    storage_account_name = "riskinsuretfstate"
    container_name       = "tfstate"
    key                  = "services.tfstate"
    use_oidc             = true
  }
}

provider "azurerm" {
  features {}
}

# ==========================================================================
# Data sources from previous layers
# ==========================================================================

data "terraform_remote_state" "foundation" {
  backend = "azurerm"
  config = {
    resource_group_name  = "CAIS-010-RiskInsure"
    storage_account_name = "riskinsuretfstate"
    container_name       = "tfstate"
    key                  = "foundation.tfstate"
    use_oidc             = true
  }
}

data "terraform_remote_state" "shared_services" {
  backend = "azurerm"
  config = {
    resource_group_name  = "CAIS-010-RiskInsure"
    storage_account_name = "riskinsuretfstate"
    container_name       = "tfstate"
    key                  = "shared-services.tfstate"
    use_oidc             = true
  }
}

# ==========================================================================
# Key Vault - retrieve secrets
# ==========================================================================

data "azurerm_key_vault" "riskinsure" {
  name                = "riskinsure-${var.environment}-kv"
  resource_group_name = data.terraform_remote_state.foundation.outputs.resource_group_name
}

data "azurerm_key_vault_secret" "cosmos_db_connection_string" {
  name         = "CosmosDbConnectionString"
  key_vault_id = data.azurerm_key_vault.riskinsure.id
}

data "azurerm_key_vault_secret" "service_bus_connection_string" {
  name         = "ServiceBusConnectionString"
  key_vault_id = data.azurerm_key_vault.riskinsure.id
}

# ==========================================================================
# Container Apps Environment
# ==========================================================================

resource "azurerm_container_app_environment" "riskinsure" {
  name                       = "riskinsure-${var.environment}-env"
  location                   = data.terraform_remote_state.foundation.outputs.location
  resource_group_name        = data.terraform_remote_state.foundation.outputs.resource_group_name
  log_analytics_workspace_id = data.terraform_remote_state.foundation.outputs.log_analytics_workspace_id

  tags = var.tags
}