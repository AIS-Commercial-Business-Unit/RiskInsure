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

variable "vnet_address_space" {
  description = "Address space for the virtual network"
  type        = string
  default     = "10.0.0.0/16"
}

variable "container_apps_subnet_cidr" {
  description = "CIDR block for Container Apps subnet"
  type        = string
  default     = "10.0.0.0/23" # /23 provides 512 IPs (10.0.0.0 - 10.0.1.255)
}

variable "private_endpoints_subnet_cidr" {
  description = "CIDR block for private endpoints subnet"
  type        = string
  default     = "10.0.2.0/24" # /24 provides 256 IPs (10.0.2.0 - 10.0.2.255)
}

variable "acr_geo_replications" {
  description = "Geo-replication locations for Container Registry (prod only)"
  type = list(object({
    location                = string
    zone_redundancy_enabled = bool
  }))
  default = [
    {
      location                = "eastus2"
      zone_redundancy_enabled = true
    }
  ]
}

variable "tags" {
  description = "Tags to apply to all resources"
  type        = map(string)
  default = {
    Project     = "RiskInsure"
    ManagedBy   = "Terraform"
    # Environment = var.environment
  }
}