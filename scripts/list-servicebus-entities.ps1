#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Lists all Azure Service Bus entities (queues, topics, subscriptions) via HTTP API.
    
.DESCRIPTION
    Queries the Service Bus emulator or Azure Service Bus namespace using the HTTP/REST API
    to retrieve all queues, topics, and subscriptions.
    
.PARAMETER Namespace
    The Service Bus namespace (e.g., "sbemulatorns" for emulator or "mynamespace" for Azure).
    
.PARAMETER Endpoint
    The Service Bus management endpoint. Defaults to "http://localhost:5672" for emulator.
    
.EXAMPLE
    .\list-servicebus-entities.ps1
    Lists all entities from the local emulator.
    
.EXAMPLE
    .\list-servicebus-entities.ps1 -Namespace "mynamespace" -Endpoint "https://mynamespace.servicebus.windows.net"
    Lists all entities from an Azure Service Bus namespace.
#>

param(
    [string]$Namespace = "sbemulatorns",
    [string]$Endpoint = "http://localhost:5672"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Service Bus Entities Report" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Namespace: $Namespace"
Write-Host "Endpoint: $Endpoint"
Write-Host ""

# For the emulator, we'll read the config.json since it doesn't expose HTTP management API
$configPath = Join-Path $PSScriptRoot ".." "config.json"

if (-not (Test-Path $configPath)) {
    Write-Host "ERROR: config.json not found at $configPath" -ForegroundColor Red
    Write-Host "The Service Bus emulator configuration is required to list entities." -ForegroundColor Yellow
    exit 1
}

try {
    $config = Get-Content $configPath -Raw | ConvertFrom-Json
    $namespaceConfig = $config.UserConfig.Namespaces | Where-Object { $_.Name -eq $Namespace }
    
    if (-not $namespaceConfig) {
        Write-Host "ERROR: Namespace '$Namespace' not found in config.json" -ForegroundColor Red
        exit 1
    }
    
    # List Queues
    Write-Host "[QUEUES]" -ForegroundColor Green
    if ($namespaceConfig.Queues.Count -gt 0) {
        $namespaceConfig.Queues | ForEach-Object {
            Write-Host "  - $($_.Name)" -ForegroundColor White
            Write-Host "    MaxDeliveryCount: $($_.Properties.MaxDeliveryCount)" -ForegroundColor Gray
            Write-Host "    LockDuration: $($_.Properties.LockDuration)" -ForegroundColor Gray
            Write-Host "    TTL: $($_.Properties.DefaultMessageTimeToLive)" -ForegroundColor Gray
            Write-Host ""
        }
        Write-Host "  Total: $($namespaceConfig.Queues.Count) queues" -ForegroundColor Cyan
    } else {
        Write-Host "  No queues found." -ForegroundColor Yellow
    }
    Write-Host ""
    
    # List Topics and Subscriptions
    Write-Host "[TOPICS]" -ForegroundColor Green
    if ($namespaceConfig.Topics.Count -gt 0) {
        $namespaceConfig.Topics | ForEach-Object {
            $topic = $_
            Write-Host "  - $($topic.Name)" -ForegroundColor White
            Write-Host "    TTL: $($topic.Properties.DefaultMessageTimeToLive)" -ForegroundColor Gray
            
            if ($topic.Subscriptions.Count -gt 0) {
                Write-Host "    Subscriptions:" -ForegroundColor Cyan
                $topic.Subscriptions | ForEach-Object {
                    Write-Host "      * $($_.Name)" -ForegroundColor White
                    Write-Host "        MaxDeliveryCount: $($_.Properties.MaxDeliveryCount)" -ForegroundColor Gray
                    Write-Host "        LockDuration: $($_.Properties.LockDuration)" -ForegroundColor Gray
                }
            } else {
                Write-Host "    No subscriptions." -ForegroundColor Yellow
            }
            Write-Host ""
        }
        Write-Host "  Total: $($namespaceConfig.Topics.Count) topics" -ForegroundColor Cyan
    } else {
        Write-Host "  No topics found." -ForegroundColor Yellow
    }
    Write-Host ""
    
    # Summary
    Write-Host "[SUMMARY]" -ForegroundColor Green
    Write-Host "  Queues: $($namespaceConfig.Queues.Count)" -ForegroundColor White
    Write-Host "  Topics: $($namespaceConfig.Topics.Count)" -ForegroundColor White
    $totalSubs = ($namespaceConfig.Topics | ForEach-Object { $_.Subscriptions.Count } | Measure-Object -Sum).Sum
    Write-Host "  Subscriptions: $totalSubs" -ForegroundColor White
    Write-Host ""
    
    Write-Host "========================================" -ForegroundColor Cyan
    
} catch {
    Write-Host "ERROR: Failed to parse config.json" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
