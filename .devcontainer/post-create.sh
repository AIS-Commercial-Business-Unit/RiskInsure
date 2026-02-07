#!/bin/bash
set -e

echo "🚀 Setting up RiskInsure development environment..."

# Install .NET workloads
if command -v dotnet &> /dev/null; then
  echo "📦 Installing .NET workloads..."
  dotnet workload update

  # Restore .NET dependencies
  echo "📦 Restoring .NET packages..."
  dotnet restore
else
  echo "ℹ️  Skipping .NET setup - dotnet not found in PATH"
fi

# Install Playwright test dependencies
if [ -d "test/e2e" ]; then
  echo "📦 Installing Playwright test dependencies..."
  cd test/e2e
  npm install
  npx playwright install --with-deps chromium
  cd ../..
else
  echo "ℹ️  Skipping Playwright setup - test/e2e directory not found"
fi

# Wait for emulators to be ready
echo "⏳ Waiting for emulators to start..."
timeout 60 bash -c 'until curl -k -s https://cosmos-emulator:8081/_explorer/index.html > /dev/null; do sleep 2; done' || echo "⚠️  Cosmos emulator may not be ready"
timeout 30 bash -c 'until nc -z servicebus-emulator 5672; do sleep 2; done' || echo "⚠️  Service Bus emulator may not be ready"

echo "✅ Development environment ready!"
echo ""
echo "📋 Quick Start:"
echo "  1. Start all services: docker-compose up -d"
echo "  2. View logs: docker-compose logs -f"
echo "  3. Run tests: cd test/e2e && npm test"
echo "  4. Stop services: docker-compose down"
echo ""
echo "🔗 Service URLs:"
echo "  Customer API: http://localhost:7073"
echo "  Rating API: http://localhost:7079"
echo "  Policy API: http://localhost:7077"
echo "  Billing API: http://localhost:7071"
echo "  Cosmos DB: https://localhost:8081/_explorer"
