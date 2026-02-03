# Azure Provider
provider "azurerm" {

  # Uncomment if using from local terminal.
  # subscription_id = "6e83f8e1-bcf9-4cf5-8eb2-c9585391d4ee"
  # tenant_id       = "fa3f5c87-6b26-4240-9f12-c4679a19fed9"

  features {}

  # Uncomment if using in pipeline.
  subscription_id = var.subscription_id
  client_id       = var.client_id
  #client_secret   = var.client_secret
  tenant_id = var.tenant_id

}
# Azure Provider with backend
# provider "azurerm" {

#   features {}

# subscription_id = var.subscription_id
# client_id       = var.client_id
# client_secret   = var.client_secret
# tenant_id       = var.tenant_id

# }

# # TF State Backend
# terraform {
#   backend "azurerm" {
#     storage_account_name = "storageaccountdemostf"
#     container_name       = "terraform"
#     key                  = "terraform.tfstate"
#     use_msi              = false
#     subscription_id      = var.subscription_id
#     tenant_id            = var.tenant_id
#   }
# }

## An example resource that does nothing.
resource "null_resource" "example" {
  triggers = {
    value = "A example resource that does nothing!"
  }
}

# Use existing resource group (don't create new one)
data "azurerm_resource_group" "vmrg" {
  name = var.rgname
}

resource "random_id" "vm-sa" {
  keepers = {
    vm_hostname = var.vm_hostname
  }
  byte_length = 3
}

resource "random_id" "vm-ca" {
  keepers = {
    vm_hostname = var.vm_hostname
  }
  byte_length = 3
}

resource "azurerm_storage_account" "storage" {
  name                     = "bootdsk${lower(random_id.vm-sa.hex)}"
  resource_group_name      = azurerm_resource_group.vmrg.name
  location                 = azurerm_resource_group.vmrg.location
  access_tier              = "Cool"
  min_tls_version          = "TLS1_2"
  account_tier             = element(split("_", var.account_tier), 0)
  account_replication_type = element(split("_", var.account_tier), 1)
  #tags = matchkeys(var.tags,"2")
  tags = var.tags
}
resource "azurerm_storage_container" "container1" {
  count      = 2
  depends_on = [azurerm_storage_account.storage]
  name       = "${var.Environment}-${var.Command}-${var.SubCommand}-${random_id.vm-ca.hex}-${count.index}"
  # storage_account_name  = azurerm_storage_account.storage.name
  storage_account_id    = azurerm_storage_account.storage.id
  container_access_type = "private"
}