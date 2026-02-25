# RiskInsure CI/CD Workflows - Complete Guide

**Last Updated**: February 24, 2026  
**Branch**: kovurum/fix-uami-naming  
**Status**: ✅ Production-Ready

---

## Table of Contents

1. [Overview](#overview)
2. [Workflow Architecture](#workflow-architecture)
3. [What We Fixed](#what-we-fixed)
4. [What These Workflows Accomplish](#what-these-workflows-accomplish)
5. [How to Use the Workflows](#how-to-use-the-workflows)
6. [Workflow Parameters Explained](#workflow-parameters-explained)
7. [Terraform Layers](#terraform-layers)
8. [Deployment Scenarios](#deployment-scenarios)
9. [Troubleshooting](#troubleshooting)

---

## Overview

This repository contains three GitHub Actions workflows that automate the deployment of the RiskInsure insurance platform to Azure. The platform is an event-driven microservices architecture built with .NET 10, NServiceBus, Azure Cosmos DB, and Azure Container Apps.

### The Three Workflows

1. **terraform-plan.yml** - Main orchestrator that determines what to deploy
2. **infrastructure.yml** - Provisions Azure infrastructure (ACR, VNet, Cosmos DB, Service Bus, etc.)
3. **build-and-deploy.yml** - Builds Docker images and deploys Container Apps

---

## Workflow Architecture

### Design Philosophy

The workflows are designed with **separation of concerns** in mind:

- **Infrastructure changes infrequently** - Deploy once, update rarely
- **Services change frequently** - Deploy often during development
- **Granular control** - Deploy everything, specific layers, or specific services

### Workflow Flow

```
terraform-plan.yml (Orchestrator)
├── determine_workflow (decides what to run)
├── debug_outputs (shows all parameters)
├── call_infrastructure (if infrastructure needed)
│   └── infrastructure.yml
│       ├── foundation (ACR, VNet, KeyVault, Log Analytics)
│       ├── shared_services (Cosmos DB, Service Bus, UAMI)
│       └── role_propagation_wait (180s for RBAC)
├── get_infrastructure_info (fetch existing infrastructure if needed)
└── call_build_deploy (if services needed)
    └── build-and-deploy.yml
        ├── preflight_checks (verify UAMI exists)
        ├── parse_services (convert to JSON matrix)
        ├── build_and_push (Docker build in parallel)
        ├── deploy_apps (Terraform apply for Container Apps)
        └── health_check (verify deployments)
```

### Key Design Decisions

**✅ Underscored Job IDs**: All job IDs use underscores (e.g., `call_infrastructure`) instead of hyphens because GitHub Actions parses `needs.get-infrastructure-info` as mathematical subtraction, causing workflow failures.

**✅ Reusable Workflows**: `infrastructure.yml` and `build-and-deploy.yml` are reusable, allowing them to be called from other workflows or manually triggered.

**✅ Conditional Execution**: Jobs run conditionally based on workflow mode, allowing infrastructure-only, services-only, or full deployments.

**✅ Fallback Logic**: `get_infrastructure_info` job fetches existing infrastructure when running services-only mode, eliminating the need to redeploy infrastructure every time.

---

## What We Fixed

### Critical Bugs Resolved

#### Bug #1: services-environment Option (Removed)
**Problem**: The `terraform_layer` dropdown offered a `services-environment` option, but no job in `infrastructure.yml` handled this layer. Selecting it would cause the workflow to skip infrastructure deployment silently.

**Fix**: Removed `services-environment` from the options in `terraform-plan.yml` line 48.

**Impact**: Users can now only select valid layers: `all`, `foundation`, `shared-services`.

---

#### Bug #2: Condition String Mismatch (Fixed)
**Problem**: The condition in `infrastructure.yml` line 131 checked for `inputs.terraform_layer == 'shared_services'` (underscore), but users selected `'shared-services'` (hyphen) from the dropdown. The strings didn't match, causing the `shared_services` job to skip silently.

**Before**:
```yaml
if: inputs.terraform_layer == 'shared_services'  # ❌ Never matches user input
```

**After**:
```yaml
if: inputs.terraform_layer == 'shared-services'  # ✅ Matches user input
```

**Impact**: The shared-services layer now deploys correctly when selected.

---

#### Bug #3: Directory Path Mismatch (Fixed)
**Problem**: The `shared_services` job used `working-directory: platform/infra/shared_services` (underscore), but the actual directory is `platform/infra/shared-services` (hyphen). This would cause Terraform to fail with "directory not found" errors.

**Before**:
```yaml
working-directory: platform/infra/shared_services  # ❌ Directory doesn't exist
```

**After**:
```yaml
working-directory: platform/infra/shared-services  # ✅ Correct directory
```

**Impact**: Terraform can now find and execute the shared-services configuration.

---

#### Bug #4: Missing Policy Service (Added)
**Problem**: The `policy` service exists in the repository with complete code, tests, and documentation, but was not included in the deployment workflow. When users selected "all" services, only 5 out of 6 services were deployed.

**Before**:
```bash
SERVICES_INPUT="billing,customer,file-retrieval,fundstransfermgt,ratingunderwriting"  # 5 services
```

**After**:
```bash
SERVICES_INPUT="billing,customer,file-retrieval,fundstransfermgt,policy,ratingunderwriting"  # 6 services
```

**Impact**: All 6 microservices now deploy when "all" is selected.

---

### Earlier Fixes (Already Applied)

#### Bug #5: Hyphenated Job IDs (Fixed Previously)
**Problem**: Job IDs with hyphens (e.g., `get-infrastructure-info`) caused GitHub Actions to parse `needs.get-infrastructure-info` as subtraction: `needs.get - infrastructure - info`, resulting in syntax errors.

**Fix**: Renamed all 12 job IDs to use underscores (~70+ references updated across all workflows).

**Impact**: All job dependencies now resolve correctly.

---

#### Bug #6: UAMI Naming Pattern (Fixed Previously)
**Problem**: Workflows used inconsistent naming patterns for the User-Assigned Managed Identity (UAMI), causing infrastructure discovery to fail during services-only deployments.

**Fix**: Standardized UAMI naming to `riskinsure-${env}-app-mi` throughout all workflows.

**Impact**: Workflows can now reliably discover existing infrastructure.

---

## What These Workflows Accomplish

### Infrastructure Provisioning

The workflows deploy a complete Azure infrastructure using Terraform across three layers:

#### Foundation Layer
Creates the foundational resources needed by all services:
- **Resource Group**: CAIS-010-RiskInsure
- **Virtual Network**: Isolated network for Container Apps
- **Azure Container Registry**: riskinsure${env}acr (stores Docker images)
- **Key Vault**: riskinsure-${env}-kv (stores secrets)
- **Log Analytics Workspace**: Centralized logging
- **Application Insights**: Application performance monitoring
- **Network Security Group**: Network traffic rules
- **Storage Account**: Blob storage for various needs

**Terraform State**: `foundation.tfstate`

---

#### Shared Services Layer
Creates shared infrastructure used by multiple services:
- **Azure Cosmos DB**: NoSQL database (single partition per service)
- **Azure Service Bus**: Premium tier messaging for NServiceBus
- **User-Assigned Managed Identity (UAMI)**: riskinsure-${env}-app-mi
- **RBAC Role Assignments**:
  - ACR Pull (for Container Apps to pull images)
  - Cosmos DB Data Contributor (passwordless database access)
  - Service Bus Data Owner (passwordless messaging)
  - Key Vault Secrets User (passwordless secret access)

**Terraform State**: `shared-services.tfstate` (note: hyphen, not underscore)

---

#### Services Layer
Creates Container Apps for each microservice:
- **Container Apps Environment**: KEDA-based autoscaling environment
- **12 Container Apps** (6 services × 2 containers each):
  - `{service}-api`: REST API container
  - `{service}-endpoint`: NServiceBus message handler container

**Terraform State**: `services.tfstate`

---

### Microservices Deployment

The workflows build and deploy 6 microservices:

1. **billing** - Billing management and invoicing
2. **customer** - Customer data and profile management
3. **file-retrieval** - Document and file retrieval service
4. **fundstransfermgt** - Funds transfer management
5. **policy** - Policy lifecycle management (quote → issue → cancel → reinstate)
6. **ratingunderwriting** - Rating calculations and underwriting decisions

Each service is packaged as 2 Docker containers:
- **API Container**: Handles HTTP REST API requests
- **Endpoint Container**: Processes NServiceBus messages (event-driven)

---

### CI/CD Capabilities

**✅ Infrastructure-Only Deployments**: Deploy infrastructure without touching services (useful for infrastructure updates or initial setup)

**✅ Services-Only Deployments**: Deploy services without redeploying infrastructure (useful for frequent application updates)

**✅ Full Deployments**: Deploy both infrastructure and services in one workflow run

**✅ Granular Service Control**: Deploy all services or specific services (e.g., only `billing,customer`)

**✅ Layer Selection**: Deploy all infrastructure layers or specific layers (e.g., only `shared-services`)

**✅ Plan/Apply Separation**: Review Terraform plans before applying changes

**✅ Multi-Environment**: Support for dev, staging, and prod environments

**✅ Parallel Builds**: Build multiple services simultaneously using GitHub Actions matrix strategy

**✅ Health Checks**: Verify Container Apps are running after deployment

**✅ Passwordless Authentication**: OIDC for GitHub Actions, Managed Identity for Container Apps

---

## How to Use the Workflows

### Accessing the Workflow

1. Go to your GitHub repository
2. Click the **Actions** tab
3. Select **Terraform - Main Pipeline** from the left sidebar
4. Click **Run workflow** button (top right)

---

### Workflow Parameters

You'll see 5 input fields:

#### 1. workflow_mode (Required)
**What to run**

- `infrastructure-only` - Deploy only Azure infrastructure
- `deploy-services-only` - Deploy only services (assumes infrastructure exists)
- `full-deployment` - Deploy both infrastructure and services

#### 2. environment (Required)
**Target environment**

- `dev` - Development environment
- `staging` - Staging/QA environment
- `prod` - Production environment

#### 3. terraform_layer (Required)
**Infrastructure layer - only used in infrastructure-only mode**

- `all` - Deploy all infrastructure layers (foundation + shared-services)
- `foundation` - Deploy only foundation layer (ACR, VNet, KeyVault, etc.)
- `shared-services` - Deploy only shared-services layer (Cosmos DB, Service Bus, UAMI)

*Note: This parameter is ignored in `deploy-services-only` and `full-deployment` modes*

#### 4. services_to_deploy (Required)
**Services to deploy**

- `all` - Deploy all 6 services (billing, customer, file-retrieval, fundstransfermgt, policy, ratingunderwriting)
- `billing` - Deploy only billing service
- `billing,customer` - Deploy specific comma-separated services

*Note: This parameter is ignored in `infrastructure-only` mode*

#### 5. terraform_action (Required)
**Terraform action**

- `plan` - Show what would be deployed without making changes (dry run)
- `apply` - Actually deploy the changes

---

## Workflow Parameters Explained

### When to Use Each Workflow Mode

#### infrastructure-only
**Use When**:
- Initial setup of a new environment
- Updating infrastructure configuration (e.g., scaling Cosmos DB)
- Adding new shared resources (e.g., new Service Bus queues)
- Troubleshooting infrastructure issues

**What Runs**:
- ✅ Terraform for selected infrastructure layer(s)
- ❌ Docker builds (skipped)
- ❌ Service deployments (skipped)

**Example**: "I need to deploy the infrastructure for dev environment for the first time"
```
workflow_mode: infrastructure-only
environment: dev
terraform_layer: all
terraform_action: apply
```

---

#### deploy-services-only
**Use When**:
- Deploying application code changes
- Updating service configuration
- Deploying specific services after bug fixes
- Regular development deployments

**What Runs**:
- ❌ Infrastructure Terraform (skipped)
- ✅ Docker builds for selected services
- ✅ Service deployments via Terraform
- ✅ Health checks

**Prerequisites**: Infrastructure must already exist in the target environment

**Example**: "I just fixed a bug in the billing service and want to deploy only that service"
```
workflow_mode: deploy-services-only
environment: dev
services_to_deploy: billing
terraform_action: apply
```

---

#### full-deployment
**Use When**:
- Initial environment setup
- Major releases that include both infrastructure and code changes
- Disaster recovery (rebuilding entire environment)
- Spinning up a new environment from scratch

**What Runs**:
- ✅ Terraform for all infrastructure layers (foundation + shared-services)
- ✅ Docker builds for all services
- ✅ Service deployments
- ✅ Health checks

**Example**: "I'm setting up the staging environment for the first time"
```
workflow_mode: full-deployment
environment: staging
terraform_action: apply
```

---

### Understanding terraform_layer

The infrastructure is split into layers to minimize blast radius and reduce deployment time.

#### all (Default)
Deploys both foundation and shared-services layers sequentially.

**Use When**: Initial setup or full infrastructure updates

**Deploys**:
- Foundation layer: ACR, VNet, KeyVault, Log Analytics, App Insights, NSG, Storage
- Shared Services layer: Cosmos DB, Service Bus, UAMI, RBAC assignments

**Deployment Time**: ~15-20 minutes

---

#### foundation
Deploys only foundational resources.

**Use When**:
- Setting up base infrastructure for the first time
- Updating VNet configuration
- Scaling ACR
- Modifying Key Vault policies

**Deploys**: ACR, VNet, KeyVault, Log Analytics, App Insights, NSG, Storage

**Deployment Time**: ~8-10 minutes

---

#### shared-services
Deploys only shared services layer.

**Use When**:
- Adding Cosmos DB databases
- Updating Service Bus configuration
- Modifying RBAC role assignments
- Updating UAMI settings

**Prerequisites**: Foundation layer must already exist

**Deploys**: Cosmos DB, Service Bus, UAMI, RBAC assignments

**Deployment Time**: ~10-12 minutes

*Note: Includes 180-second wait after deployment for Azure RBAC propagation*

---

### Understanding services_to_deploy

#### all (Default)
Deploys all 6 microservices:
- billing
- customer
- file-retrieval
- fundstransfermgt
- policy
- ratingunderwriting

**Use When**: Full deployment or deploying all services after infrastructure changes

**Builds**: 12 Docker images (6 services × 2 containers each)

**Deployment Time**: ~25-30 minutes (builds run in parallel)

---

#### Specific Services
Comma-separated list of service names (no spaces).

**Examples**:
- `billing` - Deploy only billing service (2 containers)
- `billing,customer` - Deploy billing and customer services (4 containers)
- `policy,ratingunderwriting` - Deploy policy and rating services (4 containers)

**Use When**:
- Deploying bug fixes for specific services
- Testing changes in isolation
- Rolling out changes gradually

**Deployment Time**: ~8-12 minutes per service (parallel builds)

---

### Understanding terraform_action

#### plan (Default - Safe)
Shows what Terraform would do **without making any changes**.

**Use When**:
- Reviewing changes before applying
- Validating Terraform configuration
- Checking for unintended changes
- Required before running apply in production

**Output**: Terraform plan showing resources to be created, updated, or destroyed

**Best Practice**: Always run `plan` first, review the output, then run `apply`

---

#### apply (Executes Changes)
Actually creates, updates, or destroys resources.

**Use When**: You've reviewed the plan and are ready to deploy

**Warning**: This makes real changes to Azure resources. Use carefully in production.

**Best Practice**: Run in this order:
1. First: `terraform_action: plan` → Review output
2. Then: `terraform_action: apply` → Deploy changes

---

## Terraform Layers

### Why Layers?

The infrastructure is split into three layers with separate Terraform state files:

**✅ Separation of Concerns**: Different resources change at different frequencies
**✅ Reduced Blast Radius**: Minimize impact of infrastructure changes
**✅ Faster Deployments**: Update only what changed
**✅ Better State Management**: Smaller state files are easier to manage
**✅ Team Workflows**: Different teams can own different layers

---

### Layer 1: Foundation

**State File**: `foundation.tfstate`  
**Location**: `platform/infra/foundation/`  
**Purpose**: Base infrastructure needed by all services

**Resources**:
- Resource Group: CAIS-010-RiskInsure
- Virtual Network (VNet)
- Azure Container Registry (ACR): riskinsure${env}acr
- Key Vault: riskinsure-${env}-kv
- Log Analytics Workspace
- Application Insights
- Network Security Group (NSG)
- Storage Account

**Change Frequency**: Rarely (maybe once per quarter)

**Dependencies**: None (first layer to deploy)

**Typical Changes**:
- Scaling ACR SKU
- Updating VNet address space
- Modifying Key Vault access policies
- Adjusting Log Analytics retention

---

### Layer 2: Shared Services

**State File**: `shared-services.tfstate`  
**Location**: `platform/infra/shared-services/`  
**Purpose**: Shared infrastructure used by multiple services

**Resources**:
- Azure Cosmos DB (NoSQL API)
  - Single partition per service
  - Passwordless authentication via RBAC
- Azure Service Bus (Premium tier)
  - NServiceBus message broker
  - Queues, topics, subscriptions
- User-Assigned Managed Identity (UAMI)
  - Name: riskinsure-${env}-app-mi
  - Used by all Container Apps
- RBAC Role Assignments:
  - AcrPull (Container Apps → ACR)
  - Cosmos DB Data Contributor (Container Apps → Cosmos DB)
  - Service Bus Data Owner (Container Apps → Service Bus)
  - Key Vault Secrets User (Container Apps → Key Vault)

**Change Frequency**: Occasionally (monthly or when adding services)

**Dependencies**: Requires foundation layer

**Typical Changes**:
- Adding Cosmos DB databases for new services
- Creating new Service Bus queues
- Updating RBAC role assignments
- Scaling Cosmos DB throughput

**Special Note**: After deploying this layer, the workflow waits 180 seconds for Azure RBAC role assignments to propagate. This prevents "permission denied" errors during service deployment.

---

### Layer 3: Services

**State File**: `services.tfstate`  
**Location**: `platform/infra/services/`  
**Purpose**: Container Apps for microservices

**Resources**:
- Container Apps Environment
  - KEDA-based autoscaling
  - Integrated with VNet
  - Connected to Log Analytics
- 12 Container Apps (6 services × 2 containers):
  - `billing-api` & `billing-endpoint`
  - `customer-api` & `customer-endpoint`
  - `file-retrieval-api` & `file-retrieval-endpoint`
  - `fundstransfermgt-api` & `fundstransfermgt-endpoint`
  - `policy-api` & `policy-endpoint`
  - `ratingunderwriting-api` & `ratingunderwriting-endpoint`

**Change Frequency**: Very frequently (daily or multiple times per day)

**Dependencies**: Requires foundation and shared-services layers

**Typical Changes**:
- Updating Docker images (most common)
- Scaling container resources (CPU/memory)
- Modifying environment variables
- Adjusting replica counts

**Deployment Strategy**: This layer is deployed via `build-and-deploy.yml`, which builds Docker images first, then runs Terraform to update Container Apps with new image tags.

---

## Deployment Scenarios

### Scenario 1: Initial Setup (New Environment)

**Goal**: Set up dev environment from scratch

**Steps**:
```
# Step 1: Deploy infrastructure
workflow_mode: infrastructure-only
environment: dev
terraform_layer: all
terraform_action: plan  ← Review first!

# Step 2: Apply infrastructure
workflow_mode: infrastructure-only
environment: dev
terraform_layer: all
terraform_action: apply  ← Deploy it!

# Step 3: Wait 3-5 minutes for RBAC propagation (automatic)

# Step 4: Deploy services
workflow_mode: deploy-services-only
environment: dev
services_to_deploy: all
terraform_action: apply
```

**Duration**: ~45-50 minutes total

**Alternative (Faster)**: Use `full-deployment` mode to do steps 1-4 in one run.

---

### Scenario 2: Deploy Code Changes to Specific Service

**Goal**: Deploy billing service after bug fix

**Prerequisites**: Infrastructure already exists in dev

**Steps**:
```
workflow_mode: deploy-services-only
environment: dev
services_to_deploy: billing
terraform_action: apply
```

**Duration**: ~8-10 minutes

**What Happens**:
1. Workflow fetches existing infrastructure details (ACR, UAMI, etc.)
2. Builds 2 Docker images: billing-api, billing-endpoint
3. Pushes images to ACR with git SHA and 'latest' tags
4. Runs Terraform to update Container Apps with new image tags
5. Runs health check to verify deployment

---

### Scenario 3: Update Cosmos DB Configuration

**Goal**: Increase Cosmos DB throughput for production

**Steps**:
```
# Step 1: Review changes
workflow_mode: infrastructure-only
environment: prod
terraform_layer: shared-services
terraform_action: plan  ← Always plan first in prod!

# Step 2: Apply changes
workflow_mode: infrastructure-only
environment: prod
terraform_layer: shared-services
terraform_action: apply
```

**Duration**: ~10-12 minutes

**What Happens**:
1. Workflow runs Terraform for shared-services layer only
2. Updates Cosmos DB configuration
3. Waits 180 seconds for RBAC propagation
4. Verifies deployment

**Services Impact**: No downtime for Container Apps (they continue running)

---

### Scenario 4: Deploy Multiple Services to Staging

**Goal**: Deploy billing, customer, and policy services to staging for QA testing

**Prerequisites**: Infrastructure exists in staging

**Steps**:
```
workflow_mode: deploy-services-only
environment: staging
services_to_deploy: billing,customer,policy
terraform_action: apply
```

**Duration**: ~15-18 minutes

**What Happens**:
1. Workflow builds 6 Docker images in parallel (3 services × 2 containers)
2. Pushes all images to ACR
3. Runs Terraform to update 6 Container Apps
4. Runs health checks for all 3 services

---

### Scenario 5: Disaster Recovery (Rebuild Everything)

**Goal**: Completely rebuild production environment after incident

**Steps**:
```
# Option 1: Full deployment (recommended)
workflow_mode: full-deployment
environment: prod
terraform_action: apply

# Option 2: Layered approach (more control)
# Step 1: Foundation
workflow_mode: infrastructure-only
environment: prod
terraform_layer: foundation
terraform_action: apply

# Step 2: Shared Services
workflow_mode: infrastructure-only
environment: prod
terraform_layer: shared-services
terraform_action: apply

# Step 3: Services
workflow_mode: deploy-services-only
environment: prod
services_to_deploy: all
terraform_action: apply
```

**Duration**: ~45-60 minutes

**Important**: Ensure Terraform state files are available (stored in Azure Storage)

---

### Scenario 6: Gradual Rollout to Production

**Goal**: Deploy services one at a time to production with testing between deployments

**Steps**:
```
# Deploy billing first
workflow_mode: deploy-services-only
environment: prod
services_to_deploy: billing
terraform_action: apply
# ← Test billing in production

# Deploy customer next
workflow_mode: deploy-services-only
environment: prod
services_to_deploy: customer
terraform_action: apply
# ← Test customer in production

# Deploy remaining services
workflow_mode: deploy-services-only
environment: prod
services_to_deploy: file-retrieval,fundstransfermgt,policy,ratingunderwriting
terraform_action: apply
```

**Duration**: ~10 minutes per batch

**Benefit**: Minimize risk by deploying incrementally with validation between batches

---

## Troubleshooting

### Common Issues and Solutions

#### Issue: "UAMI not found" Error

**Error Message**:
```
❌ ERROR: UAMI not found: riskinsure-dev-app-mi
```

**Cause**: The User-Assigned Managed Identity hasn't been created yet, or infrastructure hasn't been deployed.

**Solution**:
1. Deploy infrastructure first:
   ```
   workflow_mode: infrastructure-only
   environment: dev
   terraform_layer: all (or shared-services)
   terraform_action: apply
   ```
2. Then deploy services

---

#### Issue: "Permission Denied" When Accessing Cosmos DB

**Error Message**:
```
Forbidden: The caller does not have permission to perform the action
```

**Cause**: RBAC role assignments haven't propagated yet (takes 3-5 minutes in Azure).

**Solution**:
- Wait 5 minutes after infrastructure deployment
- The workflow includes a 180-second automatic wait
- If still failing, redeploy the service after 5 minutes

---

#### Issue: Docker Build Fails

**Error Message**:
```
ERROR: failed to solve: failed to compute cache key
```

**Causes**:
- Project file path incorrect in Dockerfile
- Missing dependencies in .csproj
- Docker cache corruption

**Solution**:
1. Check Dockerfile build args are correct
2. Verify .csproj file exists at specified path
3. Try running with `--no-cache` flag (workflow already uses this)

---

#### Issue: Terraform State Lock

**Error Message**:
```
Error: Error acquiring the state lock
```

**Cause**: Another workflow run is in progress, or a previous run didn't complete properly.

**Solution**:
1. Wait for other workflow runs to complete
2. Check Azure Storage for state lock blob
3. If orphaned, manually delete lock file (use with caution)

---

#### Issue: Job Skipped Unexpectedly

**Symptom**: A job you expected to run shows "skipped" status

**Debugging Steps**:
1. Check `debug_outputs` job in workflow run
2. Verify conditional expressions:
   - `run_infrastructure == 'true'` for infrastructure jobs
   - `run_services == 'true'` for service jobs
3. Verify workflow_mode selection matches your intent

**Common Cause**: Selected `infrastructure-only` but expected services to deploy, or vice versa.

---

#### Issue: Health Check Fails

**Error Message**:
```
Container App status: Stopped
```

**Causes**:
- Container image failed to start
- Environment variables missing or incorrect
- UAMI doesn't have required permissions
- Cosmos DB or Service Bus connection issues

**Solution**:
1. Check Container App logs in Azure Portal
2. Verify UAMI has correct RBAC roles
3. Check environment variables in Container App configuration
4. Verify Cosmos DB and Service Bus exist and are accessible

---

### Debugging Tips

#### View Detailed Logs

1. Go to GitHub Actions run
2. Click on the specific job (e.g., `build_and_push`)
3. Expand the step that failed
4. Review complete output including errors

#### Check debug_outputs Job

Every workflow run includes a `debug_outputs` job that shows all parameters:
```
run_infrastructure: 'true'
run_services: 'false'
environment: 'dev'
terraform_action: 'apply'
terraform_layer: 'all'
services_to_deploy: 'all'
```

Use this to verify the workflow interpreted your inputs correctly.

#### Azure Portal Investigation

1. Navigate to Resource Group: CAIS-010-RiskInsure
2. Check Container App logs:
   - Select Container App → Monitoring → Log stream
3. Check Application Insights:
   - Search for exceptions and errors
4. Check Cosmos DB:
   - Verify databases exist
   - Check Data Explorer for data
5. Check Service Bus:
   - Verify queues and topics exist
   - Check message counts

---

## Best Practices

### 1. Always Plan Before Apply

```
# First run: Review changes
terraform_action: plan

# Second run: Apply changes
terraform_action: apply
```

**Especially important in production!**

---

### 2. Use Infrastructure-Only Mode for Infrastructure Changes

Don't use `full-deployment` mode just to update infrastructure. It will unnecessarily rebuild all service Docker images.

```
# ✅ Good: Update only infrastructure
workflow_mode: infrastructure-only
terraform_layer: shared-services

# ❌ Bad: Rebuilds all services unnecessarily
workflow_mode: full-deployment
```

---

### 3. Deploy Specific Services When Possible

Don't deploy all services if only one changed.

```
# ✅ Good: Deploy only changed service
services_to_deploy: billing

# ❌ Bad: Deploys all 6 services unnecessarily
services_to_deploy: all
```

---

### 4. Wait for RBAC Propagation

After deploying infrastructure (especially shared-services), wait 3-5 minutes before deploying services. The workflow includes a 180-second automatic wait, but Azure can sometimes take longer.

---

### 5. Monitor Workflow Runs

Watch the workflow run to completion. Don't assume success without checking:
- ✅ All jobs completed successfully (green checkmarks)
- ✅ Health checks passed
- ✅ No warnings in logs

---

### 6. Use Concurrency Control

The workflows include concurrency controls to prevent simultaneous deployments to the same environment. Don't try to bypass this—it protects against state conflicts.

---

### 7. Keep Documentation Updated

When you add new services or modify infrastructure, update this document and Terraform configurations accordingly.

---

## Security Considerations

### Passwordless Authentication

**GitHub Actions → Azure**: Uses OIDC (OpenID Connect) with federated credentials. No secrets stored in GitHub.

**Container Apps → Azure Resources**: Uses User-Assigned Managed Identity with RBAC. No connection strings or passwords in configuration.

### Secrets Management

**Key Vault**: All sensitive configuration (connection strings, API keys) stored in Azure Key Vault.

**Container Apps**: Access Key Vault secrets via Managed Identity.

### Network Security

**Virtual Network**: All Container Apps deployed in a VNet.

**Network Security Group**: Controls inbound/outbound traffic.

**Private Endpoints**: Cosmos DB and Service Bus can use private endpoints (configure in Terraform).

---

## Next Steps

### After Initial Setup

1. **Test Each Service**: Verify all APIs are responding
2. **Check Logs**: Review Application Insights for errors
3. **Verify Data Flow**: Send test messages through the system
4. **Configure Monitoring**: Set up alerts in Azure Monitor
5. **Document Environment-Specific Settings**: Note any custom configurations

### For Production Deployment

1. **Review All Terraform Plans**: Never apply without reviewing
2. **Enable Diagnostic Settings**: Comprehensive logging for all resources
3. **Set Up Alerts**: Monitor Container App health, Cosmos DB throughput, Service Bus queue lengths
4. **Configure Backup**: Enable Cosmos DB backup (automatic continuous backup)
5. **Test Disaster Recovery**: Practice rebuilding from Terraform state

---

## Support and Questions

For questions about these workflows:

1. Review this documentation
2. Check the `debug_outputs` job in failed workflow runs
3. Review Azure Portal logs for runtime issues
4. Consult Terraform documentation for infrastructure issues
5. Contact the platform team for assistance

---

**Last Updated**: February 24, 2026  
**Maintained By**: Platform Team  
**Version**: 1.0
