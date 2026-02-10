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

variable "client_id" {
  default = ""
}
variable "subscription_id" {
  default = ""
}
variable "tenant_id" {
  default = ""
}


variable "cosmos_database_name" {
  description = "The name of the Cosmos DB database."
  type        = string
  default     = "billing-db"
}

variable "cosmos_container_name" {
  description = "The name of the Cosmos DB container."
  type        = string
  default     = "billing-container"
}

variable "container_app_image" {
  description = "The image for the container app."
  type        = string
  default     = "billingapi:v1.0.0"
}

variable "cosmos_connection_string" {
  type        = string
  description = "Cosmos DB connection string for dev/test"
}

variable "servicebus_connection_string" {
  type        = string
  description = "Service Bus connection string for dev/test"
}