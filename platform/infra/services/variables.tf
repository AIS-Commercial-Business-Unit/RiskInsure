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
# Foundation Layer Inputs (from terraform_remote_state)
# ==========================================================================

variable "container_apps_subnet_id" {
  description = "Subnet ID for Container Apps (from foundation layer)"
  type        = string
  default     = ""
}

variable "log_analytics_workspace_id" {
  description = "Log Analytics workspace ID (from foundation layer)"
  type        = string
  default     = ""
}

variable "acr_login_server" {
  description = "Azure Container Registry login server (from foundation layer)"
  type        = string
  default     = ""
}

variable "key_vault_name" {
  description = "Key Vault name (from foundation layer)"
  type        = string
  default     = ""
}

# ==========================================================================
# Shared Services Layer Inputs (from terraform_remote_state)
# ==========================================================================

variable "cosmosdb_endpoint" {
  description = "Cosmos DB account endpoint (from shared-services layer)"
  type        = string
  default     = ""
}

variable "cosmosdb_database_name" {
  description = "Cosmos DB database name (from shared-services layer)"
  type        = string
  default     = "RiskInsure"
}

variable "cosmosdb_account_name" {
  description = "Cosmos DB account name (from shared-services layer)"
  type        = string
  default     = ""
}

variable "cosmosdb_account_id" {
  description = "Cosmos DB account resource ID (from shared-services layer)"
  type        = string
  default     = ""
}

variable "servicebus_namespace_fqdn" {
  description = "Service Bus fully qualified domain name (from shared-services layer)"
  type        = string
  default     = ""
}

variable "servicebus_namespace_id" {
  description = "Service Bus namespace resource ID (from shared-services layer)"
  type        = string
  default     = ""
}

# ==========================================================================
# Container Apps Environment Variables
# ==========================================================================

variable "container_apps_environment_name" {
  description = "Name of the Container Apps Environment"
  type        = string
  default     = "" # Will be generated as riskinsure-{environment}-env
}

variable "container_apps_internal_only" {
  description = "Make Container Apps Environment internal-only (requires VNet)"
  type        = bool
  default     = false
}

variable "container_apps_zone_redundancy_enabled" {
  description = "Enable zone redundancy for Container Apps Environment (production only)"
  type        = bool
  default     = false
}

# ==========================================================================
# Docker Image Variables
# ==========================================================================

variable "image_tag" {
  description = "Docker image tag to deploy"
  type        = string
  default     = "latest"
}

variable "acr_name" {
  description = "Azure Container Registry name (without .azurecr.io)"
  type        = string
  default     = "riskinsuredevacr"
}

# ==========================================================================
# Microservices Configuration
# ==========================================================================

variable "services" {
  description = "Map of microservices to deploy with their configurations"
  type = map(object({
    api = object({
      enabled        = bool
      cpu            = number
      memory         = string
      min_replicas   = number
      max_replicas   = number
      container_name = string
    })
    endpoint = object({
      enabled        = bool
      cpu            = number
      memory         = string
      min_replicas   = number
      max_replicas   = number
      container_name = string
    })
  }))

  default = {
    "billing" = {
      api = {
        enabled        = true
        cpu            = 0.25
        memory         = "0.5Gi"
        min_replicas   = 1
        max_replicas   = 5
        container_name = "Billing"
      }
      endpoint = {
        enabled        = true
        cpu            = 0.25
        memory         = "0.5Gi"
        min_replicas   = 1
        max_replicas   = 3
        container_name = "Billing"
      }
    }
    "customer" = {
      api = {
        enabled        = true
        cpu            = 0.25
        memory         = "0.5Gi"
        min_replicas   = 1
        max_replicas   = 10
        container_name = "customer"
      }
      endpoint = {
        enabled        = true
        cpu            = 0.25
        memory         = "0.5Gi"
        min_replicas   = 1
        max_replicas   = 5
        container_name = "customer"
      }
    }
    "policy" = {
      api = {
        enabled        = true
        cpu            = 0.25
        memory         = "0.5Gi"
        min_replicas   = 1
        max_replicas   = 10
        container_name = "policy"
      }
      endpoint = {
        enabled        = true
        cpu            = 0.25
        memory         = "0.5Gi"
        min_replicas   = 1
        max_replicas   = 5
        container_name = "policy"
      }
    }
    "ratingandunderwriting" = {
      api = {
        enabled        = true
        cpu            = 0.25
        memory         = "0.5Gi"
        min_replicas   = 1
        max_replicas   = 10
        container_name = "ratingunderwriting"
      }
      endpoint = {
        enabled        = true
        cpu            = 0.25
        memory         = "0.5Gi"
        min_replicas   = 1
        max_replicas   = 5
        container_name = "ratingunderwriting"
      }
    }
    "fundstransfermgt" = {
      api = {
        enabled        = true
        cpu            = 0.25
        memory         = "0.5Gi"
        min_replicas   = 1
        max_replicas   = 10
        container_name = "fundstransfermgt"
      }
      endpoint = {
        enabled        = true
        cpu            = 0.25
        memory         = "0.5Gi"
        min_replicas   = 1
        max_replicas   = 5
        container_name = "fundstransfermgt"
      }
    }
  }
}

# ==========================================================================
# Authentication & Authorization
# ==========================================================================

variable "use_managed_identity" {
  description = "Use Managed Identity for Cosmos DB and Service Bus (recommended for production)"
  type        = bool
  default     = true
}

variable "use_connection_strings" {
  description = "Use connection strings instead of Managed Identity (dev/test only)"
  type        = bool
  default     = false
}

# ==========================================================================
# NServiceBus Configuration
# ==========================================================================

variable "nservicebus_license" {
  description = "NServiceBus license key (store in Key Vault)"
  type        = string
  default     = ""
  sensitive   = true
}

# ==========================================================================
# Scaling Configuration
# ==========================================================================

variable "enable_keda_scaling" {
  description = "Enable KEDA auto-scaling for NServiceBus endpoints"
  type        = bool
  default     = true
}

variable "keda_service_bus_queue_length" {
  description = "Target queue length for KEDA scaling"
  type        = number
  default     = 10
}

# ==========================================================================
# Monitoring & Observability
# ==========================================================================

variable "enable_application_insights" {
  description = "Enable Application Insights for distributed tracing"
  type        = bool
  default     = true
}

variable "application_insights_connection_string" {
  description = "Application Insights connection string (from foundation layer)"
  type        = string
  default     = ""
  sensitive   = true
}

# ==========================================================================
# Ingress Configuration
# ==========================================================================

variable "ingress_external_enabled" {
  description = "Enable external ingress for APIs (set false for internal-only)"
  type        = bool
  default     = true
}

variable "ingress_target_port" {
  description = "Target port for Container Apps ingress"
  type        = number
  default     = 8080
}

variable "ingress_allow_insecure" {
  description = "Allow insecure HTTP traffic (set false for production)"
  type        = bool
  default     = false
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
    Layer       = "ContainerApps"
  }
}
