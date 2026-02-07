#!/bin/bash
# Azure Service Bus Queue Setup for NsbShipping Service
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
echo " Setting up NsbShipping Service Queues"
echo "========================================="
echo ""

# Shared error and audit queues (run once per namespace)
echo "Creating shared infrastructure queues..."
asb-transport queue create error || echo "  ℹ️  Queue 'error' may already exist"
asb-transport queue create audit || echo "  ℹ️  Queue 'audit' may already exist"
asb-transport queue create particular.monitoring || echo "  ℹ️  Queue 'particular.monitoring' may already exist"
echo ""

# NsbShipping service endpoint
echo "Creating NsbShipping endpoint..."
asb-transport endpoint create RiskInsure.NsbShipping.Endpoint
echo ""

# NsbShipping service subscriptions (add one line per PublicContracts event subscribed to)
echo "Creating NsbShipping subscriptions..."
asb-transport endpoint subscribe RiskInsure.NsbShipping.Endpoint RiskInsure.PublicContracts.Events.OrderPlaced
asb-transport endpoint subscribe RiskInsure.NsbShipping.Endpoint RiskInsure.PublicContracts.Events.OrderBilled
echo ""
echo "✅ NsbShipping service queues created successfully!"
