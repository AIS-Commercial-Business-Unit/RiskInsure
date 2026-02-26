<#
.SYNOPSIS
    Quick smoke test of locally running RiskInsure services
.DESCRIPTION
    Verifies Docker containers are running and API endpoints are accessible.
    Non-destructive read-only checks. Completes in 10-15 seconds.
.EXAMPLE
    .\scripts\smoke-test.ps1
    .\scripts\smoke-test.ps1 -Verbose
#>

param(
    [switch]$Verbose
)

$ErrorActionPreference = "Continue"
$startTime = Get-Date

# Change to repository root
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptPath
Set-Location $repoRoot

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " RiskInsure Local Smoke Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Started: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray
Write-Host ""

$passCount = 0
$failCount = 0
$warnCount = 0

# Step 1: Check Docker
Write-Host "[DOCKER STATUS]" -ForegroundColor Cyan
try {
    wsl docker version | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Docker daemon: Running" -ForegroundColor Green
        $passCount++
    } else {
        Write-Host "  Docker daemon: Not responding" -ForegroundColor Red
        $failCount++
        Write-Host ""
        Write-Host "[OVERALL RESULT]" -ForegroundColor Cyan
        Write-Host "FAIL - Docker is not running. Start Rancher Desktop." -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "  Docker daemon: Not found" -ForegroundColor Red
    $failCount++
    Write-Host ""
    Write-Host "[OVERALL RESULT]" -ForegroundColor Cyan
    Write-Host "FAIL - Docker not installed or not in PATH" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Step 2: Check Containers
Write-Host "[CONTAINER STATUS]" -ForegroundColor Cyan

$expectedContainers = @(
    "riskinsure-billing-api-1",
    "riskinsure-billing-endpoint-1",
    "riskinsure-customer-api-1",
    "riskinsure-customer-endpoint-1",
    "riskinsure-fundstransfermgt-api-1",
    "riskinsure-fundstransfermgt-endpoint-1",
    "riskinsure-policy-api-1",
    "riskinsure-policy-endpoint-1",
    "riskinsure-ratingandunderwriting-api-1",
    "riskinsure-ratingandunderwriting-endpoint-1"
)

$runningContainers = wsl docker ps --filter "name=riskinsure" --format '{{.Names}}'
$allContainersRaw = wsl docker ps -a --filter "name=riskinsure" --format '{{.Names}}:::{{.Status}}'

$containerStatus = @{}
foreach ($line in $allContainersRaw) {
    $parts = $line -split ':::'
    if ($parts.Count -eq 2) {
        $containerStatus[$parts[0]] = $parts[1]
    }
}

$runningCount = 0
foreach ($container in $expectedContainers) {
    if ($containerStatus.ContainsKey($container)) {
        $status = $containerStatus[$container]
        if ($status -like "Up*") {
            Write-Host "  $container" -ForegroundColor Green -NoNewline
            Write-Host " ($status)" -ForegroundColor Gray
            $runningCount++
        } elseif ($status -like "Exited*") {
            Write-Host "  $container" -ForegroundColor Red -NoNewline
            Write-Host " ($status)" -ForegroundColor Gray
            $failCount++
            
            if ($Verbose) {
                Write-Host "    Last 10 log lines:" -ForegroundColor Yellow
                wsl docker logs $container --tail 10 2>&1 | ForEach-Object {
                    Write-Host "      $_" -ForegroundColor Gray
                }
            }
        } else {
            Write-Host "  $container" -ForegroundColor Yellow -NoNewline
            Write-Host " ($status)" -ForegroundColor Gray
            $warnCount++
        }
    } else {
        Write-Host "  $container - NOT FOUND" -ForegroundColor Red
        $failCount++
    }
}

Write-Host ""
Write-Host "  Summary: $runningCount/10 containers running" -ForegroundColor $(if ($runningCount -eq 10) { "Green" } elseif ($runningCount -ge 8) { "Yellow" } else { "Red" })
Write-Host ""

# Step 3: Test API Endpoints
Write-Host "[API CONNECTIVITY]" -ForegroundColor Cyan

$apiEndpoints = @{
    "Billing" = "http://127.0.0.1:7071"
    "Customer" = "http://127.0.0.1:7073"
    "FundsTransferMgt" = "http://127.0.0.1:7075"
    "Policy" = "http://127.0.0.1:7077"
    "RatingAndUnderwriting" = "http://127.0.0.1:7079"
}

$apiPassCount = 0
foreach ($api in $apiEndpoints.GetEnumerator() | Sort-Object Name) {
    try {
        $response = Invoke-WebRequest -Uri $api.Value -Method GET -TimeoutSec 3 -UseBasicParsing -ErrorAction Stop
        Write-Host "  $($api.Key) API: $($api.Value)" -ForegroundColor Green -NoNewline
        Write-Host " ($($response.StatusCode))" -ForegroundColor Gray
        $apiPassCount++
        $passCount++
    } catch {
        $errorMsg = $_.Exception.Message
        if ($errorMsg -like "*404*") {
            Write-Host "  $($api.Key) API: $($api.Value)" -ForegroundColor Green -NoNewline
            Write-Host " (404 - No root endpoint)" -ForegroundColor Gray
            $apiPassCount++
            $passCount++
        } elseif ($errorMsg -like "*Connection refused*" -or $errorMsg -like "*Unable to connect*") {
            Write-Host "  $($api.Key) API: $($api.Value)" -ForegroundColor Red -NoNewline
            Write-Host " (Connection refused)" -ForegroundColor Gray
            $failCount++
        } else {
            Write-Host "  $($api.Key) API: $($api.Value)" -ForegroundColor Yellow -NoNewline
            Write-Host " ($errorMsg)" -ForegroundColor Gray
            $warnCount++
        }
    }
}

Write-Host ""
Write-Host "  Summary: $apiPassCount/5 APIs responding" -ForegroundColor $(if ($apiPassCount -eq 5) { "Green" } elseif ($apiPassCount -ge 4) { "Yellow" } else { "Red" })
Write-Host ""

# Step 4: Check Configuration
Write-Host "[CONFIGURATION]" -ForegroundColor Cyan

if (Test-Path ".env") {
    Write-Host "  .env file: Found" -ForegroundColor Green
    $passCount++
    
    $envContent = Get-Content .env | Out-String
    if ($envContent -match "COSMOSDB_CONNECTION_STRING=AccountEndpoint=https://") {
        Write-Host "  Cosmos DB connection: Valid format" -ForegroundColor Green
        $passCount++
    } else {
        Write-Host "  Cosmos DB connection: Invalid or missing" -ForegroundColor Red
        $failCount++
    }

    if ($envContent -match "SERVICEBUS_CONNECTION_STRING=Endpoint=sb://") {
        Write-Host "  Service Bus connection: Valid format" -ForegroundColor Green        
    }
    if ($envContent -match "RABBITMQ_CONNECTION_STRING=host=") {
        Write-Host "  RabbitMQ connection: Valid format" -ForegroundColor Green
        $passCount++
    }
    if (($envContent -match "SERVICEBUS_CONNECTION_STRING=Endpoint=sb://") -or ($envContent -match "RABBITMQ_CONNECTION_STRING=host=")) {
        $passCount++
    } else {
        Write-Host "  RabbitMQ connection and Service Bus connection: Invalid or missing" -ForegroundColor Red
        $failCount++
    }
} else {
    Write-Host "  .env file: NOT FOUND" -ForegroundColor Red
    $failCount += 3
    Write-Host "    Run: Copy .env.example to .env and configure connection strings" -ForegroundColor Yellow
}

Write-Host ""

# Step 5: Issues Detection
$crashed = wsl docker ps -a --filter "name=riskinsure" --filter "status=exited" --format "{{.Names}}: {{.Status}}"
if ($crashed) {
    Write-Host "[ISSUES DETECTED]" -ForegroundColor Cyan
    $crashed | ForEach-Object {
        Write-Host "  $_" -ForegroundColor Yellow
    }
    Write-Host ""
    Write-Host "[NEXT STEPS]" -ForegroundColor Cyan
    Write-Host "  1. Check logs: wsl docker logs <container-name>" -ForegroundColor Gray
    Write-Host "  2. Restart specific service: wsl docker-compose restart <service-name>" -ForegroundColor Gray
    Write-Host "  3. Restart all services: .\scripts\docker-start.ps1" -ForegroundColor Gray
    Write-Host ""
}

# Step 6: Overall Result
Write-Host "[OVERALL RESULT]" -ForegroundColor Cyan

$duration = (Get-Date) - $startTime
$durationSeconds = [math]::Round($duration.TotalSeconds, 1)

if ($failCount -eq 0 -and $runningCount -eq 10 -and $apiPassCount -eq 5) {
    Write-Host "PASS" -ForegroundColor Green -NoNewline
    Write-Host " - All services operational" -ForegroundColor White
    $exitCode = 0
} elseif ($failCount -le 2 -and $runningCount -ge 8) {
    Write-Host "PARTIAL PASS" -ForegroundColor Yellow -NoNewline
    Write-Host " - $runningCount/10 services running, $apiPassCount/5 APIs responding" -ForegroundColor White
    $exitCode = 0
} else {
    Write-Host "FAIL" -ForegroundColor Red -NoNewline
    Write-Host " - Critical services down ($runningCount/10 containers, $apiPassCount/5 APIs)" -ForegroundColor White
    $exitCode = 1
}

Write-Host ""
Write-Host "Execution time: $durationSeconds seconds" -ForegroundColor Gray
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

exit $exitCode
