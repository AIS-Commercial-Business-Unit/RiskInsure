environment                        = "prod"
resource_group_name                = "CAIS-010-RiskInsure-Prod"
location                           = "eastus2"
cosmosdb_enable_free_tier          = false
cosmosdb_throughput                = 1000
cosmosdb_enable_automatic_failover = true
servicebus_sku                     = "Premium"
servicebus_capacity                = 1
enable_private_endpoints           = true

cosmosdb_geo_locations = [
  {
    location          = "eastus2"
    failover_priority = 0
  },
  {
    location          = "westus2"
    failover_priority = 1
  }
]

tags = {
  Project     = "RiskInsure"
  ManagedBy   = "Terraform"
  Environment = "prod"
  Layer       = "SharedServices"
  CostCenter  = "Insurance-Platform"
}

# ==========================================================================
# Azure AI Search Configuration (Basic tier for prod)
# ==========================================================================
ai_search_sku             = "basic"
ai_search_replica_count   = 1
ai_search_partition_count = 1

# ==========================================================================
# Azure OpenAI (AI Foundry) Configuration
# ==========================================================================
ai_foundry_sku      = "S0"
ai_foundry_location = "eastus2"

# Chat model - gpt-4.1 for production quality
ai_foundry_chat_deployment_name = "gpt-4.1"
ai_foundry_chat_model           = "gpt-4.1"
ai_foundry_chat_model_version   = "2025-04-14"
ai_foundry_chat_capacity        = 30 # Higher capacity for production

# Embedding model - text-embedding-3-small
ai_foundry_embedding_deployment_name = "text-embedding-3-small"
ai_foundry_embedding_model           = "text-embedding-3-small"
ai_foundry_embedding_model_version   = "1"
ai_foundry_embedding_capacity        = 120 # Higher capacity for production
