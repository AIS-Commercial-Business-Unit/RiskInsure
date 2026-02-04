variable "rgname" {
  default = "CAIS-010-RiskInsure"
}

variable "location" {
  default = "eastus2"
}

variable "account_tier" {
  default = "Standard_LRS"
}

variable "vm_hostname" {
  default = "azhost"
}

variable "tags" {
  type = map(any)
  default = {
    Name = "P-AFC-FCC-"
  }
}

variable "Environment" {
  default = "d"
}

variable "Command" {
  default = "afc"
}

variable "SubCommand" {
  default = "fcc"
}

## GitActions TF Variables - Matched to GitHub Secrets
## Optional for local development (uses Azure CLI)
## Required for GitHub Actions (set via environment variables)
variable "client_id" {
  default = ""
}
variable "subscription_id" {
  default = ""
}
variable "tenant_id" {
  default = ""
}