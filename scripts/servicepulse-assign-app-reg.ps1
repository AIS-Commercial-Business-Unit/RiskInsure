# ============================================================================
# Assign Monitoring Reader role on the Service Bus namespace
# ============================================================================

# Variables (re-use from above or set again)
$subscriptionId = "c4fb1c99-fb99-4dc1-9926-a3a4356fd44a"
$rgName         = "CAIS-010-RiskInsure"
$sbNamespace    = "riskinsure-dev-bus"
$appName        = "riskinsure-servicepulse-licensing"

az account set --subscription $subscriptionId

# Get the Service Principal Object ID
$spObjectId = (az ad sp list --display-name $appName --query "[0].id" -o tsv)
Write-Host "Service Principal Object ID: $spObjectId"

# Build scopes - subscription level required for metrics API access
$subscriptionScope = "/subscriptions/$subscriptionId"

# Assign "Monitoring Reader" at subscription level - required because the metrics API
# queries at the resource group scope, not just the namespace scope
Write-Host "Assigning 'Monitoring Reader' at subscription level ..." -ForegroundColor Cyan
az role assignment create `
    --assignee-object-id $spObjectId `
    --assignee-principal-type ServicePrincipal `
    --role "Monitoring Reader" `
    --scope $subscriptionScope `
    2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Role assignment failed (exit code $LASTEXITCODE)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Role assignment complete." -ForegroundColor Green