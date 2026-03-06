# Quick Start - Testing New Workflows

**Current Branch**: `feature/new-workflow-structure`  
**Current State**: New workflows in `.github/workflows/new/` (INACTIVE)  
**Goal**: Test and migrate to new workflows

---

## ⚡ Quick Commands

### Step 1: Test PR Workflows (5 minutes, SAFE) ✅ START HERE

```bash
# Activate PR workflows temporarily
git checkout feature/new-workflow-structure
git mv .github/workflows/new/pr-terraform-validate.yml .github/workflows/
git mv .github/workflows/new/pr-unit-tests.yml .github/workflows/
git add .
git commit -m "test: activate PR workflows"
git push origin feature/new-workflow-structure

# Create test PR
git checkout -b test/pr-validation
echo "# Test" >> services/billing/README.md
git add . && git commit -m "test: trigger pr-unit-tests"
git push origin test/pr-validation
# Open PR on GitHub → Watch workflows run
```

**Expected**: Unit tests run, results posted ✅

### Step 2: Disable Old Workflows (REQUIRED before testing deployments)

```bash
# Disable old workflows to prevent conflicts
git checkout feature/new-workflow-structure
cd .github/workflows
git mv terraform-plan.yml terraform-plan.yml.disabled
git mv infrastructure.yml infrastructure.yml.disabled
git mv build-and-deploy.yml build-and-deploy.yml.disabled
git add .
git commit -m "test: disable old workflows"
git push origin feature/new-workflow-structure
```

**Why**: Prevents both old and new workflows from running simultaneously

### Step 3: Activate Deployment Workflows

```bash
# Move deployment workflows to active directory
git mv .github/workflows/new/cd-infra-dev.yml .github/workflows/
git mv .github/workflows/new/cd-services-dev.yml .github/workflows/
git mv .github/workflows/new/ci-test-integration.yml .github/workflows/
git mv .github/workflows/new/ci-build-services.yml .github/workflows/
git add .
git commit -m "test: activate deployment workflows"
git push origin feature/new-workflow-structure
```

### Step 4: Test Infrastructure (Manual Trigger)

```bash
# Go to GitHub: Actions → CD: Infrastructure Dev → Run workflow
# Select: branch=feature/new-workflow-structure, layer=foundation, action=plan
# Click "Run workflow"
# 
# If plan looks good:
# Select: branch=feature/new-workflow-structure, layer=foundation, action=apply
```

**Expected**: Foundation deployed, outputs shown ✅

### Step 5: Test Service Deployment (Manual Trigger)

```bash
# Go to GitHub: Actions → CD: Services Dev → Run workflow
# Select: branch=feature/new-workflow-structure, services=billing, skip_tests=false
# Click "Run workflow"
```

**Expected**: Infrastructure verified → Docker built → Service deployed ✅

### Step 6: Test Automatic Triggers

```bash
# Test auto-deploy on push
git checkout feature/new-workflow-structure
echo "# Trigger deployment" >> services/billing/README.md
git add . && git commit -m "test: auto-deploy"
git push origin feature/new-workflow-structure

# Watch GitHub Actions:
# 1. cd-services-dev.yml triggers
# 2. ci-build-services.yml triggers (parallel)
# 3. ci-test-integration.yml triggers (after services deploy)
```

**Expected**: All workflows run in sequence ✅

### Step 7: Merge to Main (After All Tests Pass)

```bash
# Final validation
git checkout feature/new-workflow-structure
git pull origin feature/new-workflow-structure

# Merge to main
git checkout main
git pull origin main
git merge feature/new-workflow-structure
git push origin main

# Archive legacy workflows
mkdir -p .github/workflows/legacy
git mv .github/workflows/*.disabled .github/workflows/legacy/
git add . && git commit -m "chore: archive legacy workflows"
git push origin main
```

---

## 🚨 Important Notes

### ⚠️ Key Differences from Old Workflows

| Aspect | Old Workflows | New Workflows |
|--------|--------------|---------------|
| **Location** | `.github/workflows/*.yml` | `.github/workflows/new/*.yml` (inactive) |
| **Activation** | Always active | Must move to `.github/workflows/` |
| **PR Checks** | ❌ None | ✅ Terraform + Unit Tests |
| **Infrastructure First** | ⚠️ Manual | ✅ Automatic |
| **Test Timing** | ❌ Before deploy | ✅ After deploy |

### 🎯 Testing Strategy

1. **Phase 1** (SAFE): Test PR workflows only
   - No deployments
   - No infrastructure changes
   - Can run alongside old workflows
   - **Recommendation**: Start here ✅

2. **Phase 2** (CAREFUL): Test deployments
   - ⚠️ Will deploy to dev environment
   - ⚠️ Must disable old workflows first
   - Test on feature branch only
   - Use manual triggers first

3. **Phase 3** (PRODUCTION): Merge to main
   - Only after all tests pass
   - Archive old workflows
   - Update branch protection rules
   - Train team on new workflows

---

## 🔍 How to Check Workflow Status

### Option 1: GitHub UI
```
1. Go to GitHub repository
2. Click "Actions" tab
3. See all workflow runs
4. Click run to see logs
```

### Option 2: GitHub CLI
```bash
# List recent runs
gh run list --limit 10

# View specific run
gh run view <run-id> --log

# Watch run in real-time
gh run watch <run-id>
```

### Option 3: VS Code Extension
```
1. Install "GitHub Actions" extension
2. View workflow runs in sidebar
3. Click to see logs inline
```

---

## 🐛 Common Issues

### Issue: New workflows not triggering
**Cause**: Still in `new/` folder  
**Fix**: `git mv .github/workflows/new/workflow.yml .github/workflows/`

### Issue: Both old and new workflows running
**Cause**: Both active simultaneously  
**Fix**: Disable old: `git mv terraform-plan.yml terraform-plan.yml.disabled`

### Issue: verify-infrastructure fails
**Cause**: Missing Service Bus queue  
**Fix**: Add `"riskinsure.fundstransfermgt.api"` to `servicebus.tf`

### Issue: Integration tests fail
**Cause**: Services not deployed yet  
**Fix**: Deploy services first: Run `cd-services-dev.yml`

---

## ✅ Success Checklist

Before merging to main:

- [ ] PR workflows tested (pr-terraform-validate, pr-unit-tests)
- [ ] Infrastructure deployment tested (cd-infra-dev)
- [ ] Service deployment tested (cd-services-dev)
- [ ] Integration tests tested (ci-test-integration)
- [ ] Build workflow tested (ci-build-services)
- [ ] Rollback workflow tested (ops-rollback)
- [ ] Automatic triggers tested (push events)
- [ ] Old workflows disabled
- [ ] No conflicts found
- [ ] Team reviewed and approved

---

## 📚 Full Documentation

- **Complete Testing Guide**: `.github/workflows/new/TESTING-GUIDE.md`
- **Workflow Overview**: `.github/workflows/new/README.md`
- **Implementation Summary**: `.github/workflows/new/IMPLEMENTATION-SUMMARY.md`

---

## 🎓 Key Concepts

### Why workflows in `new/` folder don't run:
GitHub Actions only recognizes workflows in **`.github/workflows/`** (root level). Files in subdirectories like `new/` are ignored.

### To activate a workflow:
```bash
git mv .github/workflows/new/workflow-name.yml .github/workflows/
```

### To deactivate a workflow:
```bash
# Option 1: Rename (keeps file)
git mv .github/workflows/workflow-name.yml .github/workflows/workflow-name.yml.disabled

# Option 2: Move to subfolder (organizes)
git mv .github/workflows/workflow-name.yml .github/workflows/legacy/

# Option 3: Delete (permanent)
git rm .github/workflows/workflow-name.yml
```

---

**Next Action**: Start with Step 1 (PR Workflows) ✅  
**Estimated Time**: 30 minutes for full testing  
**Risk Level**: 🟢 Low (old workflows can be re-enabled if needed)
