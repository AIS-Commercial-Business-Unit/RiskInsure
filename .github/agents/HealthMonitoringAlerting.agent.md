---
name: Azure Container Apps Health Check
description: Checks the health of specified Azure Container Apps and summarizes results.
# Schedule to run every morning at 9 AM UTC
on:
  schedule:
    - cron: '0 2 * * *'
  workflow_dispatch: {}

# Agentic Workflows are read-only by default, perfect for your "no new items" rule.
permissions:
  contents: read
---

Check the status of my Azure Container Apps using the following list of base URLs. 

For each URL:
1. Append `/health` to the end of the URL.
2. Perform an unauthenticated HTTP GET request.
3. Determine if the app is "Healthy" (HTTP 200) or "Unhealthy" (any other status or timeout).

### Target URLs
- https://fundstransfermgt-api.ambitioussea-f3f6277f.eastus2.azurecontainerapps.io/health
- https://crmgt-api.ambitioussea-f3f6277f.eastus2.azurecontainerapps.io/health
- https://policylifecyclemgt-api.ambitioussea-f3f6277f.eastus2.azurecontainerapps.io/health
- https://peimgt-api.ambitioussea-f3f6277f.eastus2.azurecontainerapps.io/health
- https://rru-api.ambitioussea-f3f6277f.eastus2.azurecontainerapps.io/health

### Output Instructions
DO NOT create any GitHub Issues, Pull Requests, or Comments. 
Instead, output a Markdown table summarizing the results directly to the **GitHub Actions Job Summary**.

The table should have the following columns:

| App Name | Health Endpoint | Status | Latency (ms) |
|----------|-----------------|--------|--------------|
