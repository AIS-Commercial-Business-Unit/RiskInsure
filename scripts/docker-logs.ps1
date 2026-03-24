<#
.SYNOPSIS
    View consolidated logs from all RiskInsure services
.DESCRIPTION
    Opens a live view of logs from all services or a specific service.
    Press Ctrl+C to exit.
.EXAMPLE
    .\scripts\docker-logs.ps1
    .\scripts\docker-logs.ps1 -Service crmgt-api
    .\scripts\docker-logs.ps1 -Service fundstransfermgt-api -Follow:$false
#>

param(
    [string]$Service = "",  # Specific service name (e.g., "crmgt-api")
    [switch]$Follow = $true # Follow logs in real-time
)

$ErrorActionPreference = "Stop"

# Change to repository root
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptPath
Set-Location $repoRoot

try {
    if ($Service) {
        Write-Host "📋 Viewing logs for: $Service" -ForegroundColor Cyan
        Write-Host "Press Ctrl+C to exit" -ForegroundColor Gray
        Write-Host ""
        
        if ($Follow) {
            docker-compose logs -f $Service
        } else {
            docker-compose logs $Service
        }
    } else {
        Write-Host "📋 Viewing logs for all services" -ForegroundColor Cyan
        Write-Host "Press Ctrl+C to exit" -ForegroundColor Gray
        Write-Host ""
        
        if ($Follow) {
            docker-compose logs -f
        } else {
            docker-compose logs
        }
    }

} catch {
    Write-Host "❌ Error: $_" -ForegroundColor Red
    exit 1
}
