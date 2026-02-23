param(
  [string]$ResourceGroupName = "CAIS-010-RiskInsure",
  [string]$AppName = "riskinsure-modernizationpatterns",
  [string]$Location = "eastus2",
  [string]$Sku = "Free",
  [switch]$UseGitHubIntegration,
  [string]$RepoUrl = "https://github.com/AIS-Commercial-Business-Unit/RiskInsure",
  [string]$Branch = "main",
  [string]$AppLocation = "platform/modernizationpatterns",
  [string]$OutputLocation = "dist",
  [string]$GitHubToken
)

$az = Get-Command az -ErrorAction SilentlyContinue
if (-not $az) {
  Write-Error "Azure CLI (az) is required. Install it from https://aka.ms/azure-cli"
  exit 1
}

Write-Host "Using existing resource group $ResourceGroupName"

Write-Host "Creating Static Web App $AppName"
$createArgs = @("staticwebapp", "create", "--name", $AppName, "--resource-group", $ResourceGroupName, "--location", $Location, "--sku", $Sku)

if ($UseGitHubIntegration) {
  $createArgs += @("--source", $RepoUrl, "--branch", $Branch, "--app-location", $AppLocation, "--output-location", $OutputLocation)

  if ($GitHubToken) {
    $createArgs += "--token"
    $createArgs += $GitHubToken
  }
}

& az @createArgs
if ($LASTEXITCODE -ne 0) {
  Write-Error "Static Web App creation failed. Fix the error above, then re-run the script."
  exit 1
}

Write-Host "Retrieving deployment token..."
$token = az staticwebapp secrets list --name $AppName --resource-group $ResourceGroupName --query properties.apiKey -o tsv

Write-Host ""
Write-Host "Deployment token (store as AZURE_STATIC_WEB_APPS_API_TOKEN):"
Write-Host $token
