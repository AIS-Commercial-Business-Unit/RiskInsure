# ==========================================================================
# Cosmos DB Account
# ==========================================================================

resource "azurerm_cosmosdb_account" "riskinsure" {
  name                = local.cosmosdb_account_name
  location            = local.location
  resource_group_name = local.resource_group_name
  offer_type          = "Standard"
  kind                = "GlobalDocumentDB"

  consistency_policy {
    consistency_level       = "Session"
    max_interval_in_seconds = 5
    max_staleness_prefix    = 100
  }

  # Primary location
  geo_location {
    location          = local.location
    failover_priority = 0
  }

  # Additional geo-replications (if configured)
  dynamic "geo_location" {
    for_each = var.cosmosdb_geo_locations
    content {
      location          = geo_location.value.location
      failover_priority = geo_location.value.failover_priority
    }
  }

  # Backup
  dynamic "backup" {
    for_each = var.environment == "prod" ? [1] : []
    content {
      type = "Continuous"
      # Retention configured via Azure policy: 30 days minimum
    }
  }

  dynamic "backup" {
    for_each = var.environment == "prod" ? [] : [1]
    content {
      type                = "Periodic"
      interval_in_minutes = 240
      retention_in_hours  = 8
    }
  }

  tags = local.common_tags
}

# ==========================================================================
# Cosmos DB Database
# ==========================================================================

resource "azurerm_cosmosdb_sql_database" "riskinsure" {
  name                = "RiskInsure"
  resource_group_name = local.resource_group_name
  account_name        = azurerm_cosmosdb_account.riskinsure.name
}

# ==========================================================================
# Cosmos DB Containers (Data Containers)
# ==========================================================================

resource "azurerm_cosmosdb_sql_container" "billing" {
  name                  = "billing"
  resource_group_name   = local.resource_group_name
  account_name          = azurerm_cosmosdb_account.riskinsure.name
  database_name         = azurerm_cosmosdb_sql_database.riskinsure.name
  partition_key_paths   = ["/accountId"]    # ← NOT orderId! Use accountId
  partition_key_version = 1
  throughput            = var.cosmosdb_throughput

  indexing_policy {
    indexing_mode = "consistent"

    included_path {
      path = "/*"
    }
  }
}

resource "azurerm_cosmosdb_sql_container" "customer" {
  name                  = "customer"
  resource_group_name   = local.resource_group_name
  account_name          = azurerm_cosmosdb_account.riskinsure.name
  database_name         = azurerm_cosmosdb_sql_database.riskinsure.name
  partition_key_paths   = ["/customerId"]   # ← Correct ✅
  partition_key_version = 1
  throughput            = var.cosmosdb_throughput

  indexing_policy {
    indexing_mode = "consistent"

    included_path {
      path = "/*"
    }
  }
}

resource "azurerm_cosmosdb_sql_container" "policy" {
  name                  = "policy"
  resource_group_name   = local.resource_group_name
  account_name          = azurerm_cosmosdb_account.riskinsure.name
  database_name         = azurerm_cosmosdb_sql_database.riskinsure.name
  partition_key_paths   = ["/policyId"]     # ← Correct ✅
  partition_key_version = 1
  throughput            = var.cosmosdb_throughput

  indexing_policy {
    indexing_mode = "consistent"

    included_path {
      path = "/*"
    }
  }
}

resource "azurerm_cosmosdb_sql_container" "ratingunderwriting" {
  name                  = "ratingunderwriting"
  resource_group_name   = local.resource_group_name
  account_name          = azurerm_cosmosdb_account.riskinsure.name
  database_name         = azurerm_cosmosdb_sql_database.riskinsure.name
  partition_key_paths   = ["/quoteId"]      # ← NOT id! Use quoteId
  partition_key_version = 1
  throughput            = var.cosmosdb_throughput

  indexing_policy {
    indexing_mode = "consistent"

    included_path {
      path = "/*"
    }
  }
}

resource "azurerm_cosmosdb_sql_container" "fundstransfermgt" {
  name                  = "fundstransfermgt"
  resource_group_name   = local.resource_group_name
  account_name          = azurerm_cosmosdb_account.riskinsure.name
  database_name         = azurerm_cosmosdb_sql_database.riskinsure.name
  partition_key_paths   = ["/transactionId"]
  partition_key_version = 1
  throughput            = var.cosmosdb_throughput

  indexing_policy {
    indexing_mode = "consistent"

    included_path {
      path = "/*"
    }
  }
}

# ==========================================================================
# Cosmos DB Saga Containers (for NServiceBus)
# ==========================================================================

resource "azurerm_cosmosdb_sql_container" "billing_sagas" {
  name                  = "billing-sagas"
  resource_group_name   = local.resource_group_name
  account_name          = azurerm_cosmosdb_account.riskinsure.name
  database_name         = azurerm_cosmosdb_sql_database.riskinsure.name
  partition_key_paths   = ["/accountId"]
  partition_key_version = 1
  throughput            = var.cosmosdb_throughput

  indexing_policy {
    indexing_mode = "consistent"

    included_path {
      path = "/*"
    }
  }
}

resource "azurerm_cosmosdb_sql_container" "customer_sagas" {
  name                  = "customer-sagas"
  resource_group_name   = local.resource_group_name
  account_name          = azurerm_cosmosdb_account.riskinsure.name
  database_name         = azurerm_cosmosdb_sql_database.riskinsure.name
  partition_key_paths   = ["/customerId"]
  partition_key_version = 1
  throughput            = var.cosmosdb_throughput

  indexing_policy {
    indexing_mode = "consistent"

    included_path {
      path = "/*"
    }
  }
}

resource "azurerm_cosmosdb_sql_container" "policy_sagas" {
  name                  = "policy-sagas"
  resource_group_name   = local.resource_group_name
  account_name          = azurerm_cosmosdb_account.riskinsure.name
  database_name         = azurerm_cosmosdb_sql_database.riskinsure.name
  partition_key_paths   = ["/policyId"]
  partition_key_version = 1
  throughput            = var.cosmosdb_throughput

  indexing_policy {
    indexing_mode = "consistent"

    included_path {
      path = "/*"
    }
  }
}

resource "azurerm_cosmosdb_sql_container" "fundstransfermgt_sagas" {
  name                  = "fundstransfermgt-sagas"
  resource_group_name   = local.resource_group_name
  account_name          = azurerm_cosmosdb_account.riskinsure.name
  database_name         = azurerm_cosmosdb_sql_database.riskinsure.name
  partition_key_paths   = ["/transactionId"]
  partition_key_version = 1
  throughput            = var.cosmosdb_throughput

  indexing_policy {
    indexing_mode = "consistent"

    included_path {
      path = "/*"
    }
  }
}

resource "azurerm_cosmosdb_sql_container" "ratingunderwriting_sagas" {
  name                  = "ratingunderwriting-sagas"
  resource_group_name   = local.resource_group_name
  account_name          = azurerm_cosmosdb_account.riskinsure.name
  database_name         = azurerm_cosmosdb_sql_database.riskinsure.name
  partition_key_paths   = ["/quoteId"] # ← NOT id! Use quoteId
  partition_key_version = 1
  throughput            = var.cosmosdb_throughput

  indexing_policy {
    indexing_mode = "consistent"

    included_path {
      path = "/*"
    }
  }
}
