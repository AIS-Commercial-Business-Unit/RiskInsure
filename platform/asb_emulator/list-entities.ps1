#!/usr/bin/env pwsh
<#
.SYNOPSIS
    List queues and topics in the Azure Service Bus Emulator

.DESCRIPTION
    Since the Service Bus Emulator's Admin API returns empty feeds, this script
    provides multiple methods to list configured queues and topics.

.PARAMETER Method
    Method to use: 'Config' (read config.json), 'Detailed' (with subscriptions info)

.EXAMPLE
    .\list-entities.ps1
    Lists all queues and topics from config.json

.EXAMPLE
    .\list-entities.ps1 -Method Detailed
    Shows detailed information including subscriptions
#>

param(
    [ValidateSet('Config', 'Detailed')]
    [string]$Method = 'Config'
)

$ErrorActionPreference = 'Stop'

# Check if emulator is running
$emulatorHealth = try {
    $response = Invoke-RestMethod -Uri "http://localhost:5300/health" -ErrorAction Stop
    $response.status -eq "healthy"
} catch {
    $false
}

if (-not $emulatorHealth) {
    Write-Host "⚠️  Service Bus Emulator is not running or not healthy" -ForegroundColor Yellow
    Write-Host "   Start it with: docker compose up -d`n" -ForegroundColor Gray
    exit 1
}

Write-Host "✓ Service Bus Emulator is running`n" -ForegroundColor Green

# Read config.json
$configPath = Join-Path $PSScriptRoot "config.json"
if (-not (Test-Path $configPath)) {
    Write-Error "config.json not found at: $configPath"
    exit 1
}

$config = Get-Content $configPath | ConvertFrom-Json
$namespace = $config.UserConfig.Namespaces[0]

# Display Queues
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "📬 QUEUES (Namespace: $($namespace.Name))" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

if ($Method -eq 'Detailed') {
    $namespace.Queues | ForEach-Object {
        Write-Host "`n  Queue: " -NoNewline -ForegroundColor Yellow
        Write-Host $_.Name -ForegroundColor White
        Write-Host "    Lock Duration: " -NoNewline -ForegroundColor Gray
        Write-Host $_.Properties.LockDuration
        Write-Host "    Max Delivery Count: " -NoNewline -ForegroundColor Gray
        Write-Host $_.Properties.MaxDeliveryCount
        Write-Host "    TTL: " -NoNewline -ForegroundColor Gray
        Write-Host $_.Properties.DefaultMessageTimeToLive
        Write-Host "    Requires Session: " -NoNewline -ForegroundColor Gray
        Write-Host $_.Properties.RequiresSession
    }
} else {
    $namespace.Queues | ForEach-Object {
        Write-Host "  • " -NoNewline -ForegroundColor Gray
        Write-Host $_.Name
    }
}

Write-Host "`n  Total: " -NoNewline -ForegroundColor Gray
Write-Host $namespace.Queues.Count -ForegroundColor White

# Display Topics
Write-Host "`n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "📢 TOPICS (Pub/Sub)" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

if ($Method -eq 'Detailed') {
    $namespace.Topics | ForEach-Object {
        Write-Host "`n  Topic: " -NoNewline -ForegroundColor Yellow
        Write-Host $_.Name -ForegroundColor White
        Write-Host "    TTL: " -NoNewline -ForegroundColor Gray
        Write-Host $_.Properties.DefaultMessageTimeToLive
        Write-Host "    Requires Duplicate Detection: " -NoNewline -ForegroundColor Gray
        Write-Host $_.Properties.RequiresDuplicateDetection
        Write-Host "    Subscriptions: " -ForegroundColor Magenta
        $_.Subscriptions | ForEach-Object {
            Write-Host "      → " -NoNewline -ForegroundColor Gray
            Write-Host $_.Name -ForegroundColor White
            Write-Host "        Lock Duration: " -NoNewline -ForegroundColor DarkGray
            Write-Host $_.Properties.LockDuration
            Write-Host "        Max Delivery Count: " -NoNewline -ForegroundColor DarkGray
            Write-Host $_.Properties.MaxDeliveryCount
        }
    }
} else {
    $namespace.Topics | ForEach-Object {
        Write-Host "  • " -NoNewline -ForegroundColor Gray
        Write-Host $_.Name -NoNewline
        Write-Host " (" -NoNewline -ForegroundColor DarkGray
        Write-Host "$($_.Subscriptions.Count) subscription(s)" -NoNewline -ForegroundColor DarkGray
        Write-Host ")" -ForegroundColor DarkGray
        $_.Subscriptions | ForEach-Object {
            Write-Host "      → " -NoNewline -ForegroundColor DarkGray
            Write-Host $_.Name -ForegroundColor Gray
        }
    }
}

Write-Host "`n  Total: " -NoNewline -ForegroundColor Gray
Write-Host $namespace.Topics.Count -ForegroundColor White

Write-Host "`n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━`n" -ForegroundColor Cyan

# Export option
Write-Host "💡 Tip: Use " -NoNewline -ForegroundColor DarkGray
Write-Host ".\list-entities.ps1 -Method Detailed" -NoNewline -ForegroundColor Yellow
Write-Host " for full configuration details" -ForegroundColor DarkGray
Write-Host "💡 Tip: Pipe to file with " -NoNewline -ForegroundColor DarkGray
Write-Host ".\list-entities.ps1 | Out-File entities.txt" -ForegroundColor Yellow
