<#
.SYNOPSIS
    Complete startup script for RiskInsure local development
.DESCRIPTION
    Starts infrastructure, initializes Cosmos DB, then starts domain services
.EXAMPLE
    .\scripts\start-all.ps1
#>

param(
    [switch]$SkipBuild,
    [switch]$InfraOnly
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " RiskInsure Startup Sequence" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Change to repository root
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptPath
Set-Location $repoRoot

# Step 1: Start infrastructure emulators
Write-Host "[1/4] Starting infrastructure emulators..." -ForegroundColor Cyan
docker compose up -d sql-server cosmos-emulator servicebus-emulator

Write-Host "  Waiting for emulators to be healthy..." -ForegroundColor Yellow
Start-Sleep -Seconds 10

# Check health
$sqlHealthy = docker inspect sql-server --format='{{.State.Health.Status}}' 2>$null
$cosmosHealthy = docker inspect cosmos-emulator --format='{{.State.Health.Status}}' 2>$null

if ($sqlHealthy -eq "healthy") {
    Write-Host "  ✓ SQL Server ready" -ForegroundColor Green
} else {
    Write-Host "  ⚠ SQL Server not healthy yet (will retry)" -ForegroundColor Yellow
}

if ($cosmosHealthy -eq "healthy") {
    Write-Host "  ✓ Cosmos DB ready" -ForegroundColor Green
} else {
    Write-Host "  ⚠ Cosmos DB still starting (may take 2-3 minutes)" -ForegroundColor Yellow
}

Write-Host "  ✓ Service Bus Emulator starting" -ForegroundColor Green
Write-Host ""

# Step 2: Initialize Cosmos DB
Write-Host "[2/4] Initializing Cosmos DB..." -ForegroundColor Cyan
Write-Host "  The Cosmos DB Data Explorer will open in your browser" -ForegroundColor Yellow
Write-Host "  Follow the instructions to create the database and containers" -ForegroundColor Yellow
Write-Host ""

.\scripts\init-cosmosdb-manual.ps1

Write-Host ""
Read-Host "Press ENTER after you've created the database and containers in the browser"

Write-Host ""

if ($InfraOnly) {
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "✓ Infrastructure ready" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Emulators running:" -ForegroundColor Yellow
    Write-Host "  - Cosmos DB: https://localhost:8081" -ForegroundColor Gray
    Write-Host "  - Service Bus: localhost:5672" -ForegroundColor Gray
    Write-Host "  - SQL Server: localhost:1433" -ForegroundColor Gray
    Write-Host ""
    Write-Host "To start services:" -ForegroundColor Yellow
    Write-Host "  docker compose up -d" -ForegroundColor White
    Write-Host ""
    exit 0
}

# Step 3: Build images if needed
if (-not $SkipBuild) {
    Write-Host "[3/4] Building Docker images..." -ForegroundColor Cyan
    docker compose build
    Write-Host ""
}

# Step 4: Start domain services
Write-Host "[4/4] Starting domain services..." -ForegroundColor Cyan
docker compose up -d

Write-Host ""
Write-Host "  Waiting for services to start..." -ForegroundColor Yellow
Start-Sleep -Seconds 15

Write-Host ""

# Check status
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Startup Complete" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$running = docker ps --filter "name=riskinsure" --format "{{.Names}}" | Measure-Object | Select-Object -ExpandProperty Count
Write-Host "Running containers: $running/10" -ForegroundColor $(if ($running -ge 8) { "Green" } else { "Yellow" })
Write-Host ""

Write-Host "API Endpoints:" -ForegroundColor Yellow
Write-Host "  Billing:                http://localhost:7071/scalar/v1" -ForegroundColor Gray
Write-Host "  Customer:               http://localhost:7073/scalar/v1" -ForegroundColor Gray
Write-Host "  Funds Transfer Mgt:     http://localhost:7075/scalar/v1" -ForegroundColor Gray
Write-Host "  Policy:                 http://localhost:7077/scalar/v1" -ForegroundColor Gray
Write-Host "  Rating & Underwriting:  http://localhost:7079/scalar/v1" -ForegroundColor Gray
Write-Host ""

Write-Host "Emulators:" -ForegroundColor Yellow
Write-Host "  Cosmos DB Explorer:     https://localhost:8081/_explorer/index.html" -ForegroundColor Gray
Write-Host ""

Write-Host "Useful Commands:" -ForegroundColor Yellow
Write-Host "  Check status:           docker compose ps" -ForegroundColor Gray
Write-Host "  View logs:              docker compose logs -f <service-name>" -ForegroundColor Gray
Write-Host "  Run smoke test:         .\scripts\smoke-test.ps1" -ForegroundColor Gray
Write-Host "  Stop all:               docker compose down" -ForegroundColor Gray
Write-Host ""

# Run smoke test
Write-Host "Running smoke test..." -ForegroundColor Cyan
Start-Sleep -Seconds 5
.\scripts\smoke-test.ps1
