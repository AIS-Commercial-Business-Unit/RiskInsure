<#
.SYNOPSIS
    Check the health and status of all RiskInsure services
.DESCRIPTION
    Shows running status, ports, and quick health check of all services.
.EXAMPLE
    .\scripts\docker-status.ps1
#>

$ErrorActionPreference = "Stop"

# Change to repository root
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptPath
Set-Location $repoRoot

Write-Host "📊 RiskInsure Services Status" -ForegroundColor Cyan
Write-Host "=" * 60
Write-Host ""

try {
    # Check if any services are running
    $runningServices = docker-compose ps -q
    
    if (-not $runningServices) {
        Write-Host "⚠️  No services are currently running" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "💡 Start services with: .\scripts\docker-start.ps1" -ForegroundColor Gray
        exit 0
    }

    # Show detailed status
    Write-Host "🐳 Docker Compose Status:" -ForegroundColor Cyan
    docker-compose ps
    
    Write-Host ""
    Write-Host "🌐 API Health Check:" -ForegroundColor Cyan
    
    # Test each API endpoint
    $apis = @(
        @{Name="Billing"; Url="http://localhost:7071/health"},
        @{Name="Customer"; Url="http://localhost:7073/health"},
        @{Name="Funds Transfer"; Url="http://localhost:7075/health"},
        @{Name="Policy"; Url="http://localhost:7077/health"},
        @{Name="Rating & Underwriting"; Url="http://localhost:7079/health"}
    )
    
    foreach ($api in $apis) {
        try {
            $response = Invoke-WebRequest -Uri $api.Url -TimeoutSec 2 -UseBasicParsing -ErrorAction SilentlyContinue
            if ($response.StatusCode -eq 200) {
                Write-Host "   ✓ $($api.Name): Healthy" -ForegroundColor Green
            } else {
                Write-Host "   ⚠️  $($api.Name): Unhealthy (Status: $($response.StatusCode))" -ForegroundColor Yellow
            }
        } catch {
            Write-Host "   ❌ $($api.Name): Not responding" -ForegroundColor Red
        }
    }
    
    Write-Host ""
    Write-Host "📝 Quick Commands:" -ForegroundColor Cyan
    Write-Host "   View logs:     .\scripts\docker-logs.ps1" -ForegroundColor Gray
    Write-Host "   Stop all:      .\scripts\docker-stop.ps1" -ForegroundColor Gray
    Write-Host "   Rebuild:       .\scripts\docker-rebuild.ps1" -ForegroundColor Gray

} catch {
    Write-Host "❌ Error checking status: $_" -ForegroundColor Red
    exit 1
}
