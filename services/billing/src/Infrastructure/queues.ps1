# Azure Service Bus Queue Setup for Billing Service
# Run this script after creating a Service Bus namespace to set up queues and subscriptions

# Set your connection string here or use environment variable
# $env:AzureServiceBus_ConnectionString = 'Endpoint=sb://YOUR-NAMESPACE.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=YOUR-KEY'

# Shared error and audit queues (run once per namespace)
asb-transport queue create error
asb-transport queue create audit
asb-transport queue create particular.monitoring

# Billing service queues
asb-transport endpoint create RiskInsure.Billing.Endpoint
asb-transport endpoint subscribe RiskInsure.Billing.Endpoint RiskInsure.Billing.Domain.Contracts.Events.BillingAccountCreated
asb-transport endpoint subscribe RiskInsure.Billing.Endpoint RiskInsure.Billing.Domain.Contracts.Events.AccountActivated
asb-transport endpoint subscribe RiskInsure.Billing.Endpoint RiskInsure.Billing.Domain.Contracts.Events.PremiumOwedUpdated
asb-transport endpoint subscribe RiskInsure.Billing.Endpoint RiskInsure.Billing.Domain.Contracts.Events.AccountSuspended
asb-transport endpoint subscribe RiskInsure.Billing.Endpoint RiskInsure.Billing.Domain.Contracts.Events.AccountClosed
asb-transport endpoint subscribe RiskInsure.Billing.Endpoint RiskInsure.Billing.Domain.Contracts.Events.BillingCycleUpdated
