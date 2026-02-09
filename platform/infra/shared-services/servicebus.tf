# ==========================================================================
# Service Bus Namespace
# ==========================================================================

resource "azurerm_servicebus_namespace" "riskinsure" {
  name                = local.servicebus_namespace_name
  location            = local.location
  resource_group_name = local.resource_group_name
  sku                 = var.servicebus_sku

  # Premium SKU features
  capacity                     = var.servicebus_sku == "Premium" ? var.servicebus_capacity : null
  premium_messaging_partitions = var.servicebus_sku == "Premium" ? var.servicebus_premium_messaging_partitions : null

  tags = local.common_tags
}

# ==========================================================================
# Service Bus Topic (NServiceBus auto-creates subscriptions)
# ==========================================================================

resource "azurerm_servicebus_topic" "bundle" {
  name         = "bundle-1"
  namespace_id = azurerm_servicebus_namespace.riskinsure.id

  # NServiceBus recommended settings
  max_size_in_megabytes = 1024
}

# ==========================================================================
# Authorization Rule (for dev/test - use Managed Identity in prod)
# ==========================================================================

resource "azurerm_servicebus_namespace_authorization_rule" "root_manage" {
  name         = "RootManageSharedAccessKey"
  namespace_id = azurerm_servicebus_namespace.riskinsure.id

  listen = true
  send   = true
  manage = true
}