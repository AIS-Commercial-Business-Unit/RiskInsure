# Testing & Migration FAQ

**Your Questions Answered**

---

## Q1: How can I test these workflows?

### Answer: 3-Phase Approach

#### **Phase 1: Test PR Workflows (SAFEST - Start Here)**

These workflows only validate code, they never deploy anything.

```bash
# 1. Move PR workflows out of new/ folder to activate them
git checkout feature/new-workflow-structure
git mv .github/workflows/new/pr-terraform-validate.yml .github/workflows/
git mv .github/workflows/new/pr-unit-tests.yml .github/workflows/
git add . && git commit -m "test: activate PR workflows"
git push origin feature/new-workflow-structure

# 2. Create a test PR
git checkout -b test/pr-workflows
echo "# Test" >> services/billing/README.md
git add . && git commit -m "test: trigger unit tests"
git push origin test/pr-workflows

# 3. Open PR on GitHub and watch workflows run
# Expected: pr-unit-tests.yml runs, posts results
```

✅ **Safe to test**: Won't affect infrastructure or deployments  
⏱️ **Time**: 5-10 minutes  
🎯 **What you'll see**: Unit tests run, Terraform plan posted as PR comment

#### **Phase 2: Test Deployment Workflows (Requires Preparation)**

⚠️ These workflows **WILL DEPLOY** to dev environment.

**CRITICAL STEP: Disable old workflows first to prevent conflicts**

```bash
# Disable old workflows
git checkout feature/new-workflow-structure
cd .github/workflows
git mv terraform-plan.yml terraform-plan.yml.disabled
git mv infrastructure.yml infrastructure.yml.disabled
git mv build-and-deploy.yml build-and-deploy.yml.disabled
git add . && git commit -m "test: disable old workflows"
git push origin feature/new-workflow-structure

# Activate new deployment workflows
git mv new/cd-infra-dev.yml .
git mv new/cd-services-dev.yml .
git mv new/ci-test-integration.yml .
git mv new/ci-build-services.yml .
git add . && git commit -m "test: activate deployment workflows"
git push origin feature/new-workflow-structure
```

**Test with manual triggers first (safer)**:
```
GitHub UI → Actions → CD: Infrastructure Dev → Run workflow
- Branch: feature/new-workflow-structure
- Layer: foundation
- Action: plan (see what would change without applying)

If plan looks good:
- Action: apply (actually deploy)
```

⏱️ **Time**: 30-45 minutes for full test  
🎯 **What you'll see**: Infrastructure deployed → Services deployed → Tests run

#### **Phase 3: Test Automatic Triggers**

After manual testing succeeds, test automatic triggers:

```bash
# Test infrastructure auto-deployment
git checkout feature/new-workflow-structure
echo "# Trigger" >> platform/infra/foundation/README.md
git add . && git commit -m "test: auto-deploy infrastructure"
git push origin feature/new-workflow-structure
# Watch: cd-infra-dev.yml triggers automatically

# Test service auto-deployment
echo "# Trigger" >> services/billing/README.md
git add . && git commit -m "test: auto-deploy service"
git push origin feature/new-workflow-structure
# Watch: cd-services-dev.yml → ci-build-services.yml → ci-test-integration.yml
```

⏱️ **Time**: 20-30 minutes per test  
🎯 **What you'll see**: Full deployment pipeline runs automatically

---

## Q2: If any changes to the infrastructure trigger the new workflows?

### Answer: New Workflows Don't Run Until You Move Them

**Current State**:
- ❌ New workflows in `.github/workflows/new/` are **INACTIVE**
- ✅ Old workflows in `.github/workflows/` are **ACTIVE**

**Why?** GitHub Actions only recognizes workflows in `.github/workflows/` root directory.

### To Activate New Workflows:

```bash
# Move new workflows to active directory
git mv .github/workflows/new/cd-infra-dev.yml .github/workflows/
# Now this workflow will trigger on infrastructure changes
```

### Trigger Conditions for New Workflows:

#### **cd-infra-dev.yml** triggers when:
- ✅ Push to main with changes in `platform/infra/**`
- ✅ Manual workflow_dispatch

Example:
```bash
# This WILL trigger cd-infra-dev.yml (after you activate it)
git checkout main
echo "# Change" >> platform/infra/foundation/main.tf
git add . && git commit -m "feat: update foundation"
git push origin main
```

#### **cd-services-dev.yml** triggers when:
- ✅ Push to main with changes in `services/**`
- ✅ After cd-infra-dev.yml completes (workflow_run)
- ✅ Manual workflow_dispatch

#### **ci-test-integration.yml** triggers when:
- ✅ After cd-services-dev.yml completes (workflow_run)
- ✅ Manual workflow_dispatch

---

## Q3: How about the old workflows?

### Answer: Old Workflows Will Keep Running Until You Disable Them

**Current State**:
- ✅ `terraform-plan.yml` - **ACTIVE** (will run on infrastructure changes)
- ✅ `infrastructure.yml` - **ACTIVE** (called by terraform-plan.yml)
- ✅ `build-and-deploy.yml` - **ACTIVE** (called by terraform-plan.yml)
- ✅ `service-tests.yml` - **ACTIVE** (called by terraform-plan.yml)

### Problem: Both Old and New Workflows Will Run Simultaneously

If you activate new workflows WITHOUT disabling old workflows:
```
Push to main with service change:
❌ terraform-plan.yml runs (old)
❌ cd-services-dev.yml runs (new)
❌ CONFLICT: Both try to deploy at the same time
```

### Solution: Disable Old Workflows During Testing

**Option 1: Rename to .disabled (Recommended)**
```bash
git mv .github/workflows/terraform-plan.yml .github/workflows/terraform-plan.yml.disabled
git mv .github/workflows/infrastructure.yml .github/workflows/infrastructure.yml.disabled
git mv .github/workflows/build-and-deploy.yml .github/workflows/build-and-deploy.yml.disabled
git mv .github/workflows/service-tests.yml .github/workflows/service-tests.yml.disabled
```

**Option 2: Move to legacy/ folder**
```bash
mkdir -p .github/workflows/legacy
git mv .github/workflows/terraform-plan.yml .github/workflows/legacy/
git mv .github/workflows/infrastructure.yml .github/workflows/legacy/
git mv .github/workflows/build-and-deploy.yml .github/workflows/legacy/
git mv .github/workflows/service-tests.yml .github/workflows/legacy/
```

**Can you re-enable old workflows if needed?** YES!
```bash
# If new workflows have issues, revert:
git mv .github/workflows/terraform-plan.yml.disabled .github/workflows/terraform-plan.yml
# Old workflows active again
```

---

## Q4: I want to test all the new workflows and if everything works as expected then I want to stop using old workflows and ask team to use new workflows. How can we accomplish this?

### Answer: Follow This Proven Migration Strategy

### **Step-by-Step Migration Plan**

#### **Step 1: Test PR Workflows (1 day)**

```bash
# Day 1: Test PR validation (safe, no deployments)
git checkout feature/new-workflow-structure
git mv .github/workflows/new/pr-*.yml .github/workflows/
git add . && git commit -m "test: activate PR workflows"
git push origin feature/new-workflow-structure

# Create test PR
git checkout -b test/pr-workflows
echo "# Test" >> services/billing/README.md
git add . && git commit -m "test: PR workflows"
git push origin test/pr-workflows
# Open PR, watch workflows

# ✅ Success criteria:
# - Unit tests run and pass
# - Terraform plan posted as comment
# - No errors in logs
```

#### **Step 2: Disable Old Workflows (5 minutes)**

```bash
# CRITICAL: Do this before testing deployment workflows
git checkout feature/new-workflow-structure
cd .github/workflows
git mv terraform-plan.yml terraform-plan.yml.disabled
git mv infrastructure.yml infrastructure.yml.disabled
git mv build-and-deploy.yml build-and-deploy.yml.disabled
git mv service-tests.yml service-tests.yml.disabled
git add . && git commit -m "test: disable old workflows during testing"
git push origin feature/new-workflow-structure
```

#### **Step 3: Test Deployment Workflows (1-2 days)**

```bash
# Activate deployment workflows
git mv .github/workflows/new/cd-*.yml .github/workflows/
git mv .github/workflows/new/ci-*.yml .github/workflows/
git mv .github/workflows/new/ops-*.yml .github/workflows/
git add . && git commit -m "test: activate all new workflows"
git push origin feature/new-workflow-structure

# Test manually first (safer)
# GitHub → Actions → CD: Infrastructure Dev → Run workflow
# - layer: foundation, action: plan
# Review plan, then action: apply

# Test automatically
echo "# Test" >> services/billing/README.md
git add . && git commit -m "test: automatic deployment"
git push origin feature/new-workflow-structure
# Watch all workflows run in sequence

# ✅ Success criteria:
# - Infrastructure deploys successfully
# - Services deploy successfully
# - Integration tests pass
# - No conflicts with old workflows
# - Rollback workflow tested and working
```

#### **Step 4: Merge to Main (1 hour)**

```bash
# After ALL tests pass
git checkout main
git merge feature/new-workflow-structure
git push origin main

# Archive old workflows permanently
mkdir -p .github/workflows/legacy
git mv .github/workflows/*.disabled .github/workflows/legacy/
git add . && git commit -m "chore: archive legacy workflows"
git push origin main
```

#### **Step 5: Update Branch Protection & Notify Team (1 day)**

**A. Update Branch Protection Rules**:
```
GitHub → Settings → Branches → Edit 'main' protection rule

Add required checks:
✅ PR: Terraform Validate / terraform-validate (foundation)
✅ PR: Terraform Validate / terraform-validate (shared-services)
✅ Unit Tests: billing
✅ Unit Tests: customer
✅ Unit Tests: fundstransfermgt
✅ Unit Tests: policy
✅ Unit Tests: ratingandunderwriting

Save changes
```

**B. Notify Team**:
```markdown
Subject: 🎉 New GitHub Actions Workflows Now Active

Hi Team,

We've successfully migrated to new industry-standard workflows.

Key Changes:
✅ PRs now run Terraform validation and unit tests automatically
✅ Infrastructure deploys automatically on merge to main
✅ Services deploy automatically after infrastructure
✅ Integration tests run after deployment
✅ Emergency rollback available via GitHub Actions UI

What You Need to Do:
1. Read the guide: .github/workflows/new/README.md
2. PRs will now require all checks to pass before merge
3. No manual deployment steps needed after merge
4. For emergency rollback: Actions → OPS: Rollback Service

Questions? Contact [Your Name]
```

#### **Step 6: Monitor (30 days)**

```bash
# Week 1: Daily monitoring
- Check all workflow runs
- Fix any issues immediately
- Document any problems

# Week 2-4: Weekly monitoring
- Review workflow success rate
- Optimize slow workflows if needed

# After 30 days: Delete legacy workflows
git rm -r .github/workflows/legacy/
git commit -m "chore: remove legacy workflows after 30-day validation"
git push origin main
```

---

## 📊 Timeline Summary

| Phase | Duration | Actions | Risk |
|-------|----------|---------|------|
| PR Workflows Test | 1 day | Test validation only | 🟢 None |
| Disable Old Workflows | 5 min | Prevent conflicts | 🟢 Low (reversible) |
| Deployment Test | 1-2 days | Test on feature branch | 🟡 Medium (dev only) |
| Merge to Main | 1 hour | Production cutover | 🟡 Medium (reversible) |
| Team Training | 1 day | Documentation + Q&A | 🟢 None |
| Monitoring | 30 days | Watch for issues | 🟢 None |

**Total Time**: ~1 week active work + 30 days monitoring

---

## ✅ Complete Testing Checklist

Use this checklist to ensure everything works before going to production:

### PR Workflows
- [ ] `pr-terraform-validate.yml` triggered on infra PR
- [ ] Terraform plan posted as PR comment
- [ ] Plan output is readable and accurate
- [ ] `pr-unit-tests.yml` triggered on service PR
- [ ] Unit tests ran for correct services
- [ ] Test results uploaded
- [ ] Coverage reports generated
- [ ] No errors in workflow logs

### Infrastructure Deployment
- [ ] Old workflows disabled (renamed to .disabled)
- [ ] `cd-infra-dev.yml` triggered manually (plan first)
- [ ] Plan reviewed and looks correct
- [ ] `cd-infra-dev.yml` applied successfully
- [ ] Foundation layer deployed
- [ ] Shared-services layer deployed
- [ ] RBAC propagation wait completed (180s)
- [ ] Outputs exported (ACR, UAMI, etc.)
- [ ] `cd-infra-dev.yml` triggered automatically on push

### Service Deployment
- [ ] `verify-infrastructure` job passed
- [ ] ACR, UAMI, Service Bus, Cosmos DB verified
- [ ] Changed services detected correctly via git diff
- [ ] Docker images built successfully
- [ ] Images pushed to ACR with all tags (sha, short, latest)
- [ ] Container Apps deployed via Terraform
- [ ] Container Apps showing "Running" status
- [ ] `cd-services-dev.yml` triggered automatically on push
- [ ] `cd-services-dev.yml` triggered via workflow_run after infra

### Integration Tests
- [ ] `verify-infrastructure` job passed
- [ ] Service Bus queues verified (including fundstransfermgt.api)
- [ ] Cosmos DB verified
- [ ] All Container Apps in "Running" state verified
- [ ] Integration tests ran successfully
- [ ] Service URLs resolved correctly
- [ ] Test results uploaded
- [ ] Playwright traces available on failure
- [ ] `ci-test-integration.yml` triggered via workflow_run

### Build Workflow
- [ ] `ci-build-services.yml` detected changed services
- [ ] Docker images built
- [ ] Multi-tag strategy worked (sha, short, latest)
- [ ] SBOM generated
- [ ] Trivy vulnerability scan completed
- [ ] Results uploaded to GitHub Security tab

### Rollback Workflow
- [ ] Confirmation required (must type "ROLLBACK")
- [ ] Target image verified exists in ACR
- [ ] Rollback applied successfully
- [ ] Container App updated to old image
- [ ] Running status verified
- [ ] Health check passed

### Team Readiness
- [ ] Documentation reviewed by team
- [ ] Branch protection rules updated
- [ ] Team members understand new PR process
- [ ] Team knows how to use rollback workflow
- [ ] Emergency contacts established

---

## 🚨 Important Reminders

1. **Workflows in `new/` folder DON'T RUN** - Must move to `.github/workflows/`
2. **Disable old workflows BEFORE testing deployments** - Prevents conflicts
3. **Test on feature branch first** - Don't test directly on main
4. **Old workflows can be re-enabled** - Migration is reversible
5. **Use manual triggers first** - Safer than automatic triggers
6. **Monitor closely for first week** - Catch issues early

---

## 📚 Documentation Links

- **Quick Start**: `.github/workflows/new/QUICK-START.md`
- **Detailed Testing Guide**: `.github/workflows/new/TESTING-GUIDE.md`
- **Workflow Overview**: `.github/workflows/new/README.md`
- **Implementation Summary**: `.github/workflows/new/IMPLEMENTATION-SUMMARY.md`

---

**Ready to Start?** → Go to `.github/workflows/new/QUICK-START.md`  
**Questions?** → Review this FAQ again or check detailed guides  
**Need Help?** → Check troubleshooting section in TESTING-GUIDE.md
