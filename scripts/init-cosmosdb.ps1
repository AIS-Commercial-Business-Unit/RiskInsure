<#
.SYNOPSIS
    Initialize Cosmos DB Emulator with databases and containers
.DESCRIPTION
    Creates the RiskInsure database and all domain containers before starting services.
    Prevents timeout issues during application startup.
.EXAMPLE
    .\scripts\init-cosmosdb.ps1
#>

param(
    [string]$CosmosEndpoint = "https://localhost:8081",
    [string]$CosmosKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
    [string]$DatabaseName = "RiskInsure"
)

$ErrorActionPreference = "Continue"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Cosmos DB Initialization" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Container definitions (partition keys extracted from source code)
$containers = @(
    @{ Name = "Billing"; PartitionKey = "/accountId" },
    @{ Name = "customer"; PartitionKey = "/customerId" },
    @{ Name = "FundTransferMgt-PaymentMethods"; PartitionKey = "/customerId" },
    @{ Name = "FundTransferMgt-Transactions"; PartitionKey = "/customerId" },
    @{ Name = "policy"; PartitionKey = "/policyId" },
    @{ Name = "ratingunderwriting"; PartitionKey = "/quoteId" },
    @{ Name = "Billing-Sagas"; PartitionKey = "/CorrelationId" },
    @{ Name = "customer-Sagas"; PartitionKey = "/CorrelationId" },
    @{ Name = "FundTransferMgt-Sagas"; PartitionKey = "/CorrelationId" },
    @{ Name = "policy-Sagas"; PartitionKey = "/CorrelationId" },
    @{ Name = "ratingunderwriting-Sagas"; PartitionKey = "/CorrelationId" }
)
)

# Wait for Cosmos DB to be ready
Write-Host "Waiting for Cosmos DB to be ready..." -ForegroundColor Yellow
$maxAttempts = 30
$attempt = 0
$cosmosReady = $false

while (-not $cosmosReady -and $attempt -lt $maxAttempts) {
    try {
        $attempt++
        $response = Invoke-WebRequest -Uri "$CosmosEndpoint/_explorer/index.html" `
            -Method GET `
            -SkipCertificateCheck `
            -TimeoutSec 5 `
            -UseBasicParsing `
            -ErrorAction Stop
        
        if ($response.StatusCode -eq 200) {
            $cosmosReady = $true
            Write-Host "✓ Cosmos DB is ready" -ForegroundColor Green
        }
    } catch {
        Write-Host "  Attempt $attempt/$maxAttempts - Waiting..." -ForegroundColor Gray
        Start-Sleep -Seconds 2
    }
}

if (-not $cosmosReady) {
    Write-Host "✗ Cosmos DB failed to start after $maxAttempts attempts" -ForegroundColor Red
    Write-Host "  Check: docker logs cosmos-emulator" -ForegroundColor Yellow
    exit 1
}

Write-Host ""

# Install Azure Cosmos SDK if needed
Write-Host "Checking Azure Cosmos SDK..." -ForegroundColor Cyan
$cosmosModule = Get-Module -ListAvailable -Name Az.CosmosDB -ErrorAction SilentlyContinue
if (-not $cosmosModule) {
    Write-Host "  Azure Cosmos SDK not found - using REST API instead" -ForegroundColor Yellow
}

# Helper function to call Cosmos REST API
# Based on: https://learn.microsoft.com/en-us/rest/api/cosmos-db/access-control-on-cosmosdb-resources
function Invoke-CosmosApi {
    param(
        [string]$Method,
        [string]$ResourceType,
        [string]$ResourceLink,
        [object]$Body = $null
    )
    
    # 1. Generate UTC date in RFC 7231 format
    $date = [DateTime]::UtcNow.ToString("r")
    
    # 2. Build signature payload per Microsoft specification:
    #    "{verb}\n{resourceType}\n{resourceLink}\n{date}\n\n"
    #    - verb: lowercase HTTP method (post, get, put, delete)
    #    - resourceType: lowercase type (dbs, colls, docs, etc.)
    #    - resourceLink: case-sensitive path ("", "dbs/dbname", etc.)
    #    - date: lowercase RFC 7231 date
    #    - Two newlines at the end
    $verb = $Method.ToLowerInvariant()
    $resourceTypeLower = $ResourceType.ToLowerInvariant()
    $dateLower = $date.ToLowerInvariant()
    $payLoad = "$verb`n$resourceTypeLower`n$ResourceLink`n$dateLower`n`n"
    
    # 3. Compute HMAC-SHA256 signature
    $keyBytes = [Convert]::FromBase64String($CosmosKey)
    $hmacSha = New-Object System.Security.Cryptography.HMACSHA256
    $hmacSha.Key = $keyBytes
    $hashPayLoad = $hmacSha.ComputeHash([Text.Encoding]::UTF8.GetBytes($payLoad))
    $signature = [Convert]::ToBase64String($hashPayLoad)
    
    # 4. Build authorization header (URL-encoded)
    $authToken = [System.Web.HttpUtility]::UrlEncode("type=master&ver=1.0&sig=$signature")
    
    # 5. Build request headers
    $headers = @{
        "authorization" = $authToken
        "x-ms-date" = $date
        "x-ms-version" = "2018-12-31"
    }
    
    if ($Body) {
        $headers["Content-Type"] = "application/json"
    }
    
    # 6. Build URL based on resource hierarchy
    if ($ResourceLink) {
        # For child resources: https://localhost:8081/dbs/{db-id}/colls
        $uri = "$CosmosEndpoint/$ResourceLink/$ResourceType"
    } else {
        # For top-level resources: https://localhost:8081/dbs
        $uri = "$CosmosEndpoint/$ResourceType"
    }
    
    # 7. Make the request
    try {
        if ($Body) {
            $jsonBody = $Body | ConvertTo-Json -Depth 10 -Compress
            $response = Invoke-RestMethod -Uri $uri `
                -Method $Method `
                -Headers $headers `
                -Body $jsonBody `
                -SkipCertificateCheck `
                -ErrorAction Stop
        } else {
            $response = Invoke-RestMethod -Uri $uri `
                -Method $Method `
                -Headers $headers `
                -SkipCertificateCheck `
                -ErrorAction Stop
        }
        return $response
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq 409) {
            # 409 Conflict = resource already exists (expected)
            return $null
        }
        # Log error details for debugging
        Write-Host "    Error: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) {
            Write-Host "    Details: $($_.ErrorDetails.Message)" -ForegroundColor Red
        }
        throw
    }
}

# Create database
Write-Host "Creating database '$DatabaseName'..." -ForegroundColor Cyan
try {
    $dbBody = @{
        id = $DatabaseName
    }
    
    $result = Invoke-CosmosApi -Method "POST" -ResourceType "dbs" -ResourceLink "" -Body $dbBody
    
    if ($result) {
        Write-Host "  ✓ Database created" -ForegroundColor Green
    } else {
        Write-Host "  ✓ Database already exists" -ForegroundColor Green
    }
} catch {
    if ($_ -match "409") {
        Write-Host "  ✓ Database already exists" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Failed to create database: $_" -ForegroundColor Red
        throw
    }
}

Write-Host ""

# Create containers
Write-Host "Creating containers..." -ForegroundColor Cyan
foreach ($container in $containers) {
    Write-Host "  Creating container '$($container.Name)'..." -ForegroundColor Gray
    
    try {
        $containerBody = @{
            id = $container.Name
            partitionKey = @{
                paths = @($container.PartitionKey)
                kind = "Hash"
            }
        }
        
        $result = Invoke-CosmosApi -Method "POST" `
            -ResourceType "colls" `
            -ResourceLink "dbs/$DatabaseName" `
            -Body $containerBody
        
        if ($result) {
            Write-Host "    ✓ Container '$($container.Name)' created (partition key: $($container.PartitionKey))" -ForegroundColor Green
        } else {
            Write-Host "    ✓ Container '$($container.Name)' already exists" -ForegroundColor Green
        }
    } catch {
        if ($_ -match "409") {
            Write-Host "    ✓ Container '$($container.Name)' already exists" -ForegroundColor Green
        } else {
            Write-Host "    ✗ Failed to create container '$($container.Name)': $_" -ForegroundColor Red
            Write-Host "    Error: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "✓ Cosmos DB initialization complete" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Database: $DatabaseName" -ForegroundColor Gray
Write-Host "Containers: $($containers.Count)" -ForegroundColor Gray
Write-Host "Endpoint: $CosmosEndpoint" -ForegroundColor Gray
Write-Host ""
Write-Host "You can now start your services:" -ForegroundColor Yellow
Write-Host "  docker compose up -d" -ForegroundColor White
Write-Host ""
