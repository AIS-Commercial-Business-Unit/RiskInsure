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
    use_oidc             = true
  }
}

# ==========================================================================
# Local variables
# ==========================================================================

locals {
  resource_group_name       = var.resource_group_name
  location                  = var.location

  # Generate names if not provided
  cosmosdb_account_name     = var.cosmosdb_account_name != "" ? var.cosmosdb_account_name : "riskinsure-${var.environment}-cosmos"
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

# ==========================================================================
# Shared User-Assigned Managed Identity (for all Container Apps)
# ==========================================================================

resource "azurerm_user_assigned_identity" "apps_shared" {
  name                = "riskinsure-${var.environment}-app-mi"
  location            = data.terraform_remote_state.foundation.outputs.location
  resource_group_name = data.terraform_remote_state.foundation.outputs.resource_group_name

  tags = local.common_tags
}

# Grant AcrPull role for image pulling
resource "azurerm_role_assignment" "apps_shared_acr" {
  scope              = data.terraform_remote_state.foundation.outputs.acr_id
  role_definition_name = "AcrPull"
  principal_id       = azurerm_user_assigned_identity.apps_shared.principal_id
}

# Grant Service Bus Data Owner role for messaging
resource "azurerm_role_assignment" "apps_shared_servicebus" {
  scope                = azurerm_servicebus_namespace.riskinsure.id
  role_definition_name = "Azure Service Bus Data Owner"
  principal_id         = azurerm_user_assigned_identity.apps_shared.principal_id
}

# Grant Cosmos DB Data Contributor role for data access
resource "azurerm_cosmosdb_sql_role_assignment" "apps_shared_cosmos" {
  resource_group_name = data.terraform_remote_state.foundation.outputs.resource_group_name
  account_name        = azurerm_cosmosdb_account.riskinsure.name
  role_definition_id  = "${azurerm_cosmosdb_account.riskinsure.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002"
  principal_id        = azurerm_user_assigned_identity.apps_shared.principal_id
  scope               = azurerm_cosmosdb_account.riskinsure.id
}

resource "azurerm_role_assignment" "uami_kv_secrets_user" {
  scope              = data.terraform_remote_state.foundation.outputs.key_vault_id
  role_definition_name = "Key Vault Secrets User"
  principal_id       = azurerm_user_assigned_identity.apps_shared.principal_id  # UAMI
}
