# ============================================================================
# Foundation Infrastructure for RiskInsure
# This should be deployed FIRST and rarely changes
# ============================================================================

terraform {
  required_version = ">= 1.11.0"
  
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = ">= 3.0.0"
    }
    random = {
      source  = "hashicorp/random"
      version = ">= 3.0.0"
    }
  }

  backend "azurerm" {
    resource_group_name  = "CAIS-010-RiskInsure"
    storage_account_name = "riskinsuretfstate"
    container_name       = "tfstate"
    key                  = "foundation.tfstate"
    use_oidc             = true
  }
}

provider "azurerm" {
  features {
    resource_group {
      prevent_deletion_if_contains_resources = true
    }
    key_vault {
      purge_soft_delete_on_destroy = false
    }
  }
  
  # Use Azure AD authentication for storage account operations
  # This allows shared_access_key_enabled = false while still managing storage
  storage_use_azuread = true
}

# ============================================================================
# Resource Group (using existing CAIS-010-RiskInsure)
# ============================================================================

# Use data source instead of resource to avoid "already managed" error
# The resource group is pre-existing and should not be managed by Terraform
data "azurerm_resource_group" "riskinsure" {
  name = var.resource_group_name
}

# ============================================================================
# Virtual Network (for private endpoints in production)
# ============================================================================

resource "azurerm_virtual_network" "riskinsure" {
  name                = "riskinsure-${var.environment}-vnet"
  location            = data.azurerm_resource_group.riskinsure.location
  resource_group_name = data.azurerm_resource_group.riskinsure.name
  address_space       = [var.vnet_address_space]
  
  tags = var.tags
}

# Subnet for Container Apps Environment
resource "azurerm_subnet" "container_apps" {
  name                 = "services-subnet"
  resource_group_name  = data.azurerm_resource_group.riskinsure.name
  virtual_network_name = azurerm_virtual_network.riskinsure.name
  address_prefixes     = [var.container_apps_subnet_cidr]

  # Required for Container Apps
  delegation {
    name = "Microsoft.App/environments"
    service_delegation {
      name    = "Microsoft.App/environments"
      actions = ["Microsoft.Network/virtualNetworks/subnets/join/action"]
    }
  }
}

# Subnet for Private Endpoints (Cosmos, Service Bus)
resource "azurerm_subnet" "private_endpoints" {
  name                 = "private-endpoints-subnet"
  resource_group_name  = data.azurerm_resource_group.riskinsure.name
  virtual_network_name = azurerm_virtual_network.riskinsure.name
  address_prefixes     = [var.private_endpoints_subnet_cidr]
}

# ============================================================================
# Log Analytics Workspace (for Container Apps + Application Insights)
# ============================================================================

resource "azurerm_log_analytics_workspace" "riskinsure" {
  name                = "riskinsure-${var.environment}-logs"
  location            = data.azurerm_resource_group.riskinsure.location
  resource_group_name = data.azurerm_resource_group.riskinsure.name
  sku                 = "PerGB2018"
  retention_in_days   = var.environment == "prod" ? 90 : 30

  tags = var.tags
}

# ============================================================================
# Application Insights (for all microservices)
# ============================================================================

resource "azurerm_application_insights" "riskinsure" {
  name                = "riskinsure-${var.environment}-appinsights"
  location            = data.azurerm_resource_group.riskinsure.location
  resource_group_name = data.azurerm_resource_group.riskinsure.name
  workspace_id        = azurerm_log_analytics_workspace.riskinsure.id
  application_type    = "web"

  tags = var.tags
}

# ============================================================================
# Container Registry (for Docker images)
# ============================================================================

resource "azurerm_container_registry" "riskinsure" {
  name                = "riskinsure${var.environment}acr"
  resource_group_name = data.azurerm_resource_group.riskinsure.name
  location            = data.azurerm_resource_group.riskinsure.location
  sku                 = var.environment == "prod" ? "Premium" : "Basic"
  admin_enabled       = false # Use Managed Identity instead

  # Enable geo-replication for production
  dynamic "georeplications" {
    for_each = var.environment == "prod" ? var.acr_geo_replications : []
    content {
      location                = georeplications.value.location
      zone_redundancy_enabled = georeplications.value.zone_redundancy_enabled
    }
  }

  tags = var.tags
}

# ============================================================================
# Key Vault (for secrets, certificates, NServiceBus license)
# ============================================================================

data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "riskinsure" {
  name                       = "riskinsure-${var.environment}-kv"
  location                   = data.azurerm_resource_group.riskinsure.location
  resource_group_name        = data.azurerm_resource_group.riskinsure.name
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  soft_delete_retention_days = 90
  purge_protection_enabled   = var.environment == "prod" ? true : false

  # Enable RBAC authorization (recommended over access policies)
  rbac_authorization_enabled = true

  # Network ACLs for production
  dynamic "network_acls" {
    for_each = var.environment == "prod" ? [1] : []
    content {
      default_action             = "Deny"
      bypass                     = "AzureServices"
      virtual_network_subnet_ids = [azurerm_subnet.container_apps.id]
    }
  }

  tags = var.tags
}

# Grant current user Key Vault Secrets Officer role (for initial setup)
# resource "azurerm_role_assignment" "kv_secrets_officer" {
#   scope                = azurerm_key_vault.riskinsure.id
#   role_definition_name = "Key Vault Secrets Officer"
#   principal_id         = data.azurerm_client_config.current.object_id
# }

# ============================================================================
# Network Security Group (for production security)
# ============================================================================

resource "azurerm_network_security_group" "container_apps" {
  name                = "riskinsure-${var.environment}-nsg"
  location            = data.azurerm_resource_group.riskinsure.location
  resource_group_name = data.azurerm_resource_group.riskinsure.name

  # Allow HTTPS inbound
  security_rule {
    name                       = "AllowHTTPS"
    priority                   = 100
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "443"
    source_address_prefix      = "*"
    destination_address_prefix = "*"
  }

  # Allow HTTP inbound (Container Apps internal routing)
  security_rule {
    name                       = "AllowHTTP"
    priority                   = 110
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "80"
    source_address_prefix      = "VirtualNetwork"
    destination_address_prefix = "*"
  }

  tags = var.tags
}

# Associate NSG with Container Apps subnet
resource "azurerm_subnet_network_security_group_association" "container_apps" {
  subnet_id                 = azurerm_subnet.container_apps.id
  network_security_group_id = azurerm_network_security_group.container_apps.id
}

# ============================================================================
# Storage Account (for Terraform state, backups, etc.)
# ============================================================================

resource "azurerm_storage_account" "riskinsure" {
  name                     = "riskinsure${var.environment}storage"
  resource_group_name      = data.azurerm_resource_group.riskinsure.name
  location                 = data.azurerm_resource_group.riskinsure.location
  account_tier             = "Standard"
  account_replication_type = var.environment == "prod" ? "GRS" : "LRS"
  min_tls_version          = "TLS1_2"

  # Security features
  https_traffic_only_enabled      = true
  # Enable for initial deployment; disable after deployment completes
  shared_access_key_enabled       = true

  tags = var.tags
}

# Container for backups
resource "azurerm_storage_container" "backups" {
  name                  = "backups"
  storage_account_name  = azurerm_storage_account.riskinsure.name
  container_access_type = "private"
}