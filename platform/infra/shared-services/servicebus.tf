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
# Configuration: Add/Remove Queues, Topics, and Subscriptions Here
# ==========================================================================

locals {
  # Default queue configuration (applies to all queues unless overridden)
  default_queue_config = {
    max_size_in_megabytes                   = 1024
    lock_duration                           = "PT5M"
    max_delivery_count                      = 2147483647
    duplicate_detection_history_time_window = "PT10M"
    dead_lettering_on_message_expiration    = false
  }

  # Default topic configuration (applies to all topics unless overridden)
  default_topic_config = {
    max_size_in_megabytes                   = 1024
    duplicate_detection_history_time_window = "PT10M"
  }

  # Default subscription configuration (applies to all subscriptions unless overridden)
  default_subscription_config = {
    max_delivery_count                        = 2147483647
    lock_duration                             = "PT5M"
    dead_lettering_on_message_expiration      = false
    dead_lettering_on_filter_evaluation_error = false
  }

  # ========================================================================
  # QUEUES: Add new queues to this list
  # ========================================================================
  queue_names = [
    # NServiceBus Infrastructure
    "audit",
    "error",

    # Particular ServiceControl (for ServicePulse monitoring)
    "particular.monitoring",
    "particular.servicecontrol",
    "particular.servicecontrol.audit",
    "particular.servicecontrol.audit.errors",
    "particular.servicecontrol.errors",
    "particular.servicecontrol.staging",
    "servicecontrol.throughputdata",

    # RiskInsure Application Endpoints (NServiceBus message processors)
    "RiskInsure.Billing.Endpoint",
    "RiskInsure.Customer.Endpoint",
    "RiskInsure.FundTransferMgt.Endpoint",
    "RiskInsure.Policy.Endpoint",
    "RiskInsure.RatingAndUnderwriting.Endpoint",

    # RiskInsure API Send-Only Endpoints (for publishing events from APIs)
    # Note: NServiceBus normalizes these to lowercase in Azure Service Bus
    "riskinsure.billing.api",
    "riskinsure.customer.api",
    "riskinsure.ratingandunderwriting.api",

    # Add new endpoint queues here:
    # "RiskInsure.NewService.Endpoint",
  ]

  # ========================================================================
  # TOPICS: Add new topics to this list
  # ========================================================================
  topic_names = [
    # Legacy/existing topic
    "bundle-1",

    # RiskInsure Public Contract Events
    "RiskInsure.PublicContracts.Events.FundsRefunded",
    "RiskInsure.PublicContracts.Events.FundsSettled",
    "RiskInsure.PublicContracts.Events.QuoteAccepted",

    # Add new event topics here:
    # "RiskInsure.PublicContracts.Events.PaymentReceived",
    # "RiskInsure.PublicContracts.Events.PolicyIssued",
  ]

  # ========================================================================
  # SUBSCRIPTIONS: Add new subscriptions with forwarding here
  # ========================================================================
  # Format: "unique_key" = { topic_name, subscription_name, forward_to_queue }
  subscriptions = {
    "funds_refunded_to_billing" = {
      topic_name        = "RiskInsure.PublicContracts.Events.FundsRefunded"
      subscription_name = "RiskInsure.Billing.Endpoint"
      forward_to_queue  = "RiskInsure.Billing.Endpoint"
    }

    "funds_settled_to_billing" = {
      topic_name        = "RiskInsure.PublicContracts.Events.FundsSettled"
      subscription_name = "RiskInsure.Billing.Endpoint"
      forward_to_queue  = "RiskInsure.Billing.Endpoint"
    }

    "quote_accepted_to_policy" = {
      topic_name        = "RiskInsure.PublicContracts.Events.QuoteAccepted"
      subscription_name = "RiskInsure.Policy.Endpoint"
      forward_to_queue  = "RiskInsure.Policy.Endpoint"
    }

    # Add new subscriptions here:
    # "payment_received_to_billing" = {
    #   topic_name        = "RiskInsure.PublicContracts.Events.PaymentReceived"
    #   subscription_name = "RiskInsure.Billing.Endpoint"
    #   forward_to_queue  = "RiskInsure.Billing.Endpoint"
    # }
  }

  # Create lookup maps for resource references
  queue_map = { for q in local.queue_names : q => q }
  topic_map = { for t in local.topic_names : t => t }
}

# ==========================================================================
# Service Bus Queues (dynamically created from queue_names list)
# ==========================================================================

resource "azurerm_servicebus_queue" "queues" {
  for_each = local.queue_map

  name         = each.value
  namespace_id = azurerm_servicebus_namespace.riskinsure.id

  # Apply default configuration
  max_size_in_megabytes                   = local.default_queue_config.max_size_in_megabytes
  lock_duration                           = local.default_queue_config.lock_duration
  max_delivery_count                      = local.default_queue_config.max_delivery_count
  duplicate_detection_history_time_window = local.default_queue_config.duplicate_detection_history_time_window
  dead_lettering_on_message_expiration    = local.default_queue_config.dead_lettering_on_message_expiration
}

# ==========================================================================
# Service Bus Topics (dynamically created from topic_names list)
# ==========================================================================

resource "azurerm_servicebus_topic" "topics" {
  for_each = local.topic_map

  name         = each.value
  namespace_id = azurerm_servicebus_namespace.riskinsure.id

  # Apply default configuration
  max_size_in_megabytes                   = local.default_topic_config.max_size_in_megabytes
  duplicate_detection_history_time_window = local.default_topic_config.duplicate_detection_history_time_window
}

# ==========================================================================
# Topic Subscriptions (with forwarding to queues)
# ==========================================================================

resource "azurerm_servicebus_subscription" "subscriptions" {
  for_each = local.subscriptions

  name               = each.value.subscription_name
  topic_id           = azurerm_servicebus_topic.topics[each.value.topic_name].id
  max_delivery_count = local.default_subscription_config.max_delivery_count
  lock_duration      = local.default_subscription_config.lock_duration

  # Forward messages to the specified queue
  forward_to = azurerm_servicebus_queue.queues[each.value.forward_to_queue].name

  dead_lettering_on_message_expiration      = local.default_subscription_config.dead_lettering_on_message_expiration
  dead_lettering_on_filter_evaluation_error = local.default_subscription_config.dead_lettering_on_filter_evaluation_error
}

# ==========================================================================
# Subscription Rules
# ==========================================================================
# Note: Azure automatically creates a $Default rule with "1=1" SQL filter 
# when a subscription is created. We don't need to explicitly create it.
# If you need custom filters in the future, add them here as separate resources.

# ==========================================================================
# Authorization Rule (for dev/test - use Managed Identity in prod)
# ==========================================================================

data "azurerm_servicebus_namespace_authorization_rule" "root_manage" {
  name         = "RootManageSharedAccessKey"
  namespace_id = azurerm_servicebus_namespace.riskinsure.id
}
