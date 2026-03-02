# ============================================================================
# Create Azure AD App Registration for ServicePulse Licensing Component
# ============================================================================

# Variables
$appName        = "riskinsure-servicepulse-licensing"
$rgName         = "CAIS-010-RiskInsure"
$sbNamespace    = "riskinsure-dev-bus"
$subscriptionId = "c4fb1c99-fb99-4dc1-9926-a3a4356fd44a"

# Ensure correct subscription
az account set --subscription $subscriptionId

# 1. Create the App Registration
Write-Host "Creating App Registration: $appName ..." -ForegroundColor Cyan
$app = az ad app create --display-name $appName | ConvertFrom-Json
$appId = $app.appId
$objectId = $app.id
Write-Host "  App (client) ID : $appId"
Write-Host "  Object ID       : $objectId"

# 2. Create Service Principal for the app
Write-Host "Creating Service Principal ..." -ForegroundColor Cyan
$sp = az ad sp create --id $appId | ConvertFrom-Json
$spObjectId = $sp.id
Write-Host "  SP Object ID    : $spObjectId"

# 3. Create a client secret (valid 1 year)
Write-Host "Creating Client Secret (1-year expiry) ..." -ForegroundColor Cyan
$secret = az ad app credential reset `
    --id $appId `
    --display-name "ServicePulse Licensing" `
    --years 1 | ConvertFrom-Json
$clientSecret = $secret.password
$tenantId     = $secret.tenant

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " App Registration Created Successfully" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "  ClientId     : $appId"
Write-Host "  ClientSecret : $clientSecret"
Write-Host "  TenantId     : $tenantId"
Write-Host ""
Write-Host "  >>> SAVE THE CLIENT SECRET NOW - it cannot be retrieved again <<<" -ForegroundColor Yellow