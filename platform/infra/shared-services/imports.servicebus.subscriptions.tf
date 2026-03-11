# Temporary state reconciliation for pre-existing Service Bus subscriptions.
# Keep this file only until import is applied successfully in CI, then remove it.

data "azurerm_client_config" "current" {}

import {
  to = azurerm_servicebus_subscription.subscriptions["funds_refunded_to_policyequityandinvoicingmgt"]
  id = "/subscriptions/${data.azurerm_client_config.current.subscription_id}/resourceGroups/CAIS-010-RiskInsure/providers/Microsoft.ServiceBus/namespaces/riskinsure-dev-bus/topics/RiskInsure.PublicContracts.Events.FundsRefunded/subscriptions/RiskInsure.PolicyEquityAndInvoicingMgt.Endpoint"
}

import {
  to = azurerm_servicebus_subscription.subscriptions["funds_settled_to_policyequityandinvoicingmgt"]
  id = "/subscriptions/${data.azurerm_client_config.current.subscription_id}/resourceGroups/CAIS-010-RiskInsure/providers/Microsoft.ServiceBus/namespaces/riskinsure-dev-bus/topics/RiskInsure.PublicContracts.Events.FundsSettled/subscriptions/RiskInsure.PolicyEquityAndInvoicingMgt.Endpoint"
}
