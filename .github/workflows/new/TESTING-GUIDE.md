# Testing Guide for New Workflows

**Branch**: `feature/new-workflow-structure`  
**Date**: March 2, 2026  
**Status**: Ready for Testing

---

## 🎯 Testing Strategy Overview

### Current Situation
- ✅ New workflows created in `.github/workflows/new/`
- ⚠️ Old workflows still active in `.github/workflows/`
- ❌ New workflows in `new/` folder **WILL NOT RUN** automatically

### Why New Workflows Won't Trigger Yet

GitHub Actions only recognizes workflows in these locations:
- ✅ `.github/workflows/*.yml` (active)
- ❌ `.github/workflows/new/*.yml` (inactive - treated as regular files)

**Solution**: We need to temporarily move/activate new workflows for testing.

---

## 📋 Testing Plan (3 Phases)

### Phase 1: Safe Testing (No Risk) ✅ START HERE
Test workflows that don't modify infrastructure

### Phase 2: Parallel Testing (Low Risk)
Run new workflows alongside old workflows

### Phase 3: Full Migration (Planned Cutover)
Disable old workflows, enable new workflows permanently

---

## Phase 1: Safe Testing (Recommended Start)

### Step 1.1: Test PR Workflows on Feature Branch

These workflows are **100% safe** - they only validate, never deploy.

```bash
# 1. Create test branch from current feature branch
git checkout feature/new-workflow-structure
git pull origin feature/new-workflow-structure

# 2. Move ONLY PR workflows to active directory (temporary)
git mv .github/workflows/new/pr-terraform-validate.yml .github/workflows/
git mv .github/workflows/new/pr-unit-tests.yml .github/workflows/

# 3. Commit and push
git add .
git commit -m "test: activate PR validation workflows for testing"
git push origin feature/new-workflow-structure

# 4. Create test PR to trigger workflows
git checkout -b test/pr-workflows-validation
echo "# Test PR workflows" >> services/billing/README.md
git add services/billing/README.md
git commit -m "test: trigger pr-unit-tests workflow"
git push origin test/pr-workflows-validation
```

**Expected Results**:
- ✅ `pr-unit-tests.yml` runs (billing service detected)
- ✅ Test results posted
- ✅ Coverage uploaded
- ⏭️ `pr-terraform-validate.yml` SKIPPED (no infra changes)

**Test Terraform Validation**:
```bash
# On same test branch
echo "# Test comment" >> platform/infra/foundation/main.tf
git add platform/infra/foundation/main.tf
git commit -m "test: trigger pr-terraform-validate workflow"
git push origin test/pr-workflows-validation
```

**Expected Results**:
- ✅ `pr-terraform-validate.yml` runs
- ✅ Terraform plan posted as PR comment
- ✅ Both workflows show in PR checks

### Step 1.2: Review and Validate

✅ **Success Criteria**:
- [ ] PR workflows triggered correctly
- [ ] Terraform plan posted as comment
- [ ] Unit tests ran and passed
- [ ] No errors in workflow logs
- [ ] Old workflows didn't interfere

❌ **If Issues Found**:
1. Check workflow logs
2. Fix issues in `new/` folder
3. Move workflows back: `git mv .github/workflows/pr-*.yml .github/workflows/new/`
4. Repeat from Step 1.1

### Step 1.3: Restore State (After Testing)

```bash
# Move PR workflows back to new/ folder
git checkout feature/new-workflow-structure
git mv .github/workflows/pr-terraform-validate.yml .github/workflows/new/
git mv .github/workflows/pr-unit-tests.yml .github/workflows/new/
git add .
git commit -m "test: move PR workflows back to new/ after successful testing"
git push origin feature/new-workflow-structure

# Close test PR (don't merge)
```

---

## Phase 2: Parallel Testing (Infrastructure & Services)

⚠️ **WARNING**: These workflows **WILL DEPLOY** to dev environment.

### Step 2.1: Prepare for Infrastructure Testing

**IMPORTANT**: Old workflows will ALSO trigger. We need to prevent conflicts.

**Option A: Disable Old Workflows Temporarily (RECOMMENDED)**

```bash
# Rename old workflows to .yml.disabled
git checkout feature/new-workflow-structure
cd .github/workflows

# Disable old workflows
git mv terraform-plan.yml terraform-plan.yml.disabled
git mv infrastructure.yml infrastructure.yml.disabled
git mv build-and-deploy.yml build-and-deploy.yml.disabled

# Commit
git add .
git commit -m "test: temporarily disable old workflows for new workflow testing"
git push origin feature/new-workflow-structure
```

**Option B: Use Workflow Conditions (Less Reliable)**
Add this to old workflows (not recommended - requires modifying old workflows):
```yaml
if: github.ref != 'refs/heads/feature/new-workflow-structure'
```

### Step 2.2: Activate New Infrastructure Workflows

```bash
# Move infrastructure workflows to active directory
git mv .github/workflows/new/cd-infra-dev.yml .github/workflows/
git mv .github/workflows/new/cd-services-dev.yml .github/workflows/
git mv .github/workflows/new/ci-test-integration.yml .github/workflows/
git mv .github/workflows/new/ci-build-services.yml .github/workflows/

# Commit and push
git add .
git commit -m "test: activate infrastructure deployment workflows"
git push origin feature/new-workflow-structure
```

### Step 2.3: Test Infrastructure Deployment (Manual Trigger)

```bash
# Go to GitHub Actions UI
# Navigate to: Actions → CD: Infrastructure Dev → Run workflow
# 
# Select:
#   - Branch: feature/new-workflow-structure
#   - layer: foundation
#   - action: plan
#
# Click "Run workflow"
```

**Expected Results**:
- ✅ Workflow runs successfully
- ✅ Terraform plan shows in logs
- ✅ No actual changes (action=plan)
- ✅ Job summary shows foundation layer planned

**If Plan Looks Good, Apply**:
```bash
# Run workflow again with:
#   - layer: foundation
#   - action: apply
```

### Step 2.4: Test Service Deployment (Manual Trigger)

⚠️ **Prerequisites**: 
- Infrastructure deployed (Step 2.3)
- Wait 3 minutes after infrastructure deployment

```bash
# Go to GitHub Actions UI
# Navigate to: Actions → CD: Services Dev → Run workflow
#
# Select:
#   - Branch: feature/new-workflow-structure
#   - services: billing
#   - skip_tests: false
#
# Click "Run workflow"
```

**Expected Results**:
- ✅ `verify-infrastructure` job passes
- ✅ Docker image built
- ✅ Image pushed to ACR
- ✅ Container App deployed
- ✅ Job summary shows deployment status

### Step 2.5: Test Integration Tests (Manual Trigger)

```bash
# Go to GitHub Actions UI
# Navigate to: Actions → CI: Integration Tests → Run workflow
#
# Select:
#   - Branch: feature/new-workflow-structure
#   - services: billing
#   - environment: dev
#
# Click "Run workflow"
```

**Expected Results**:
- ✅ `verify-infrastructure` job passes (checks queues, Cosmos, Container Apps)
- ✅ Integration tests run
- ✅ Test results uploaded
- ✅ Job summary shows test status

### Step 2.6: Test Automatic Triggers (Push to Branch)

```bash
# Test automatic infrastructure deployment
git checkout feature/new-workflow-structure
echo "# Trigger infra deployment" >> platform/infra/foundation/README.md
git add platform/infra/foundation/README.md
git commit -m "test: trigger cd-infra-dev automatic deployment"
git push origin feature/new-workflow-structure

# Watch GitHub Actions - cd-infra-dev.yml should trigger automatically
```

**Expected Results**:
- ✅ `cd-infra-dev.yml` triggers on push
- ✅ Foundation layer deploys
- ✅ Outputs exported

**Test automatic service deployment**:
```bash
# Make service change
echo "# Trigger service deployment" >> services/billing/README.md
git add services/billing/README.md
git commit -m "test: trigger cd-services-dev automatic deployment"
git push origin feature/new-workflow-structure

# Watch GitHub Actions
# Expected workflow sequence:
#   1. cd-services-dev.yml triggers
#   2. ci-build-services.yml triggers (parallel)
#   3. ci-test-integration.yml triggers (after cd-services-dev completes)
```

### Step 2.7: Test Rollback (Manual)

⚠️ **Only test after successful deployment**

```bash
# Get previous commit SHA
git log --oneline -n 5

# Go to GitHub Actions UI
# Navigate to: Actions → OPS: Rollback Service → Run workflow
#
# Inputs:
#   - service: billing
#   - environment: dev
#   - commit_sha: <previous-sha> (or leave empty for last revision)
#   - confirm: ROLLBACK
#
# Click "Run workflow"
```

**Expected Results**:
- ✅ Validation passes
- ✅ Target image verified in ACR
- ✅ Rollback applied
- ✅ Health check passes
- ✅ Job summary shows before/after images

---

## Phase 3: Full Migration to Main Branch

### Step 3.1: Validate All Tests Passed

**Checklist**:
- [ ] PR workflows tested and working
- [ ] Infrastructure deployment tested (manual + automatic)
- [ ] Service deployment tested (manual + automatic)
- [ ] Integration tests tested (manual + automatic)
- [ ] Build workflow tested
- [ ] Rollback workflow tested
- [ ] No conflicts with old workflows (old workflows disabled)
- [ ] Team reviewed and approved new structure

### Step 3.2: Merge to Main Branch

```bash
# Ensure all tests passed
git checkout feature/new-workflow-structure
git pull origin feature/new-workflow-structure

# Merge to main
git checkout main
git pull origin main
git merge feature/new-workflow-structure

# Push to main
git push origin main
```

### Step 3.3: Organize Old Workflows

```bash
# Create legacy folder
mkdir -p .github/workflows/legacy

# Move old workflows to legacy (they should already be .disabled)
git mv .github/workflows/terraform-plan.yml.disabled .github/workflows/legacy/
git mv .github/workflows/infrastructure.yml.disabled .github/workflows/legacy/
git mv .github/workflows/build-and-deploy.yml.disabled .github/workflows/legacy/
git mv .github/workflows/service-tests.yml .github/workflows/legacy/

# Keep new folder for documentation
# (workflows are already moved to .github/workflows/)

# Commit
git add .
git commit -m "chore: archive legacy workflows after successful migration"
git push origin main
```

### Step 3.4: Update Branch Protection Rules

```bash
# Go to GitHub Settings → Branches → Branch protection rules
# Edit rule for 'main' branch

# Add required status checks:
#   ✅ PR: Terraform Validate / terraform-validate (foundation)
#   ✅ PR: Terraform Validate / terraform-validate (shared-services)
#   ✅ Unit Tests: billing
#   ✅ Unit Tests: customer
#   ✅ Unit Tests: fundstransfermgt
#   ✅ Unit Tests: policy
#   ✅ Unit Tests: ratingandunderwriting

# Save changes
```

### Step 3.5: Update Team Documentation

Create announcement for team:

```markdown
# 🎉 New GitHub Actions Workflows Active

**Effective**: March 2, 2026

## What Changed?

We've migrated to industry-standard GitHub Actions workflows that:
- ✅ Run Terraform validation on PRs (with plan as comment)
- ✅ Run unit tests on PRs (fail-fast)
- ✅ Auto-deploy infrastructure changes to dev
- ✅ Auto-deploy service changes to dev
- ✅ Run integration tests after deployment
- ✅ Support emergency rollbacks

## Developer Workflow

### 1. Creating PRs
Your PRs will now automatically:
- Run unit tests (if you changed services)
- Run Terraform validation (if you changed infra)
- Show results as PR comments and checks

**Required**: All checks must pass before merge.

### 2. After Merge to Main
Automatic deployment sequence:
1. Infrastructure deploys (if changed)
2. Services deploy (if changed)
3. Integration tests run

**No manual action required** ✅

### 3. Emergency Rollback
If production issue occurs:
- Go to Actions → OPS: Rollback Service
- Select service and environment
- Type "ROLLBACK" to confirm
- Workflow rolls back to previous version

## Documentation
- Workflow Guide: `.github/workflows/new/README.md`
- Testing Guide: `.github/workflows/new/TESTING-GUIDE.md`
- Migration Summary: `.github/workflows/new/IMPLEMENTATION-SUMMARY.md`

## Questions?
Contact: [Your Name/Team]
```

---

## 🔍 Monitoring New Workflows

### Week 1: Close Monitoring
- [ ] Check all workflow runs daily
- [ ] Monitor deployment success rate
- [ ] Check integration test pass rate
- [ ] Review any failed workflows immediately

### Week 2-4: Normal Monitoring
- [ ] Review workflow runs weekly
- [ ] Check for patterns in failures
- [ ] Optimize slow workflows if needed

### After 30 Days: Delete Legacy Workflows
```bash
# If no issues found, delete legacy folder
git rm -r .github/workflows/legacy/
git commit -m "chore: remove legacy workflows after 30-day validation period"
git push origin main
```

---

## 🐛 Troubleshooting

### Issue: New Workflows Not Triggering

**Cause**: Workflows in `new/` folder aren't recognized by GitHub Actions

**Solution**: Move to `.github/workflows/` directory
```bash
git mv .github/workflows/new/workflow-name.yml .github/workflows/
```

### Issue: Both Old and New Workflows Running

**Cause**: Both sets of workflows active at same time

**Solution**: Disable old workflows
```bash
git mv .github/workflows/terraform-plan.yml .github/workflows/terraform-plan.yml.disabled
```

### Issue: verify-infrastructure Job Fails

**Cause**: Infrastructure not deployed or Service Bus queues missing

**Solution**: 
1. Check if infrastructure deployed: Go to Azure Portal → Resource Group
2. If missing queue (`riskinsure.fundstransfermgt.api`), add to `servicebus.tf`:
```terraform
queue_names = [
  # ... existing queues ...
  "riskinsure.fundstransfermgt.api",
]
```
3. Deploy infrastructure: Run `cd-infra-dev.yml` with `layer=shared-services`

### Issue: Integration Tests Fail with "Service Not Found"

**Cause**: Container Apps not deployed yet

**Solution**: 
1. Check Container Apps status: `az containerapp list --resource-group CAIS-010-RiskInsure`
2. Deploy services: Run `cd-services-dev.yml` with `services=all`
3. Wait 2-3 minutes for apps to start
4. Re-run integration tests

### Issue: Rollback Fails with "Image Not Found"

**Cause**: Target commit SHA doesn't have Docker image in ACR

**Solution**:
1. Check available tags: `az acr repository show-tags --name <acr-name> --repository <service>`
2. Use a SHA that has an image (from recent deployment)
3. Or leave `commit_sha` empty to use previous revision

---

## 📊 Testing Checklist (Complete Before Migration)

### PR Workflows
- [ ] `pr-terraform-validate.yml` triggered on infra PR
- [ ] Terraform plan posted as PR comment
- [ ] `pr-unit-tests.yml` triggered on service PR
- [ ] Unit tests ran and passed
- [ ] Test results uploaded as artifacts
- [ ] Coverage reports generated

### Infrastructure Deployment
- [ ] `cd-infra-dev.yml` triggered manually (workflow_dispatch)
- [ ] Foundation layer deployed successfully
- [ ] Shared-services layer deployed successfully
- [ ] RBAC propagation wait (180s) completed
- [ ] Outputs exported correctly
- [ ] `cd-infra-dev.yml` triggered automatically on push

### Service Deployment
- [ ] `verify-infrastructure` job passed
- [ ] Changed services detected via git diff
- [ ] Docker images built successfully
- [ ] Images pushed to ACR with correct tags
- [ ] Container Apps deployed via Terraform
- [ ] `cd-services-dev.yml` triggered automatically on push
- [ ] `cd-services-dev.yml` triggered via workflow_run after infra

### Integration Tests
- [ ] `verify-infrastructure` job passed (queues, Cosmos, Container Apps)
- [ ] Integration tests ran successfully
- [ ] Test results uploaded
- [ ] Playwright traces available on failure
- [ ] `ci-test-integration.yml` triggered via workflow_run after services

### Build Workflow
- [ ] `ci-build-services.yml` detected changed services
- [ ] Docker images built
- [ ] SBOM generated
- [ ] Trivy vulnerability scan completed
- [ ] Results uploaded to Security tab

### Rollback Workflow
- [ ] Confirmation required (typed "ROLLBACK")
- [ ] Target image verified in ACR
- [ ] Rollback applied successfully
- [ ] Container App updated to old image
- [ ] Health check passed

---

## ✅ Success Criteria

Before declaring migration complete:

1. **All workflows tested** ✅
2. **No critical bugs found** ✅
3. **Team trained on new workflows** ✅
4. **Documentation updated** ✅
5. **Branch protection rules updated** ✅
6. **At least 5 successful deployments** ✅
7. **Integration tests consistently passing** ✅
8. **Rollback tested and working** ✅

---

## 📞 Support During Migration

### Quick Commands

**Check workflow status**:
```bash
gh run list --branch feature/new-workflow-structure --limit 10
```

**View workflow logs**:
```bash
gh run view <run-id> --log
```

**Cancel stuck workflow**:
```bash
gh run cancel <run-id>
```

**Re-run failed workflow**:
```bash
gh run rerun <run-id>
```

---

**Last Updated**: March 2, 2026  
**Branch**: `feature/new-workflow-structure`  
**Status**: Ready for Phase 1 Testing ✅
