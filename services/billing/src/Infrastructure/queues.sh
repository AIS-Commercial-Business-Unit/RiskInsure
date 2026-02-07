#!/bin/bash
# Azure Service Bus Queue Setup for Billing Service
# Run this script after creating a Service Bus namespace to set up queues and subscriptions

set -e  # Exit on error

# Check if connection string is set
if [ -z "$AzureServiceBus_ConnectionString" ]; then
    echo "❌ Error: AzureServiceBus_ConnectionString environment variable is not set"
    echo ""
    echo "Set it with:"
    echo "  export AzureServiceBus_ConnectionString='Endpoint=sb://YOUR-NAMESPACE.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=YOUR-KEY'"
    echo ""
    exit 1
fi

echo "========================================="
echo " Setting up Billing Service Queues"
echo "========================================="
echo ""

# Shared error and audit queues (run once per namespace)
echo "Creating shared infrastructure queues..."
asb-transport queue create error || echo "  ℹ️  Queue 'error' may already exist"
asb-transport queue create audit || echo "  ℹ️  Queue 'audit' may already exist"
asb-transport queue create particular.monitoring || echo "  ℹ️  Queue 'particular.monitoring' may already exist"
echo ""

# Billing service endpoint
echo "Creating Billing endpoint..."
asb-transport endpoint create RiskInsure.Billing.Endpoint
echo ""

# Billing service subscriptions
echo "Creating Billing subscriptions..."
asb-transport endpoint subscribe RiskInsure.Billing.Endpoint RiskInsure.PublicContracts.Events.PolicyBound

echo ""
echo "✅ Billing service queues created successfully!"
echo ""
echo "Note: Internal domain events (BillingAccountCreated, AccountActivated, etc.)"
echo "      are handled within the Billing service and don't require subscriptions."
