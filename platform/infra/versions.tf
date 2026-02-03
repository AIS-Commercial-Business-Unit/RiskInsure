terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = ">= 3.0.0"
    }
    random = {
      source  = "hashicorp/random"
      version = ">= 3.0.0"
    }
    null = {
      source  = "hashicorp/null"
      version = ">= 3.0.0"
    }
  }

  backend "azurerm" {
    resource_group_name  = "CAIS-010-RiskInsure"
    storage_account_name = "riskinsuretfstate"
    container_name       = "tfstate"
    key                  = "riskinsure.tfstate"
    use_oidc            = true
  }
}

## Used for GOV
# provider "azurerm" {

#   features {}
#   environment = "usgovernment"
#   # #use_msi        = true
#   # subscription_id = var.subscription_id
#   # client_id       = var.client_id
#   # client_secret   = var.client_secret
#   # tenant_id       = var.tenant_id

#   # Local TF RUN
#   skip_provider_registration = true
#   subscription_id = "27bada0d-b623-4bd4-8761-b532a4146dcb"
#   client_id       = "d38e0be0-0f65-4a76-b40e-035c67140729"
#   client_secret   = "3_~e9H7mBrGknI4~_ipConc~~P_KvdSRp2"
#   tenant_id       = "066c1e4a-a46d-4d7d-b992-8c4a4a1bcd12"

# }
