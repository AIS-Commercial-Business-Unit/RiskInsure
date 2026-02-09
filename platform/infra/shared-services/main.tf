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
    key                  = "shared-services.tfstate"
    use_oidc             = true
  }
}

provider "azurerm" {
  features {
    resource_group {
      prevent_deletion_if_contains_resources = true
    }
  }
}

# ==========================================================================
# Data source: Import foundation layer outputs
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

# ==========================================================================
# Local variables
# ==========================================================================

locals {
  resource_group_name = data.terraform_remote_state.foundation.outputs.resource_group_name
  location            = data.terraform_remote_state.foundation.outputs.location
  
  # Generate names if not provided
  cosmosdb_account_name   = var.cosmosdb_account_name != "" ? var.cosmosdb_account_name : "riskinsure-${var.environment}-cosmos"
  servicebus_namespace_name = var.servicebus_namespace_name != "" ? var.servicebus_namespace_name : "riskinsure-${var.environment}-bus"
  
  # Merge default tags with provided tags
  common_tags = merge(
    var.tags,
    {
      Environment = var.environment
      ManagedBy   = "Terraform"
      Layer       = "SharedServices"
    }
  )
}