environment                          = "prod"
resource_group_name                  = "CAIS-010-RiskInsure-Prod"
location                             = "eastus2"
image_tag                            = "v1.0.0" # Use specific version tags in prod
acr_name                             = "riskinsureprodacr"
use_managed_identity                 = true
use_connection_strings               = false
ingress_external_enabled             = true
ingress_allow_insecure               = false
container_apps_internal_only         = false
container_apps_zone_redundancy_enabled = true
enable_keda_scaling                  = true
keda_service_bus_queue_length        = 5

# Service configurations use defaults from variables.tf
# Services deployed: billing, customer, policy, policylifecyclemgt, ratingandunderwriting, fundstransfermgt, policyequityandinvoicingmgt, customerrelationshipsmgt
# Production scaling: policyequityandinvoicingmgt (API: 1-5, Endpoint: 1-3) matches billing baseline
# PolicyLifeCycleMgt: parallel run with Policy during cutover (API: 1-10, Endpoint: 1-5)

tags = {
  Project     = "RiskInsure"
  ManagedBy   = "Terraform"
  Environment = "prod"
  Layer       = "ContainerApps"
  CostCenter  = "Insurance-Platform"
}