variable "environment" {
  description = "Environment name (dev, staging, prod)"
  type        = string
  default     = "dev"

  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment)
    error_message = "Environment must be dev, staging, or prod."
  }
}

variable "resource_group_name" {
  description = "Name of the resource group"
  type        = string
  default     = "CAIS-010-RiskInsure"
}

variable "location" {
  description = "Azure region for resources"
  type        = string
  default     = "eastus2"
}

# ==========================================================================
# Cosmos DB Variables
# ==========================================================================

variable "cosmosdb_account_name" {
  description = "Cosmos DB account name (must be globally unique)"
  type        = string
  default     = "" # Will be generated as riskinsure-{environment}-cosmos
}

variable "cosmosdb_enable_free_tier" {
  description = "Enable Cosmos DB free tier (only one per subscription)"
  type        = bool
  default     = false
}

variable "cosmosdb_throughput" {
  description = "Default throughput (RU/s) for Cosmos DB containers"
  type        = number
  default     = 400

  validation {
    condition     = var.cosmosdb_throughput >= 400
    error_message = "Cosmos DB throughput must be at least 400 RU/s."
  }
}

variable "cosmosdb_enable_automatic_failover" {
  description = "Enable automatic failover for Cosmos DB (production only)"
  type        = bool
  default     = false
}

variable "cosmosdb_geo_locations" {
  description = "List of geo-replication locations for Cosmos DB"
  type = list(object({
    location          = string
    failover_priority = number
  }))
  default = []
}

# ==========================================================================
# Service Bus Variables
# ==========================================================================

variable "servicebus_namespace_name" {
  description = "Service Bus namespace name (must be globally unique)"
  type        = string
  default     = "" # Will be generated as riskinsure-{environment}-bus
}

variable "servicebus_sku" {
  description = "Service Bus SKU (Basic, Standard, Premium)"
  type        = string
  default     = "Standard"

  validation {
    condition     = contains(["Basic", "Standard", "Premium"], var.servicebus_sku)
    error_message = "Service Bus SKU must be Basic, Standard, or Premium."
  }
}

variable "servicebus_capacity" {
  description = "Service Bus capacity (Premium SKU only: 1, 2, 4, 8, 16)"
  type        = number
  default     = null
}

variable "servicebus_premium_messaging_partitions" {
  description = "Number of messaging partitions (Premium SKU only: 1, 2, 4)"
  type        = number
  default     = null
}

# ==========================================================================
# Common Variables
# ==========================================================================

variable "tags" {
  description = "Tags to apply to all resources"
  type        = map(string)
  default = {
    Project     = "RiskInsure"
    ManagedBy   = "Terraform"
    Environment = "dev"
    Layer       = "SharedServices"
  }
}

variable "enable_private_endpoints" {
  description = "Enable private endpoints for Cosmos DB and Service Bus"
  type        = bool
  default     = false
}

output "servicebus_connection_string" {
  description = "Service Bus connection string (sensitive - for dev/test)"
  value       = var.environment == "dev" ? azurerm_servicebus_namespace_authorization_rule.root_manage[0].primary_connection_string : null
  sensitive   = true
}

output "servicebus_primary_key" {
  description = "Service Bus primary key (sensitive)"
  value       = var.environment == "dev" ? azurerm_servicebus_namespace_authorization_rule.root_manage[0].primary_key : null
  sensitive   = true
}
