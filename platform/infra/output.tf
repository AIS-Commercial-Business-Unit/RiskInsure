output "azurerg" {
  value = "nameoftheresourcegroup-${data.azurerm_resource_group.vmrg.name}-at${formatdate("DD MMM YYYY hh:mm ZZZ", timestamp())}"
}
#
output "azurerg1" {
  value = "id-${data.azurerm_resource_group.vmrg.id}"
}

# output "storage" {
#   value = "id-${azurerm_storage_account.storage.id}"
# }

# output "container1" {
#   value = azurerm_storage_container.container1[*].name
# }


output "container_app_url" {
  value = azurerm_container_app.app.latest_revision_fqdn
}

