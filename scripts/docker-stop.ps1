<#
.SYNOPSIS
    Stop all RiskInsure services running in Docker Compose
.DESCRIPTION
    Gracefully stops all 10 services. Containers remain for quick restart.
.EXAMPLE
    .\scripts\docker-stop.ps1
    .\scripts\docker-stop.ps1 -Remove  # Stop and remove containers
#>

param(
    [switch]$Remove  # Remove containers after stopping
)

$ErrorActionPreference = "Stop"

# Change to repository root
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptPath
Set-Location $repoRoot

Write-Host "🛑 Stopping RiskInsure Services" -ForegroundColor Cyan
Write-Host "=" * 60

try {
    if ($Remove) {
        Write-Host "Stopping and removing all containers..." -ForegroundColor Yellow
        docker-compose down
    } else {
        Write-Host "Stopping all containers (keeping them for quick restart)..." -ForegroundColor Yellow
        docker-compose stop
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Failed to stop services." -ForegroundColor Red
        exit 1
    }

    Write-Host ""
    Write-Host "✅ All services stopped successfully!" -ForegroundColor Green
    
    if (-not $Remove) {
        Write-Host ""
        Write-Host "💡 Tip: Use 'docker-compose start' to restart quickly" -ForegroundColor Yellow
        Write-Host "   Or run .\scripts\docker-start.ps1" -ForegroundColor Gray
    }

} catch {
    Write-Host "❌ Error: $_" -ForegroundColor Red
    exit 1
}
