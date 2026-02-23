param(
  [Parameter(Mandatory = $true)]
  [string]$Repo,
  [Parameter(Mandatory = $true)]
  [string]$SecretValue,
  [string]$SecretName = "AZURE_STATIC_WEB_APPS_API_TOKEN"
)

$gh = Get-Command gh -ErrorAction SilentlyContinue
if (-not $gh) {
  Write-Error "GitHub CLI (gh) is required for this script. Install it from https://cli.github.com"
  exit 1
}

Write-Host "Setting GitHub secret $SecretName for $Repo"
$SecretValue | gh secret set $SecretName --repo $Repo
Write-Host "Secret set."
