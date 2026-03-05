variable "rgname" {
  default = "CAIS-010-RiskInsure"
}

variable "location" {
  default = "eastus2"
}

variable "account_tier" {
  default = "Standard_LRS"
}

variable "vm_hostname" {
  default = "azhost"
}

variable "tags" {
  type = map(any)
  default = {
    Name = "P-AFC-FCC-"
  }
}

variable "Environment" {
  default = "d"
}

variable "Command" {
  default = "afc"
}

variable "SubCommand" {
  default = "fcc"
}

## GitActions TF Variables - Matched to GitHub Secrets
## Optional for local development (uses Azure CLI)
## Required for GitHub Actions (set via environment variables)
variable "client_id" {
  default = ""
}
variable "subscription_id" {
  default = ""
}
variable "tenant_id" {
  default = ""
}

variable "environment" {
  description = "Environment name (dev, staging, prod)"
  type        = string
  default     = "dev"
}

variable "location" {
  description = "Azure region for resources"
  type        = string
  default     = "eastus2"
}

variable "resource_group_name" {
  description = "Resource group name"
  type        = string
  default     = "CAIS-010-RiskInsure"
}

variable "project_name" {
  description = "Project name prefix for resources"
  type        = string
  default     = "riskinsure"
}

variable "cosmos_database_name" {
  description = "Cosmos DB database name"
  type        = string
  default     = "RiskInsure"
}

variable "container_registry_name" {
  description = "Azure Container Registry name"
  type        = string
  default     = "riskinsureacr"
}

variable "services" {
  description = "Map of microservices to deploy"
  type = map(object({
    api_port           = number
    container_name     = string
    cpu                = number
    memory             = string
    min_replicas       = number
    max_replicas       = number
    has_endpoint       = bool
  }))
  default = {
    billing = {
      api_port       = 8080
      container_name = "Billing"
      cpu            = 0.25
      memory         = "0.5Gi"
      min_replicas   = 0
      max_replicas   = 3
      has_endpoint   = true
    }
    customer = {
      api_port       = 8080
      container_name = "customer"
      cpu            = 0.25
      memory         = "0.5Gi"
      min_replicas   = 0
      max_replicas   = 3
      has_endpoint   = true
    }
    fundstransfermgt = {
      api_port       = 8080
      container_name = "fundstransfermgt"
      cpu            = 0.25
      memory         = "0.5Gi"
      min_replicas   = 0
      max_replicas   = 3
      has_endpoint   = true
    }
    policy = {
      api_port       = 8080
      container_name = "policy"
      cpu            = 0.25
      memory         = "0.5Gi"
      min_replicas   = 0
      max_replicas   = 3
      has_endpoint   = true
    }
    ratingandunderwriting = {
      api_port       = 8080
      container_name = "ratingunderwriting"
      cpu            = 0.25
      memory         = "0.5Gi"
      min_replicas   = 0
      max_replicas   = 3
      has_endpoint   = true
    }
  }
}

variable "tags" {
  description = "Tags to apply to all resources"
  type        = map(string)
  default = {
    Project     = "RiskInsure"
    ManagedBy   = "CAIS Team"
    Environment = "dev"
  }
}