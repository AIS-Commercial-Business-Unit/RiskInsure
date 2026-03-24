#!/bin/bash
set -e

echo "🚀 Setting up RiskInsure development environment..."

echo "✅ Lightweight startup complete!"
echo ""
echo "⚡ To reduce memory usage, heavy operations are manual:"
echo ""
echo "📦 Install dependencies:"
echo "  dotnet restore"
echo ""
echo "📦 Install Playwright (optional):"
echo "  cd test/e2e && npm install && npx playwright install --with-deps chromium"
echo ""
echo "🔧 Start infrastructure emulators (optional):"
echo "  docker-compose --profile infra up -d"
echo ""
echo "📋 Quick Start:"
echo "  1. Run dotnet restore"
echo "  2. Start infrastructure: docker-compose --profile infra up -d"
echo "  3. Start services: docker-compose up -d"
echo "  4. View logs: docker-compose logs -f"
echo ""
echo "🔗 Service URLs (after starting):"
echo "  Customer API: http://localhost:7073"
echo "  Rating API: http://localhost:7079"
echo "  Policy API: http://localhost:7077"
echo "  PolicyEquityAndInvoicingMgt API: http://localhost:7081"
echo "  Cosmos DB: https://localhost:8081/_explorer"

# Always exit successfully
exit 0
