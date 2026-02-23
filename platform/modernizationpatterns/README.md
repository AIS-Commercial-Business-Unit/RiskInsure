# Modernization Patterns Atlas

This folder hosts the static site that introduces the modernization pattern families. It is intentionally lightweight and designed for fast, immersive navigation.

## Category map

- Strategic patterns
- Technical patterns
- DevOps patterns
- Enablement patterns
- Discovery patterns

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

## Content framework

### Where to put files

- Raw source uploads: [content/_inbox/README.md](content/_inbox/README.md)
- Source catalog (for citations): [content/sources/sources.json](content/sources/sources.json)
- Pattern template: [content/templates/pattern.template.json](content/templates/pattern.template.json)
- Curated patterns (published by site): `content/patterns/*.json`

### Pattern model

All pattern files in `content/patterns` are loaded automatically and must include required fields.

Patterns support a second level grouping via `subcategory`.

Subcategory display metadata is defined in `src/data/taxonomy.js`.

Required sections reflected in the UI:

1. Summary
2. Problem and fit (`problemSolved`, `whenToUse`, `whenNotToUse`)
3. Enabling technologies
4. Things to watch out for (`gotchas` + `opinionatedGuidance`)
5. Complexity score
6. Starter diagram
7. Real-world example
8. Related patterns
9. Further reading

Example structure:

- Category: `technical`
- Subcategory: `messaging-based-patterns`
- Patterns: `publish-subscribe`, `scatter-gather`

### Complexity definition

Use `low`, `medium`, or `high` only.

Complexity is determined by:

- Team impact (single-team vs multi-team)
- Skill demand (depth required to design and implement safely)
- Operational demand (run-time support and operational burden)
- Tooling demand (specialized tools and platform requirements)

Rule of thumb: if a pattern crosses multiple teams, it is typically more complex than a pattern executed within one team.

### Complexity helper script

Use the helper to generate a starting complexity suggestion from the four factors.

```bash
cd platform/modernizationpatterns
npm run suggest-complexity -- \
	--teamImpact multi-team \
	--skillDemand high \
	--operationalDemand medium \
	--toolingDemand medium
```

The helper suggests `low`, `medium`, or `high` and prints a starter `complexity` JSON block you can paste into a pattern file.

### Validation rules

Pattern files are validated on `npm run dev` and `npm run build`.

```bash
cd platform/modernizationpatterns
npm run validate-content
```

If a required field is missing, build fails so checked-in content remains production-ready.

### Curation workflow

1. Drop source files into `content/_inbox`.
2. Add or update any citation source metadata in `content/sources/sources.json`.
3. Copy `content/templates/pattern.template.json` to `content/patterns/<new-pattern>.json`.
4. Fill all required fields and connect `relatedPatterns` by `id`.
5. Run `npm run build` and deploy.
