# Azure Static Web Apps scripts

These scripts create and configure the Azure Static Web App for the modernization patterns site.

## Prerequisites

- Azure CLI (`az`)
- Logged in via `az login`
- (Optional) GitHub CLI (`gh`) if you want to set repo secrets via script

## 1) Create Static Web App (recommended: no GitHub linkage)

```powershell
./01-create-static-webapp.ps1
```

This creates the Static Web App resource only and prints the deployment token. Use the existing workflow in this repo and store the token in GitHub secret `AZURE_STATIC_WEB_APPS_API_TOKEN`.

Optional: create with direct GitHub linkage (requires repo admin-compatible token):

```powershell
./01-create-static-webapp.ps1 -UseGitHubIntegration -Branch "main" -GitHubToken "<github-pat>"
```

Parameters (defaults shown in script):
- `-ResourceGroupName` (default: `CAIS-010-RiskInsure`)
- `-AppName` (default: `riskinsure-modernizationpatterns`)
- `-Location` (default: `eastus2`)
- `-Sku` (default: `Free`)
- `-UseGitHubIntegration` (optional switch)
- `-RepoUrl` (default: `https://github.com/AIS-Commercial-Business-Unit/RiskInsure`)
- `-Branch` (default: `main`)
- `-AppLocation` (default: `platform/modernizationpatterns`)
- `-OutputLocation` (default: `dist`)
- `-GitHubToken` (optional) - GitHub Personal Access Token with repo access

The script prints the deployment token at the end. Store it in the repo secret `AZURE_STATIC_WEB_APPS_API_TOKEN`.

## 2) Save the deployment token as a GitHub secret (optional)

```powershell
./02-set-github-secret.ps1 -Repo "<org>/<repo>" -SecretValue "<token>"
```

This sets the secret named `AZURE_STATIC_WEB_APPS_API_TOKEN`.

## 3) Cleanup (remove Static Web App only)

```powershell
./03-cleanup-static-webapp.ps1
```

This deletes the Static Web App resource but does not delete the resource group.

## 4) Set custom domain (www)

```powershell
./04-set-custom-domain.ps1
```

This adds `www.modernizationpatterns.com` to the Static Web App and prints the DNS CNAME target to configure.

## 5) Set apex/root domain

```powershell
./05-set-apex-domain.ps1
```

This adds `modernizationpatterns.com` and prints the DNS target. If your DNS provider supports ALIAS/ANAME at the apex, point it to the app's default hostname.
