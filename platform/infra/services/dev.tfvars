environment                    = "dev"
resource_group_name            = "CAIS-010-RiskInsure"
location                       = "eastus2"
image_tag                      = "latest"
acr_name                       = "riskinsuredevacr"
use_managed_identity           = false
use_connection_strings         = true
ingress_external_enabled       = true
ingress_allow_insecure         = true
container_apps_internal_only   = false
enable_keda_scaling            = true

# Service configurations use defaults from variables.tf
# Services deployed: billing, customer, policy, ratingandunderwriting, fundstransfermgt, policyequityandinvoicingmgt, customerrelationshipsmgt

tags = {
  Project     = "RiskInsure"
  ManagedBy   = "Terraform"
  Environment = "dev"
  Layer       = "ContainerApps"
}