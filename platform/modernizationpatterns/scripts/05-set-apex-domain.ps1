param(
  [string]$ResourceGroupName = "CAIS-010-RiskInsure",
  [string]$AppName = "riskinsure-modernizationpatterns",
  [string]$Hostname = "modernizationpatterns.com"
)

$az = Get-Command az -ErrorAction SilentlyContinue
if (-not $az) {
  Write-Error "Azure CLI (az) is required. Install it from https://aka.ms/azure-cli"
  exit 1
}

$defaultHost = az staticwebapp show --name $AppName --resource-group $ResourceGroupName --query defaultHostname -o tsv
if (-not $defaultHost) {
  Write-Error "Unable to read default hostname for $AppName in $ResourceGroupName"
  exit 1
}

Write-Host "Adding apex custom domain $Hostname"
az staticwebapp hostname set --name $AppName --resource-group $ResourceGroupName --hostname $Hostname | Out-Null

Write-Host ""
Write-Host "Create a DNS CNAME record (if your DNS provider supports ALIAS/ANAME for apex):"
Write-Host "  Host: $Hostname"
Write-Host "  Points to: $defaultHost"
Write-Host ""
Write-Host "If your DNS provider does not support ALIAS/ANAME at the apex, use a provider-specific workaround."
