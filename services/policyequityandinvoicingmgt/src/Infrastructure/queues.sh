#!/bin/bash
# RabbitMQ Topology Guidance for Billing Service

set -e

echo "========================================="
echo " Billing Messaging Setup (RabbitMQ)"
echo "========================================="
echo ""
echo "RabbitMQ transport is enabled for Billing."
echo "No manual queue bootstrap is required when NServiceBus installers are enabled."
echo ""
echo "If you need infrastructure manually:"
echo "  1. Ensure RabbitMQ is running: docker compose up -d rabbitmq"
echo "  2. Start Billing Endpoint.In with installers enabled"
echo "  3. Verify queues at http://localhost:15672"
