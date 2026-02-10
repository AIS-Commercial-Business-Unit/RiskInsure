terraform {
  backend "azurerm" {
    resource_group_name = "CAIS-010-RiskInsure"
    storage_account_name = "riskinsuretfstate"
    container_name = "tfstate"
    key = "riskinsure.tfstate"
  }
}