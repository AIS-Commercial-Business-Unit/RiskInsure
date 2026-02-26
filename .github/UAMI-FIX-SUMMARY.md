# UAMI Issue - Complete Fix Summary

## Problem Statement

The GitHub Actions workflow was failing with error: **"No UAMI ID provided. Infrastructure must be deployed first."**

This occurred when running the workflow with `terraform_action=plan` or when infrastructure outputs were empty.

## Root Causes Identified

### 1. **'N/A' is Truthy in GitHub Expressions**
- `infrastructure.yml` was outputting `'N/A'` as fallback value
- In GitHub Actions expressions: `'N/A' || fallback` returns `'N/A'` (not the fallback!)
- Simple `A || B` pattern only works if A is empty string, null, or undefined

### 2. **Plan + Services = Logical Inconsistency**
- Running `terraform_action=plan` with `run_services=true` attempted to deploy services
- Services require actual infrastructure (not just planned)
- UAMI doesn't exist yet, causing preflight checks to fail

### 3. **Auto-Discovery Didn't Persist**
- `build-and-deploy.yml` discovered UAMI ID but didn't export it as job output
- Later jobs (build_and_push, deploy_apps) still saw empty `inputs.apps_shared_identity_id`
- GitHub Actions pitfall: step variables don't become job outputs automatically

### 4. **Dockerfile Path Logic Broken**
- Used `${SERVICE^}` (bash capitalize first char only)
- Doesn't handle hyphenated names: `file-retrieval` → `File-retrieval` (not `FileRetrieval`)
- Would fail with "Dockerfile not found" after UAMI fix

### 5. **UAMI ID Not Passed to Terraform**
- `deploy_apps` job didn't receive validated UAMI ID
- Terraform services layer needs it as a variable

## Comprehensive Fixes Implemented

### Fix #1: Infrastructure Outputs Empty String (Not 'N/A')
**File:** `.github/workflows/infrastructure.yml`

**Changed:**
```yaml
# BEFORE (foundation layer):
echo "acr_login_server=$(terraform output -raw acr_login_server 2>/dev/null || echo 'N/A')" >> "$GITHUB_OUTPUT"

# AFTER:
echo "acr_login_server=$(terraform output -raw acr_login_server 2>/dev/null || echo '')" >> "$GITHUB_OUTPUT"
```

**Changed:**
```yaml
# BEFORE (shared_services layer):
echo "apps_shared_identity_id=$(terraform output -raw apps_shared_identity_id 2>/dev/null || echo 'N/A')" >> "$GITHUB_OUTPUT"

# AFTER:
echo "apps_shared_identity_id=$(terraform output -raw apps_shared_identity_id 2>/dev/null || echo '')" >> "$GITHUB_OUTPUT"
```

**Why:** Empty strings are falsy in GitHub expressions, enabling proper `A || B` fallback logic.

---

### Fix #2: Simplified Fallback Logic in Orchestrator
**File:** `.github/workflows/terraform-plan.yml`

**Changed:**
```yaml
# BEFORE:
apps_shared_identity_id: ${{ (needs.call_infrastructure.outputs.apps_shared_identity_id != '' && needs.call_infrastructure.outputs.apps_shared_identity_id != 'N/A' && needs.call_infrastructure.outputs.apps_shared_identity_id) || needs.get_infrastructure_info.outputs.apps_shared_identity_id }}

# AFTER:
apps_shared_identity_id: ${{ needs.call_infrastructure.outputs.apps_shared_identity_id || needs.get_infrastructure_info.outputs.apps_shared_identity_id }}
```

**Why:** Simple `A || B` pattern is more reliable and readable. Works because we fixed infrastructure.yml to output empty strings.

---

### Fix #3: Prevent Service Deployment During Plan
**File:** `.github/workflows/terraform-plan.yml`

**Added after workflow_mode decision:**
```bash
# Prevent service deployment when terraform_action is 'plan'
# Services require actual infrastructure to exist (not just planned)
if [ "$ACTION" = "plan" ] && [ "$RUN_SERVICES" = "true" ]; then
  echo "⚠️  WARNING: terraform_action=plan detected with service deployment requested"
  echo "⚠️  Service deployment requires 'apply' to ensure infrastructure exists"
  echo "⚠️  Setting RUN_SERVICES=false to prevent deployment failure"
  RUN_SERVICES="false"
fi
```

**Why:** Prevents the exact failure mode: attempting to deploy services when infrastructure is only planned (not applied).

---

### Fix #4: Persist Discovered UAMI ID as Job Output
**File:** `.github/workflows/build-and-deploy.yml`

**Changed:**
```yaml
# BEFORE:
preflight_checks:
  name: 'Pre-Flight Checks'
  runs-on: ubuntu-latest
  # No outputs!
  
  steps:
    - name: Verify or Discover Managed Identity
      shell: bash
      run: |
        UAMI_ID="$DISCOVERED_ID"
        echo "📋 Final UAMI ID: $UAMI_ID"
        # Value lost after this step!

# AFTER:
preflight_checks:
  name: 'Pre-Flight Checks'
  runs-on: ubuntu-latest
  
  outputs:
    uami_id: ${{ steps.uami.outputs.uami_id }}
  
  steps:
    - name: Verify or Discover Managed Identity
      id: uami  # Added step ID
      shell: bash
      run: |
        # ... discovery logic ...
        
        # Persist as job output
        echo "uami_id=$UAMI_ID" >> "$GITHUB_OUTPUT"
```

**Why:** Makes discovered UAMI ID available to all downstream jobs (build_and_push, deploy_apps).

---

### Fix #5: Correct Dockerfile Path for Hyphenated Services
**File:** `.github/workflows/build-and-deploy.yml`

**Changed:**
```yaml
# BEFORE:
- name: Determine service path
  id: paths
  run: |
    SERVICE="${{ matrix.service }}"
    case "$SERVICE" in
      ratingunderwriting)
        SERVICE_DIR="services/ratingandunderwriting"
        ;;
      *)
        SERVICE_DIR="services/$SERVICE"
        ;;
    esac

- name: Build API Image
  run: |
    docker build -f "$SERVICE_DIR/src/${SERVICE^}.Api/Dockerfile" ...
    # ${SERVICE^} = "File-retrieval" ❌ WRONG!

# AFTER:
- name: Determine service path and project prefix
  id: paths
  run: |
    SERVICE="${{ matrix.service }}"
    case "$SERVICE" in
      file-retrieval)
        SERVICE_DIR="platform/fileintegration"
        PROJECT_PREFIX="FileRetrieval"
        ;;
      fundstransfermgt)
        SERVICE_DIR="services/fundstransfermgt"
        PROJECT_PREFIX="FundsTransferMgt"
        ;;
      # ... all services mapped ...
    esac

- name: Build API Image
  run: |
    PROJECT_PREFIX="${{ steps.paths.outputs.project_prefix }}"
    docker build -f "$SERVICE_DIR/src/${PROJECT_PREFIX}.Api/Dockerfile" ...
    # Uses correct "FileRetrieval" ✅
```

**Why:** Explicit mapping ensures correct .NET project folder names for all services, including hyphenated ones.

---

### Fix #6: Pass UAMI ID to Terraform Services Layer
**File:** `.github/workflows/build-and-deploy.yml`

**Changed:**
```yaml
# BEFORE:
deploy_apps:
  needs: [parse_services, build_and_push]  # Missing preflight_checks!
  
  steps:
    - name: Terraform Plan
      run: |
        terraform plan \
          -var="environment=${{ inputs.environment }}" \
          -var="image_tag=${{ github.sha }}"
          # Missing UAMI ID!

# AFTER:
deploy_apps:
  needs: [parse_services, build_and_push, preflight_checks]  # Added preflight_checks
  
  steps:
    - name: Terraform Plan
      run: |
        terraform plan \
          -var="environment=${{ inputs.environment }}" \
          -var="image_tag=${{ github.sha }}" \
          -var="apps_shared_identity_id=${{ needs.preflight_checks.outputs.uami_id }}"
```

**Why:** Terraform services layer receives validated/discovered UAMI ID consistently.

---

## Updated Condition Logic

### `get_infrastructure_info` Condition
**File:** `.github/workflows/terraform-plan.yml`

```yaml
# Simplified (no longer checking for 'N/A'):
if: |
  always() &&
  needs.determine_workflow.outputs.run_services == 'true' &&
  (needs.call_infrastructure.result == 'skipped' || 
   needs.call_infrastructure.outputs.apps_shared_identity_id == '')
```

**Triggers when:**
- Services should run AND
- Infrastructure was skipped OR infrastructure outputs are empty

---

## Workflow Execution Scenarios

### Scenario 1: `infrastructure-only` + `apply`
```yaml
workflow_mode: infrastructure-only
terraform_action: apply
```
- ✅ Runs infrastructure (foundation + shared-services)
- ✅ Creates UAMI: `riskinsure-dev-app-mi`
- ✅ Outputs populated with real values
- ⏭️ Skips services (RUN_SERVICES=false)

---

### Scenario 2: `infrastructure-only` + `plan`
```yaml
workflow_mode: infrastructure-only
terraform_action: plan
```
- ✅ Plans infrastructure changes
- ⚠️ Outputs empty (no apply yet)
- ⏭️ Skips services (RUN_SERVICES=false)

---

### Scenario 3: `deploy-services-only` + `apply`
```yaml
workflow_mode: deploy-services-only
terraform_action: apply
```
- ⏭️ Skips infrastructure (call_infrastructure not run)
- ✅ Triggers `get_infrastructure_info` (infra skipped)
- ✅ Discovers UAMI from Azure
- ✅ Builds and deploys all services

---

### Scenario 4: `full-deployment` + `plan` (OLD FAILURE MODE)
```yaml
workflow_mode: full-deployment
terraform_action: plan
```

**BEFORE FIX:**
- ✅ Plans infrastructure
- ⚠️ Outputs empty (no apply)
- ❌ Tries to deploy services → UAMI check fails!

**AFTER FIX:**
- ✅ Plans infrastructure
- ⚠️ Outputs empty (no apply)
- ✅ **Automatically sets RUN_SERVICES=false**
- ℹ️ Logs warning: "Service deployment requires 'apply'"

---

### Scenario 5: `full-deployment` + `apply`
```yaml
workflow_mode: full-deployment
terraform_action: apply
```
- ✅ Applies infrastructure
- ✅ UAMI created and output populated
- ✅ Services workflow receives UAMI ID
- ✅ Builds and deploys all services

---

## Testing Checklist

### First-Time Environment Setup
1. ✅ Run `infrastructure-only` + `apply` + `all`
2. ✅ Verify UAMI exists in Azure Portal
3. ✅ Run `deploy-services-only` + `apply`
4. ✅ All services deploy successfully

### Day-to-Day Service Releases
1. ✅ Use `deploy-services-only` + `apply`
2. ✅ Auto-discovery finds existing UAMI
3. ✅ Services build and deploy

### Validation Tests
1. ✅ Test `full-deployment` + `plan` (should skip services)
2. ✅ Test hyphenated service: `file-retrieval` (Dockerfile found)
3. ✅ Test all service name mappings work

---

## Key Takeaways

### GitHub Actions Patterns
- ✅ Use empty strings (not 'N/A') for falsy fallback values
- ✅ Always persist discovered values as job outputs via `$GITHUB_OUTPUT`
- ✅ Use simple `A || B` fallback when A is guaranteed empty/null/undefined

### Terraform + GitHub Actions
- ✅ Prevent logical inconsistencies (plan ≠ apply ≠ deploy)
- ✅ Pass validated infrastructure IDs to downstream Terraform layers
- ✅ Add auto-discovery as ultimate fallback for reusable workflows

### Service Name Handling
- ✅ Explicit case mapping > string manipulation for production code
- ✅ Map both directory paths AND .NET project prefixes
- ✅ Fail fast on unknown services

---

## Files Modified

1. `.github/workflows/infrastructure.yml` - Output empty strings instead of 'N/A'
2. `.github/workflows/terraform-plan.yml` - Simplified fallbacks, prevent plan+deploy
3. `.github/workflows/build-and-deploy.yml` - Persist UAMI output, fix Dockerfile paths, pass to Terraform

## Verification

To verify the fix works:

```bash
# 1. First time: Create infrastructure
# Run: infrastructure-only + apply + all

# 2. Deploy services
# Run: deploy-services-only + apply + all

# 3. Test plan mode doesn't attempt deploy
# Run: full-deployment + plan
# Expected: Infrastructure plans, services skipped with warning
```

---

## Additional Recommendations

### Terraform Services Layer
Ensure `platform/infra/services/variables.tf` includes:
```hcl
variable "apps_shared_identity_id" {
  description = "User-assigned managed identity resource ID"
  type        = string
}
```

### Terraform Outputs Validation
In `platform/infra/shared-services/outputs.tf`:
```hcl
output "apps_shared_identity_id" {
  description = "User-assigned managed identity resource ID"
  value       = azurerm_user_assigned_identity.apps_shared.id
}
```

---

**Author:** GitHub Copilot  
**Date:** February 25, 2026  
**Branch:** kovurum/fix-uami-fallback-logic
