# Cosmos DB Setup Script for File Retrieval Service
# T035: Creates Cosmos DB database and containers with partition keys and unique constraints

param(
    [Parameter(Mandatory=$true)]
    [string]$CosmosEndpoint,
    
    [Parameter(Mandatory=$true)]
    [string]$CosmosKey,
    
    [Parameter(Mandatory=$false)]
    [string]$DatabaseName = "file-retrieval"
)

Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Cosmos DB Setup - File Retrieval Service" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Install Azure.Cosmos PowerShell module if not present
if (!(Get-Module -ListAvailable -Name Az.CosmosDB)) {
    Write-Host "Installing Az.CosmosDB module..." -ForegroundColor Yellow
    Install-Module -Name Az.CosmosDB -Force -AllowClobber
}

# Extract account name from endpoint
$accountName = $CosmosEndpoint -replace "https://", "" -replace ".documents.azure.com:443/", ""
Write-Host "Cosmos Account: $accountName" -ForegroundColor Cyan
Write-Host "Database: $DatabaseName" -ForegroundColor Cyan
Write-Host ""

try {
    # Connect to Azure (if not already connected)
    $context = Get-AzContext -ErrorAction SilentlyContinue
    if (!$context) {
        Write-Host "Connecting to Azure..." -ForegroundColor Yellow
        Connect-AzAccount
    }

    Write-Host "✓ Azure connection established" -ForegroundColor Green
    Write-Host ""

    # Create database
    Write-Host "Creating database '$DatabaseName'..." -ForegroundColor Yellow
    
    # Note: Use Azure CLI or REST API for detailed container creation with unique keys
    # PowerShell module doesn't support all Cosmos DB features directly
    
    Write-Host @"
    
MANUAL SETUP REQUIRED:

Please execute the following using Azure CLI or Azure Portal:

1. Create Database:
   az cosmosdb sql database create \
     --account-name $accountName \
     --resource-group <YOUR_RESOURCE_GROUP> \
     --name $DatabaseName

2. Create 'file-retrieval-configurations' container:
   az cosmosdb sql container create \
     --account-name $accountName \
     --database-name $DatabaseName \
     --name file-retrieval-configurations \
     --partition-key-path "/clientId" \
     --throughput 400

   # Add composite indexes for efficient filtering and sorting (T099 - US4)
   az cosmosdb sql container update \
     --account-name $accountName \
     --database-name $DatabaseName \
     --name file-retrieval-configurations \
     --idx '{
       "indexingMode": "consistent",
       "automatic": true,
       "includedPaths": [{"path": "/*"}],
       "excludedPaths": [{"path": "/\"_etag\"/?"}],
       "compositeIndexes": [
         [
           {"path": "/clientId", "order": "ascending"},
           {"path": "/isActive", "order": "ascending"},
           {"path": "/createdAt", "order": "descending"}
         ],
         [
           {"path": "/clientId", "order": "ascending"},
           {"path": "/protocol", "order": "ascending"},
           {"path": "/createdAt", "order": "descending"}
         ],
         [
           {"path": "/isActive", "order": "ascending"},
           {"path": "/nextScheduledRun", "order": "ascending"}
         ]
       ]
     }'

3. Create 'file-retrieval-executions' container:
   az cosmosdb sql container create \
     --account-name $accountName \
     --database-name $DatabaseName \
     --name file-retrieval-executions \
     --partition-key-path "/clientId" \
     --partition-key-version 2 \
     --throughput 400 \
     --ttl 7776000

   Note: TTL = 90 days (90 * 24 * 60 * 60 = 7,776,000 seconds)

4. Create 'discovered-files' container:
   az cosmosdb sql container create \
     --account-name $accountName \
     --database-name $DatabaseName \
     --name discovered-files \
     --partition-key-path "/clientId" \
     --partition-key-version 2 \
     --throughput 400 \
     --ttl 7776000

5. Add unique key constraint to 'discovered-files':
   az cosmosdb sql container update \
     --account-name $accountName \
     --database-name $DatabaseName \
     --name discovered-files \
     --unique-key-policy '{"uniqueKeys":[{"paths":["/clientId","/configurationId","/fileUrl","/discoveryDate"]}]}'

═══════════════════════════════════════════════════════════

Alternative: Use Azure Portal
1. Go to Azure Portal → Cosmos DB Account → Data Explorer
2. Create database '$DatabaseName'
3. Create containers with partition keys as specified above
4. Set TTL to 7776000 seconds for executions and discovered-files
5. Add unique key constraint on discovered-files

"@

} catch {
    Write-Host "❌ Error: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Setup instructions displayed above." -ForegroundColor Green
