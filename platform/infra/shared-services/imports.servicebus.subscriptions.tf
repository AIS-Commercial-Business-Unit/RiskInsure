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

# Temporary state reconciliation for pre-existing Key Vault AI secrets.
# Keep this only until import is applied successfully in CI, then remove it.

data "azurerm_key_vault_secret" "existing_azure_openai_endpoint" {
  name         = "AzureOpenAIEndpoint"
  key_vault_id = data.terraform_remote_state.foundation.outputs.key_vault_id
}

data "azurerm_key_vault_secret" "existing_azure_openai_api_key" {
  name         = "AzureOpenAIApiKey"
  key_vault_id = data.terraform_remote_state.foundation.outputs.key_vault_id
}

data "azurerm_key_vault_secret" "existing_azure_search_endpoint" {
  name         = "AzureSearchEndpoint"
  key_vault_id = data.terraform_remote_state.foundation.outputs.key_vault_id
}

data "azurerm_key_vault_secret" "existing_azure_search_api_key" {
  name         = "AzureSearchApiKey"
  key_vault_id = data.terraform_remote_state.foundation.outputs.key_vault_id
}

import {
  to = azurerm_key_vault_secret.azure_openai_endpoint
  id = data.azurerm_key_vault_secret.existing_azure_openai_endpoint.id
}

import {
  to = azurerm_key_vault_secret.azure_openai_api_key
  id = data.azurerm_key_vault_secret.existing_azure_openai_api_key.id
}

import {
  to = azurerm_key_vault_secret.azure_search_endpoint
  id = data.azurerm_key_vault_secret.existing_azure_search_endpoint.id
}

import {
  to = azurerm_key_vault_secret.azure_search_api_key
  id = data.azurerm_key_vault_secret.existing_azure_search_api_key.id
}
