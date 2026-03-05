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