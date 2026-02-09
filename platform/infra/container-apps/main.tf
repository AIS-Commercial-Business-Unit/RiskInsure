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
    key                  = "container-apps.tfstate"
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
  }
}

data "terraform_remote_state" "shared_services" {
  backend = "azurerm"
  config = {
    resource_group_name  = "CAIS-010-RiskInsure"
    storage_account_name = "riskinsuretfstate"
    container_name       = "tfstate"
    key                  = "shared-services.tfstate"
  }
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