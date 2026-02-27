# Start Local Emulators for RiskInsure Development
# This script starts RabbitMQ and Cosmos DB for local development

Write-Host "🚀 Starting RiskInsure Emulators..." -ForegroundColor Cyan
Write-Host ""

# Check if Docker is running
try {
    docker info | Out-Null
} catch {
    Write-Host "❌ Docker is not running. Please start Docker Desktop or Rancher Desktop." -ForegroundColor Red
    exit 1
}

# Start emulators
Write-Host "📦 Starting RabbitMQ (port 5672)..." -ForegroundColor Yellow
Write-Host "📦 Starting Cosmos DB Emulator (ports 8081, 10251-10254)..." -ForegroundColor Yellow
Write-Host ""

docker-compose up -d rabbitmq cosmos-emulator

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to start emulators" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "⏳ Waiting for emulators to be ready..." -ForegroundColor Yellow
Write-Host "   (This can take 2-3 minutes on first start)" -ForegroundColor Gray
Write-Host ""

# Wait for RabbitMQ (faster to start)
$rabbitMqReady = $false
for ($i = 1; $i -le 30; $i++) {
    try {
        $result = docker ps --filter "name=rabbitmq" --format "{{.Status}}" | Select-String "healthy|Up"
        if ($result) {
            Write-Host "✅ RabbitMQ ready!" -ForegroundColor Green
            $rabbitMqReady = $true
            break
        }
    } catch {
        # Ignore errors
    }
    Start-Sleep -Seconds 2
    Write-Host "   Checking RabbitMQ... ($i/30)" -ForegroundColor Gray
}

if (-not $rabbitMqReady) {
    Write-Host "⚠️  RabbitMQ may not be ready yet" -ForegroundColor Yellow
}

# Wait for Cosmos DB (slower to start)
$cosmosReady = $false
for ($i = 1; $i -le 60; $i++) {
    try {
        $response = Invoke-WebRequest -Uri "https://localhost:8081/_explorer/index.html" `
            -SkipCertificateCheck `
            -TimeoutSec 2 `
            -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200) {
            Write-Host "✅ Cosmos DB Emulator ready!" -ForegroundColor Green
            $cosmosReady = $true
            break
        }
    } catch {
        # Ignore errors
    }
    Start-Sleep -Seconds 3
    Write-Host "   Checking Cosmos DB... ($i/60)" -ForegroundColor Gray
}

if (-not $cosmosReady) {
    Write-Host "⚠️  Cosmos DB Emulator may not be ready yet" -ForegroundColor Yellow
    Write-Host "   Check status with: docker logs cosmos-emulator" -ForegroundColor Gray
}

Write-Host ""
Write-Host "════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "🎉 Emulators Started!" -ForegroundColor Green
Write-Host "════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "📋 Next Steps:" -ForegroundColor Yellow
Write-Host "   1. Copy environment template:  cp .env.example .env" -ForegroundColor White
Write-Host "   2. Start all services:         docker-compose up -d" -ForegroundColor White
Write-Host "   3. View logs:                  docker-compose logs -f" -ForegroundColor White
Write-Host "   4. Run tests:                  cd test/e2e && npm test" -ForegroundColor White
Write-Host ""
Write-Host "🔗 Emulator URLs:" -ForegroundColor Yellow
Write-Host "   RabbitMQ:    amqp://localhost:5672" -ForegroundColor White
Write-Host "   Cosmos DB:    https://localhost:8081/_explorer" -ForegroundColor White
Write-Host ""
Write-Host "🛑 To stop emulators:" -ForegroundColor Yellow
Write-Host "   docker-compose stop rabbitmq cosmos-emulator" -ForegroundColor White
Write-Host ""
