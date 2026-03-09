environment                    = "dev"
resource_group_name            = "CAIS-010-RiskInsure"
location                       = "eastus2"
image_tag                      = "latest"
acr_name                       = "riskinsuredevacr"
use_managed_identity           = false
use_connection_strings         = true
ingress_external_enabled       = true
ingress_allow_insecure         = true
container_apps_internal_only   = false
enable_keda_scaling            = true

# ==========================================================================
# Modernization Patterns Configuration
# ==========================================================================

modernizationpatterns_chat_api = {
  enabled      = true
  cpu          = 0.5
  memory       = "1Gi"
  min_replicas = 1
  max_replicas = 3
}

modernizationpatterns_reindex_worker = {
  enabled      = true
  cpu          = 1.0
  memory       = "2Gi"
  min_replicas = 0  # Scale to zero when not indexing
  max_replicas = 2
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
  Environment = "dev"
  Layer       = "ContainerApps"
}