environment                          = "prod"
resource_group_name                  = "CAIS-010-RiskInsure-Prod"
location                             = "eastus2"
image_tag                            = "v1.0.0" # Use specific version tags in prod
acr_name                             = "riskinsureprodacr"
use_managed_identity                 = true
use_connection_strings               = false
ingress_external_enabled             = true
ingress_allow_insecure               = false
container_apps_internal_only         = false
container_apps_zone_redundancy_enabled = true
enable_keda_scaling                  = true
keda_service_bus_queue_length        = 5

# Production scaling configuration
services = {
  "billing" = {
    api = {
      enabled        = true
      cpu            = 1.0
      memory         = "2Gi"
      min_replicas   = 2
      max_replicas   = 20
      container_name = "Billing"
    }
    endpoint = {
      enabled        = true
      cpu            = 1.0
      memory         = "2Gi"
      min_replicas   = 2
      max_replicas   = 10
      container_name = "Billing"
    }
  }
  "customer" = {
    api = {
      enabled        = true
      cpu            = 1.0
      memory         = "2Gi"
      min_replicas   = 2
      max_replicas   = 20
      container_name = "customer"
    }
    endpoint = {
      enabled        = true
      cpu            = 1.0
      memory         = "2Gi"
      min_replicas   = 2
      max_replicas   = 10
      container_name = "customer"
    }
  }
  "policy" = {
    api = {
      enabled        = true
      cpu            = 1.0
      memory         = "2Gi"
      min_replicas   = 2
      max_replicas   = 20
      container_name = "policy"
    }
    endpoint = {
      enabled        = true
      cpu            = 1.0
      memory         = "2Gi"
      min_replicas   = 2
      max_replicas   = 10
      container_name = "policy"
    }
  }
  "ratingandunderwriting" = {
    api = {
      enabled        = true
      cpu            = 1.0
      memory         = "2Gi"
      min_replicas   = 2
      max_replicas   = 20
      container_name = "ratingandunderwriting"
    }
    endpoint = {
      enabled        = true
      cpu            = 1.0
      memory         = "2Gi"
      min_replicas   = 2
      max_replicas   = 10
      container_name = "ratingandunderwriting"
    }
  }
  "fundstransfermgt" = {
    api = {
      enabled        = true
      cpu            = 1.0
      memory         = "2Gi"
      min_replicas   = 2
      max_replicas   = 20
      container_name = "fundstransfermgt"
    }
    endpoint = {
      enabled        = true
      cpu            = 1.0
      memory         = "2Gi"
      min_replicas   = 2
      max_replicas   = 10
      container_name = "fundstransfermgt"
    }
  }
}

tags = {
  Project     = "RiskInsure"
  ManagedBy   = "Terraform"
  Environment = "prod"
  Layer       = "ContainerApps"
  CostCenter  = "Insurance-Platform"
}