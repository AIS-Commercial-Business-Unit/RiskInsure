#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Simple Cosmos DB initialization using curl
.DESCRIPTION
    Creates database and containers using Cosmos DB REST API via curl
#>

param(
    [string]$DatabaseName = "RiskInsure"
)

$ErrorActionPreference = "Continue"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Cosmos DB Quick Setup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Wait for Cosmos DB
Write-Host "Waiting for Cosmos DB..." -ForegroundColor Yellow
$ready = $false
$attempts = 0
while (-not $ready -and $attempts -lt 60) {
    try {
        $null = curl.exe -k -s https://localhost:8081/_explorer/index.html --max-time 3
        if ($LASTEXITCODE -eq 0) {
            $ready = $true
            Write-Host "✓ Cosmos DB is ready" -ForegroundColor Green
        }
    } catch {
        # Ignore
    }
    if (-not $ready) {
        Start-Sleep -Seconds 2
        $attempts++
        Write-Host "  Waiting... ($attempts/60)" -ForegroundColor Gray
    }
}

if (-not $ready) {
    Write-Host "✗ Cosmos DB not ready" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Opening Cosmos DB Data Explorer..." -ForegroundColor Cyan
Write-Host "  URL: https://localhost:8081/_explorer/index.html" -ForegroundColor Yellow
Write-Host ""
Write-Host "Manual Setup Steps:" -ForegroundColor Yellow
Write-Host "  1. Accept the certificate warning in your browser" -ForegroundColor Gray
Write-Host "  2. Click 'New Database'" -ForegroundColor Gray
Write-Host "  3. Database ID: $DatabaseName" -ForegroundColor Gray
Write-Host "  4. Create these containers:" -ForegroundColor Gray
Write-Host "     - Billing (partition key: /orderId)" -ForegroundColor Gray
Write-Host "     - customer (partition key: /customerId)" -ForegroundColor Gray
Write-Host "     - fundstransfermgt (partition key: /fileRunId)" -ForegroundColor Gray
Write-Host "     - policy (partition key: /policyId)" -ForegroundColor Gray
Write-Host "     - ratingunderwriting (partition key: /quoteId)" -ForegroundColor Gray
Write-Host ""

# Try to open browser
try {
    Start-Process "https://localhost:8081/_explorer/index.html"
    Write-Host "✓ Browser opened" -ForegroundColor Green
} catch {
    Write-Host "⚠ Could not open browser automatically" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "After creating the database and containers," -ForegroundColor Cyan
Write-Host "run: docker compose up -d" -ForegroundColor White
Write-Host ""
