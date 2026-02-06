#!/bin/bash
# Start Local Emulators for RiskInsure Development
# This script starts the Azure Service Bus and Cosmos DB emulators

set -e

echo "🚀 Starting RiskInsure Emulators..."
echo ""

# Check if Docker is running
if ! docker info >/dev/null 2>&1; then
    echo "❌ Docker is not running. Please start Docker Desktop or Rancher Desktop."
    exit 1
fi

# Start emulators
echo "📦 Starting Azure Service Bus Emulator (port 5672)..."
echo "📦 Starting Cosmos DB Emulator (ports 8081, 10251-10254)..."
echo ""

docker-compose up -d servicebus-emulator cosmos-emulator

echo ""
echo "⏳ Waiting for emulators to be ready..."
echo "   (This can take 2-3 minutes on first start)"
echo ""

# Wait for Service Bus (faster to start)
SB_READY=false
for i in {1..30}; do
    if docker logs servicebus-emulator 2>&1 | grep -q "listening"; then
        echo "✅ Service Bus Emulator ready!"
        SB_READY=true
        break
    fi
    sleep 2
    echo "   Checking Service Bus... ($i/30)"
done

if [ "$SB_READY" = false ]; then
    echo "⚠️  Service Bus Emulator may not be ready yet"
fi

# Wait for Cosmos DB (slower to start)
COSMOS_READY=false
for i in {1..60}; do
    if curl -k -s -f https://localhost:8081/_explorer/index.html > /dev/null 2>&1; then
        echo "✅ Cosmos DB Emulator ready!"
        COSMOS_READY=true
        break
    fi
    sleep 3
    echo "   Checking Cosmos DB... ($i/60)"
done

if [ "$COSMOS_READY" = false ]; then
    echo "⚠️  Cosmos DB Emulator may not be ready yet"
    echo "   Check status with: docker logs cosmos-emulator"
fi

echo ""
echo "════════════════════════════════════════════════════════════"
echo "🎉 Emulators Started!"
echo "════════════════════════════════════════════════════════════"
echo ""
echo "📋 Next Steps:"
echo "   1. Copy environment template:  cp .env.emulator .env"
echo "   2. Start all services:         docker-compose up -d"
echo "   3. View logs:                  docker-compose logs -f"
echo "   4. Run tests:                  cd test/e2e && npm test"
echo ""
echo "🔗 Emulator URLs:"
echo "   Service Bus:  amqp://localhost:5672"
echo "   Cosmos DB:    https://localhost:8081/_explorer"
echo ""
echo "🛑 To stop emulators:"
echo "   docker-compose stop servicebus-emulator cosmos-emulator"
echo ""
