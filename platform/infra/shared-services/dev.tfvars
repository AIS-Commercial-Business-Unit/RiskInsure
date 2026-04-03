environment               = "dev"
resource_group_name       = "CAIS-010-RiskInsure"
location                  = "eastus2"
cosmosdb_enable_free_tier = true
cosmosdb_throughput       = 400
servicebus_sku            = "Standard"
enable_private_endpoints  = false

tags = {
  Project     = "RiskInsure"
  ManagedBy   = "CAIS Team"
  Environment = "dev"
  Layer       = "SharedServices"
}

# ==========================================================================
# Azure AI Search Configuration (Free tier for dev)
# ==========================================================================
ai_search_sku             = "free"
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
ai_foundry_chat_capacity        = 10 # 10K tokens per minute

# Embedding model - text-embedding-3-small
ai_foundry_embedding_deployment_name = "text-embedding-3-small"
ai_foundry_embedding_model           = "text-embedding-3-small"
ai_foundry_embedding_model_version   = "1"
ai_foundry_embedding_capacity        = 100 # 100K tokens per minute
