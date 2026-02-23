param(
  [string]$ResourceGroupName = "CAIS-010-RiskInsure",
  [string]$AppName = "riskinsure-modernizationpatterns"
)

$az = Get-Command az -ErrorAction SilentlyContinue
if (-not $az) {
  Write-Error "Azure CLI (az) is required. Install it from https://aka.ms/azure-cli"
  exit 1
}

Write-Host "Deleting Static Web App $AppName in $ResourceGroupName"
az staticwebapp delete --name $AppName --resource-group $ResourceGroupName -y

Write-Host "Done. Resource group was not deleted."
