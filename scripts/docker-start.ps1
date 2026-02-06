<#
.SYNOPSIS
    Start all RiskInsure services with Docker Compose
.DESCRIPTION
    Builds and starts all 10 services (5 domains × 2 services each) with a single command.
    All services will be visible in Docker Desktop with logs consolidated.
.EXAMPLE
    .\scripts\docker-start.ps1
    .\scripts\docker-start.ps1 -Build  # Force rebuild
#>

param(
    [switch]$Build  # Force rebuild images
)

$ErrorActionPreference = "Stop"

# Change to repository root
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptPath
Set-Location $repoRoot

Write-Host "[*] Starting RiskInsure Services with Docker Compose" -ForegroundColor Cyan
Write-Host "=" * 60

# Check if .env file exists
if (-not (Test-Path ".env")) {
    Write-Host "[!] .env file not found!" -ForegroundColor Yellow
    Write-Host "Creating .env from .env.example..." -ForegroundColor Yellow
    
    if (Test-Path ".env.example") {
        Copy-Item ".env.example" ".env"
        Write-Host ""
        Write-Host "[!] IMPORTANT: Edit .env file with your connection strings!" -ForegroundColor Red
        Write-Host "   - COSMOSDB_CONNECTION_STRING" -ForegroundColor Yellow
        Write-Host "   - SERVICEBUS_CONNECTION_STRING" -ForegroundColor Yellow
        Write-Host ""
        Read-Host "Press Enter after updating .env file..."
    } else {
        Write-Host "[X] .env.example not found. Cannot proceed." -ForegroundColor Red
        exit 1
    }
}

try {
    # Check if Docker is running (via WSL for Rancher Desktop)
    wsl docker info | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[X] Docker is not running. Please start Rancher Desktop." -ForegroundColor Red
        exit 1
    }

    Write-Host "[+] Docker is running" -ForegroundColor Green

    # Build and start services
    if ($Build) {
        Write-Host ""
        Write-Host "[BUILD] Building images (this may take several minutes)..." -ForegroundColor Cyan
        wsl docker-compose build --no-cache
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "[X] Build failed. Check the output above for errors." -ForegroundColor Red
            exit 1
        }
    }

    Write-Host ""
    Write-Host "[START] Starting all services..." -ForegroundColor Cyan
    wsl docker-compose up -d

    if ($LASTEXITCODE -ne 0) {
        Write-Host "[X] Failed to start services. Check the output above." -ForegroundColor Red
        exit 1
    }

    Write-Host ""
    Write-Host "[SUCCESS] All services started successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "[STATUS] Service Status:" -ForegroundColor Cyan
    wsl docker-compose ps
    
    Write-Host ""
    Write-Host "[API] API Endpoints:" -ForegroundColor Cyan
    Write-Host "   Billing:               http://localhost:7071" -ForegroundColor White
    Write-Host "   Customer:              http://localhost:7073" -ForegroundColor White
    Write-Host "   Funds Transfer:        http://localhost:7075" -ForegroundColor White
    Write-Host "   Policy:                http://localhost:7077" -ForegroundColor White
    Write-Host "   Rating & Underwriting: http://localhost:7079" -ForegroundColor White
    
    Write-Host ""
    Write-Host "[COMMANDS] Useful Commands:" -ForegroundColor Cyan
    Write-Host "   View logs:      docker-compose logs -f" -ForegroundColor Gray
    Write-Host "   View specific:  docker-compose logs -f billing-api" -ForegroundColor Gray
    Write-Host "   Stop all:       .\scripts\docker-stop.ps1" -ForegroundColor Gray
    Write-Host "   Restart:        docker-compose restart" -ForegroundColor Gray
    Write-Host "   Rebuild:        .\scripts\docker-rebuild.ps1" -ForegroundColor Gray
    Write-Host ""
    Write-Host "[TIP] Open Docker Desktop to see all services and logs in one place!" -ForegroundColor Yellow

} catch {
    Write-Host "[ERROR] Error: $_" -ForegroundColor Red
    exit 1
}
