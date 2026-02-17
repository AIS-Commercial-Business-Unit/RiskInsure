# Modernization Patterns Atlas

This folder hosts the static site that introduces the modernization pattern families. It is intentionally lightweight and designed for fast, immersive navigation.

## Category map

- Enablement patterns
- Technical patterns
- Complexity patterns
- Distributed architecture patterns
- DevOps patterns - purposefully focused on keeping delivery flow moving quickly while keeping the factory floor clean (automation, discipline, and operational readiness).

## Local development

```bash
cd platform/modernizationpatterns
npm install
npm run dev
```

Vite serves the site locally. The default URL is printed in the terminal.

## Production build

```bash
cd platform/modernizationpatterns
npm run build
npm run preview
```

## Azure Static Web Apps deployment

This repo includes a GitHub Actions workflow that deploys the site when changes land on `main`.

Scripts for creating and configuring the Azure resources live in [scripts/README.md](scripts/README.md).

### 1) Create the Static Web App

Use Azure Portal or Azure CLI to create a Static Web App. When prompted:

- **Source**: GitHub
- **App location**: `platform/modernizationpatterns`
- **Output location**: `dist`
- **API location**: leave blank

### 2) Add the deployment secret

In the GitHub repository, add the deployment token as:

- **Secret name**: `AZURE_STATIC_WEB_APPS_API_TOKEN`
- **Value**: the deployment token from the Static Web App resource

### 3) Verify the workflow

The workflow file lives at [.github/workflows/modernizationpatterns-static-webapp.yml](../../.github/workflows/modernizationpatterns-static-webapp.yml).

Once the secret is in place, push to `main` and the site will deploy automatically.

### 4) Custom domain (optional)

In Azure Portal:

1. Open the Static Web App resource.
2. Add a custom domain.
3. Follow Azure instructions to create the CNAME and TXT records.
4. Wait for validation, then set HTTPS.

## Content updates

Edit pattern content in [src/data/patterns.js](src/data/patterns.js). The structure is designed so new categories and examples can be added quickly.
