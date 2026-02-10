#Requires -Version 7
<#
.SYNOPSIS
    Rebuild and restart all RiskInsure services
.DESCRIPTION
    Stops all services, rebuilds Docker images, and starts everything fresh.
    Use this after making code changes.
.EXAMPLE
    .\scripts\docker-rebuild.ps1
    .\scripts\docker-rebuild.ps1 -CleanBuild  # Full clean rebuild
#>

param(
    [switch]$CleanBuild  # Clean build (no cache)
)

$ErrorActionPreference = "Stop"

# Change to repository root
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptPath
Set-Location $repoRoot

Write-Host "🔄 Rebuilding RiskInsure Services" -ForegroundColor Cyan
Write-Host ("=" * 60)

try {
    # Stop existing containers
    Write-Host "🛑 Stopping existing containers..." -ForegroundColor Yellow
    docker-compose down
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "⚠️  Warning: Could not stop existing containers" -ForegroundColor Yellow
    }

    # Build images
    Write-Host ""
    Write-Host "🔨 Building images..." -ForegroundColor Cyan
    
    if ($CleanBuild) {
        Write-Host "   (Clean build - this will take longer)" -ForegroundColor Gray
        docker-compose build --no-cache
    } else {
        docker-compose build
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Build failed. Check the output above for errors." -ForegroundColor Red
        exit 1
    }

    # Start services
    Write-Host ""
    Write-Host "🚢 Starting all services..." -ForegroundColor Cyan
    docker-compose up -d

    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Failed to start services." -ForegroundColor Red
        exit 1
    }

    Write-Host ""
    Write-Host "✅ Rebuild complete! All services restarted." -ForegroundColor Green
    Write-Host ""
    Write-Host "📊 Service Status:" -ForegroundColor Cyan
    docker-compose ps
    
    Write-Host ""
    Write-Host "💡 View logs: docker-compose logs -f" -ForegroundColor Yellow

} catch {
    Write-Host "❌ Error: $_" -ForegroundColor Red
    exit 1
}
