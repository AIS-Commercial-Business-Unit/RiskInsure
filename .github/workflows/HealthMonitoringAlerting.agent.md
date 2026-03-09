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

{{#runtime-import .github/agents/HealthMonitoringAlerting.agent.md}}
