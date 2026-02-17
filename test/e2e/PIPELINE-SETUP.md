# E2E Test Pipeline Configuration Guide

## Quick Setup Checklist

### 1. Install Dependencies (Pipeline Step 1)

```bash
cd test/e2e
npm ci
npx playwright install --with-deps chromium
```

### 2. Set Environment Variables (Pipeline Configuration)

**Azure DevOps** - Library Variables:
```
Variable Group: RiskInsure-E2E-Config-Dev

Variables:
- CUSTOMER_API_URL: https://riskinsure-customer-api-dev.azurewebsites.net
- RATING_API_URL: https://riskinsure-rating-api-dev.azurewebsites.net
- POLICY_API_URL: https://riskinsure-policy-api-dev.azurewebsites.net
- BILLING_API_URL: https://riskinsure-billing-api-dev.azurewebsites.net
- FUNDS_TRANSFER_API_URL: https://riskinsure-funds-api-dev.azurewebsites.net
- EVENTUAL_CONSISTENCY_TIMEOUT: 15000
```

**GitHub Actions** - Repository Secrets:
```
Settings → Secrets and variables → Actions

Secrets:
- DEV_CUSTOMER_API_URL
- DEV_RATING_API_URL
- DEV_POLICY_API_URL
- DEV_BILLING_API_URL
- DEV_FUNDS_API_URL
```

### 3. Run Tests (Pipeline Step 2)

```bash
cd test/e2e
npm test
```

### 4. Publish Results (Pipeline Step 3)

**Azure DevOps**:
```yaml
- task: PublishTestResults@2
  inputs:
    testResultsFormat: 'JUnit'
    testResultsFiles: 'test/e2e/test-results.json'
```

**GitHub Actions**:
```yaml
- uses: actions/upload-artifact@v3
  with:
    name: playwright-report
    path: test/e2e/playwright-report/
```

---

## Environment-Specific Configuration

### Local Development
```bash
# Uses defaults from api-endpoints.ts
# No environment variables needed
cd test/e2e
npm test
```

### Dev Environment
```bash
export CUSTOMER_API_URL=https://dev.riskinsure.com/customer
export RATING_API_URL=https://dev.riskinsure.com/rating
export POLICY_API_URL=https://dev.riskinsure.com/policy
export BILLING_API_URL=https://dev.riskinsure.com/billing
export FUNDS_TRANSFER_API_URL=https://dev.riskinsure.com/funds
npm test
```

### Staging Environment
```bash
export CUSTOMER_API_URL=https://staging.riskinsure.com/customer
export RATING_API_URL=https://staging.riskinsure.com/rating
export POLICY_API_URL=https://staging.riskinsure.com/policy
export BILLING_API_URL=https://staging.riskinsure.com/billing
export FUNDS_TRANSFER_API_URL=https://staging.riskinsure.com/funds
export EVENTUAL_CONSISTENCY_TIMEOUT=20000
npm test
```

---

## Complete Pipeline Examples

### Azure DevOps YAML

```yaml
trigger:
  branches:
    include:
    - main
    - develop

pool:
  vmImage: 'ubuntu-latest'

variables:
  - group: RiskInsure-E2E-Config-Dev

stages:
- stage: E2ETests
  displayName: 'E2E Integration Tests'
  jobs:
  - job: RunTests
    displayName: 'Run E2E Tests'
    steps:
    
    - task: NodeTool@0
      inputs:
        versionSpec: '20.x'
      displayName: 'Install Node.js'
    
    - script: |
        cd test/e2e
        npm ci
      displayName: 'Install npm dependencies'
    
    - script: |
        cd test/e2e
        npx playwright install --with-deps chromium
      displayName: 'Install Playwright browsers'
    
    - script: |
        cd test/e2e
        npm test
      displayName: 'Run E2E tests'
      env:
        CUSTOMER_API_URL: $(CUSTOMER_API_URL)
        RATING_API_URL: $(RATING_API_URL)
        POLICY_API_URL: $(POLICY_API_URL)
        BILLING_API_URL: $(BILLING_API_URL)
        FUNDS_TRANSFER_API_URL: $(FUNDS_TRANSFER_API_URL)
        EVENTUAL_CONSISTENCY_TIMEOUT: $(EVENTUAL_CONSISTENCY_TIMEOUT)
    
    - task: PublishTestResults@2
      condition: always()
      inputs:
        testResultsFormat: 'JUnit'
        testResultsFiles: 'test/e2e/test-results.json'
        failTaskOnFailedTests: true
      displayName: 'Publish test results'
    
    - task: PublishBuildArtifacts@1
      condition: always()
      inputs:
        PathtoPublish: 'test/e2e/playwright-report'
        ArtifactName: 'playwright-report'
      displayName: 'Publish Playwright report'
```

### GitHub Actions YAML

```yaml
name: E2E Integration Tests

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]
  workflow_dispatch:

jobs:
  e2e-tests:
    runs-on: ubuntu-latest
    
    steps:
    - name: Checkout code
      uses: actions/checkout@v4
    
    - name: Setup Node.js
      uses: actions/setup-node@v4
      with:
        node-version: '20'
        cache: 'npm'
        cache-dependency-path: 'test/e2e/package-lock.json'
    
    - name: Install dependencies
      working-directory: test/e2e
      run: npm ci
    
    - name: Install Playwright browsers
      working-directory: test/e2e
      run: npx playwright install --with-deps chromium
    
    - name: Run E2E tests
      working-directory: test/e2e
      env:
        CUSTOMER_API_URL: ${{ secrets.DEV_CUSTOMER_API_URL }}
        RATING_API_URL: ${{ secrets.DEV_RATING_API_URL }}
        POLICY_API_URL: ${{ secrets.DEV_POLICY_API_URL }}
        BILLING_API_URL: ${{ secrets.DEV_BILLING_API_URL }}
        FUNDS_TRANSFER_API_URL: ${{ secrets.DEV_FUNDS_API_URL }}
        EVENTUAL_CONSISTENCY_TIMEOUT: 15000
      run: npm test
    
    - name: Upload Playwright report
      if: always()
      uses: actions/upload-artifact@v4
      with:
        name: playwright-report
        path: test/e2e/playwright-report/
        retention-days: 30
    
    - name: Upload test results
      if: always()
      uses: actions/upload-artifact@v4
      with:
        name: test-results
        path: test/e2e/test-results.json
        retention-days: 30
```

---

## Troubleshooting Pipeline Failures

### Issue: "CUSTOMER_API_URL is not defined"

**Solution**: Add variable to pipeline configuration or variable group

```bash
# Azure DevOps: Library → Variable Groups → Add variable
# GitHub Actions: Settings → Secrets → Add secret
```

### Issue: "Connection refused" in pipeline

**Solutions**:
1. Verify APIs are deployed and running
2. Check network/firewall rules allow pipeline agent access
3. Use Azure-hosted agents with VNET peering if needed
4. Test API accessibility: `curl https://dev.riskinsure.com/customer/api/customers`

### Issue: Tests timeout in pipeline but pass locally

**Solutions**:
1. Increase timeout: `EVENTUAL_CONSISTENCY_TIMEOUT=20000`
2. Add retries: `retries: 2` in playwright.config.ts (already configured for CI)
3. Check RabbitMQ/NServiceBus processing in deployed environment
4. Verify Cosmos DB connection from deployed endpoints

### Issue: "npm ci" fails

**Solution**: Ensure package-lock.json is committed
```bash
cd test/e2e
npm install  # Regenerate package-lock.json
git add package-lock.json
git commit -m "Add package-lock.json for E2E tests"
```

---

## Monitoring & Alerts

### Set Up Pipeline Alerts

**Azure DevOps**:
- Project Settings → Notifications
- Create subscription: "Build fails" → Send email to team

**GitHub Actions**:
- Repository → Settings → Notifications
- Enable workflow failure notifications

### Test Result Dashboard

View test trends over time:

**Azure DevOps**:
- Pipelines → Your pipeline → Analytics → Test analytics

**GitHub Actions**:
- Actions tab → Workflow runs → View test results artifact

---

## Next Steps

1. ✅ Run tests locally: `cd test/e2e && npm test`
2. ✅ Add E2E tests to your pipeline using examples above
3. ✅ Configure environment variables for your dev environment
4. ✅ Set up test result publishing
5. ✅ Configure failure notifications
6. 📋 Add more E2E flows as needed (payment, cancellation, etc.)

---

**Quick Reference**: Set these 5 environment variables and run `npm test`:
```bash
CUSTOMER_API_URL
RATING_API_URL
POLICY_API_URL
BILLING_API_URL
FUNDS_TRANSFER_API_URL
```
