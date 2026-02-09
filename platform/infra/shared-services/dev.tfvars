environment                        = "dev"
resource_group_name                = "CAIS-010-RiskInsure"
location                           = "eastus2"
cosmosdb_enable_free_tier          = true
cosmosdb_throughput                = 400
servicebus_sku                     = "Standard"
enable_private_endpoints           = false

tags = {
  Project     = "RiskInsure"
  ManagedBy   = "CAIS Team"
  Environment = "dev"
  Layer       = "SharedServices"
}