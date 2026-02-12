#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Check Service Bus Emulator provisioned entities by querying SQL Server backend.
    
.DESCRIPTION
    The Service Bus emulator stores all entity metadata in SQL Server.
    This script connects to that database and shows what's actually provisioned.
    
.PARAMETER SqlPassword
    SQL Server SA password. Loads from .env if not provided.
    
.EXAMPLE
    .\check-servicebus-status.ps1
#>

param(
    [string]$SqlPassword
)

# Load password from .env if not provided
if (-not $SqlPassword) {
    $env_file = Join-Path $PSScriptRoot ".." ".env"
    if (Test-Path $env_file) {
        $envContent = Get-Content $env_file -Raw
        $passLine = $envContent -split "`n" | Where-Object {$_ -match '^SQL_SERVER_SA_PASSWORD='} | Select-Object -First 1
        if ($passLine) {
            $SqlPassword = $passLine -replace '^SQL_SERVER_SA_PASSWORD=', ''
            $SqlPassword = $SqlPassword -replace '"', ''  # Remove quotes if present
        }
    }
}

if (-not $SqlPassword) {
    Write-Host "ERROR: SQL_SERVER_SA_PASSWORD not found" -ForegroundColor Red
    Write-Host "Please provide -SqlPassword or ensure .env file exists" -ForegroundColor Yellow
    exit 1
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Service Bus Emulator Status" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Query queues
Write-Host "[QUEUES]" -ForegroundColor Green
$queues = docker exec sql-server /opt/mssql-tools18/bin/sqlcmd -h -1 -S localhost -U sa "-P$SqlPassword" -C -d SbGatewayDatabase -Q "SELECT EntityName FROM EntityNameToContainerTable WHERE EntityName LIKE '%:queue:%' ORDER BY EntityName" 2>&1 | 
    Where-Object {$_ -match '^\s*sbemulatorns:queue:'} |
    ForEach-Object {$_.Trim() -replace '^sbemulatorns:queue:', ''}

if ($queues) {
    if ($queues -is [string]) {
        $queueList = @($queues)
    } else {
        $queueList = $queues
    }
    $queueList | ForEach-Object { Write-Host "  ✓ $_" -ForegroundColor White }
    Write-Host "  Total: $($queueList.Count) queues" -ForegroundColor Cyan
} else {
    Write-Host "  No queues found" -ForegroundColor Yellow
}

Write-Host ""

# Query topics
Write-Host "[TOPICS]" -ForegroundColor Green
$topics = docker exec sql-server /opt/mssql-tools18/bin/sqlcmd -h -1 -S localhost -U sa "-P$SqlPassword" -C -d SbGatewayDatabase -Q "SELECT EntityName FROM EntityNameToContainerTable WHERE EntityName LIKE '%:topic:%' ORDER BY EntityName" 2>&1 | 
    Where-Object {$_ -match '^\s*sbemulatorns:topic:'} |
    ForEach-Object {$_.Trim() -replace '^sbemulatorns:topic:', ''}

if ($topics) {
    if ($topics -is [string]) {
        $topicList = @($topics)
    } else {
        $topicList = $topics
    }
    $topicList | ForEach-Object { Write-Host "  ✓ $_" -ForegroundColor White }
    Write-Host "  Total: $($topicList.Count) topics" -ForegroundColor Cyan
} else {
    Write-Host "  No topics found" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Policy Endpoint Queue Status: " -ForegroundColor Cyan -NoNewline

$policyQueue = docker exec sql-server /opt/mssql-tools18/bin/sqlcmd -h -1 -S localhost -U sa "-P$SqlPassword" -C -d SbGatewayDatabase -Q "SELECT COUNT(*) FROM EntityNameToContainerTable WHERE EntityName = 'sbemulatorns:queue:riskinsure.policy.endpoint'" 2>&1 | 
    Where-Object {$_ -match '^\s*\d+\s*$'} |
    ForEach-Object {$_.Trim()}

if ($policyQueue -eq "1") {
    Write-Host "✓ EXISTS" -ForegroundColor Green
} else {
    Write-Host "✗ NOT FOUND" -ForegroundColor Red
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
