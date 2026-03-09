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

# ==========================================================================
# Modernization Patterns Configuration (Production)
# ==========================================================================

modernizationpatterns_chat_api = {
  enabled      = true
  cpu          = 1.0
  memory       = "2Gi"
  min_replicas = 2  # Always-on for production
  max_replicas = 10
}

modernizationpatterns_reindex_worker = {
  enabled      = true
  cpu          = 2.0
  memory       = "4Gi"
  min_replicas = 0  # Scale to zero when not indexing
  max_replicas = 3
}

modernizationpatterns_search_index_name    = "modernization-patterns"
modernizationpatterns_chat_deployment      = "gpt-4o"
modernizationpatterns_embedding_deployment = "text-embedding-3-large"

# ==========================================================================
# Tags
# ==========================================================================

tags = {
  Project     = "RiskInsure"
  ManagedBy   = "Terraform"
  Environment = "prod"
  Layer       = "ContainerApps"
  CostCenter  = "Insurance-Platform"
}