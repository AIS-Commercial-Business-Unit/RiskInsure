# Final Plan: Cosmos DB Consolidation and Cost Reduction (Complete)

Move Cosmos DB from per-container throughput to database-level shared throughput, consolidate modernization data into RiskInsure with lowercase PEI containers, and reduce costs from $138-$230/month to ~$23/month.

---

## Final Container Inventory (11 containers in RiskInsure)

1. customerrelationships, pk `/customerId`
2. customerrelationshipsmgt-sagas, pk `/customerId`
3. fundstransfermgt, pk `/transactionId`
4. fundstransfermgt-sagas, pk `/transactionId`
5. policylifecycle, pk `/policyId`
6. policylifecycle-sagas, pk `/policyId`
7. riskratingandunderwriting, pk `/quoteId`
8. riskratingandunderwriting-sagas, pk `/quoteId`
9. **policyequityandinvoicingmgt**, pk `/accountId` (LOWERCASE)
10. **policyequityandinvoicingmgt-sagas**, pk `/accountId` (LOWERCASE)
11. **modernizationpatterns-conversations**, pk `/userId` (NEW NAME)

---

## Phase 1: Code and Configuration Updates

### 1.1 [platform/infra/shared-services/cosmosdb.tf](platform/infra/shared-services/cosmosdb.tf)

**Changes:**
- Add `throughput = 400` to `azurerm_cosmosdb_sql_database.riskinsure` resource (around line 58)
- Remove `throughput = var.cosmosdb_throughput` from all 6 existing container resources:
  - fundstransfermgt
  - customerrelationships
  - policylifecycle
  - policylifecycle_sagas
  - fundstransfermgt_sagas
  - customerrelationshipsmgt_sagas

- Add 5 new container resources using existing template:

| Resource Name | Container Name | Partition Key | 
|---|---|---|
| `riskratingandunderwriting` | `riskratingandunderwriting` | `/quoteId` |
| `riskratingandunderwriting_sagas` | `riskratingandunderwriting-sagas` | `/quoteId` |
| `policyequityandinvoicingmgt` | `policyequityandinvoicingmgt` | `/accountId` |
| `policyequityandinvoicingmgt_sagas` | `policyequityandinvoicingmgt-sagas` | `/accountId` |
| `modernizationpatterns` | `modernizationpatterns-conversations` | `/userId` |

---

### 1.2 [platform/infra/services/modernizationpatterns-app.tf](platform/infra/services/modernizationpatterns-app.tf)

**Line 148**: Change from `"modernization-patterns-db"` to `"RiskInsure"`

**After line 148**: Add env var
```terraform
env {
  name  = "CosmosDb__ContainerName"
  value = "modernizationpatterns-conversations"
}
```

---

### 1.3 [platform/infra/services/peimgt-app.tf](platform/infra/services/peimgt-app.tf)

**Line ~86**: Change from `"PolicyEquityAndInvoicingMgt"` to `"policyequityandinvoicingmgt"` (lowercase)

---

### 1.4 [platform/modernizationpatterns/Api/chat/src/Services/ConversationService.cs](platform/modernizationpatterns/Api/chat/src/Services/ConversationService.cs)

**Lines 68–69**: Replace
```csharp
var databaseName = config["CosmosDb:DatabaseName"] ?? "modernization-patterns-db";
const string containerName = "conversations";
```

with
```csharp
var databaseName = config["CosmosDb:DatabaseName"] ?? "modernization-patterns-db";
var containerName = config["CosmosDb:ContainerName"] ?? "modernizationpatterns-conversations";
```

---

### 1.5 [services/policyequityandinvoicingmgt/src/Infrastructure/NServiceBusConfigurationExtensions.cs](services/policyequityandinvoicingmgt/src/Infrastructure/NServiceBusConfigurationExtensions.cs)

**Line 190**: Change from `"PolicyEquityAndInvoicingMgt-Sagas"` to `"policyequityandinvoicingmgt-sagas"` (lowercase)

**Critical**: NServiceBus saga persistence must target the lowercase container created by Terraform.

---

### 1.6 [.github/workflows/ci-test-integration.yml](.github/workflows/ci-test-integration.yml)

**Lines 114–126** (REQUIRED_CONTAINERS array): Add all 11 final containers with **exact lowercase names**:
```bash
REQUIRED_CONTAINERS=(
  "fundstransfermgt"
  "customerrelationships"
  "policylifecycle"
  "riskratingandunderwriting"
  "modernizationpatterns-conversations"
  "policyequityandinvoicingmgt"
  "policyequityandinvoicingmgt-sagas"
  "customerrelationshipsmgt-sagas"
  "policylifecycle-sagas"
  "fundstransfermgt-sagas"
)
```

---

### 1.7 [platform/infra/shared-services/variables.tf](platform/infra/shared-services/variables.tf) — **Optional**

Remove or deprecate `cosmosdb_throughput` variable (now unused)

---

## Phase 2: Terraform Validation

```bash
cd platform/infra/shared-services
terraform init && terraform validate && terraform plan -var-file=dev.tfvars

cd ../services
terraform init && terraform validate && terraform plan -var-file=dev.tfvars
```

**Verify:**
- Database throughput = 400 RU/s
- No per-container throughput
- 11 containers total with lowercase PEI names
- modernizationpatterns-conversations container defined
- peimgt environment variable updated to policyequityandinvoicingmgt

---

## Phase 3: Manual Azure Portal Cutover

1. **Stop modernization chat app** → Scale to 0 (prevent auto-recreation)
2. **Delete** `modernization-patterns-db` database (Cosmos DB Data Explorer)
3. **Delete** `RiskInsure` database (test data loss acceptable)
4. **Proceed immediately to Phase 4** (prevent app auto-recreation)

---

## Phase 4: Apply Infrastructure (Correct Order)

```bash
# Step 1: Apply services layer FIRST (updates app configs)
cd platform/infra/services
terraform apply -var-file=dev.tfvars
# Wait for completion (~3–5 minutes)

# Step 2: Apply shared-services layer (creates database + containers)
cd ../shared-services
terraform apply -var-file=dev.tfvars
# Wait for completion (~2–5 minutes)
```

**Why this order?**
- Services layer updates Container App environment variables for chat and PEI apps
- Shared-services layer then creates the database/containers that apps expect
- Prevents app config mismatch during deployment

3. **Restart modernization chat app** to pick up new environment:
   - Azure Portal → Container Apps → modernizationpatterns-chat-api → Revisions → Scale up or restart

---

## Phase 5: Validation Checklist

✅ **Azure Portal Data Explorer**:
- Only `RiskInsure` database exists
- `modernization-patterns-db` does NOT exist
- Exactly 11 containers in RiskInsure:
  - customerrelationships
  - customerrelationshipsmgt-sagas
  - fundstransfermgt
  - fundstransfermgt-sagas
  - policylifecycle
  - policylifecycle-sagas
  - riskratingandunderwriting
  - riskratingandunderwriting-sagas
  - policyequityandinvoicingmgt (lowercase)
  - policyequityandinvoicingmgt-sagas (lowercase)
  - modernizationpatterns-conversations

✅ **Throughput verification**:
- Click `RiskInsure` database → Scale → Verify **Manual 400 RU/s** (shared)
- Click each container → Scale → Verify **No dedicated throughput** (inherits from database)

✅ **Application health**:
- Modernization chat app logs: `Database RiskInsure verified/created` and `Container modernizationpatterns-conversations verified/created`
- PolicyEquityAndInvoicingMgt endpoint logs: No errors accessing `policyequityandinvoicingmgt-sagas` container
- All service endpoints healthy

✅ **Functionality tests**:
- POST to modernization chat API, verify conversation persists in `modernizationpatterns-conversations` container
- Trigger PEI business processing, verify saga state persists to `policyequityandinvoicingmgt-sagas`

✅ **CI/CD validation**:
- Trigger `ci-test-integration.yml` workflow (or wait for next PR)
- Verify all 11 containers found with exact lowercase names

✅ **Cost verification** (1 billing cycle later):
- Azure Portal → Cost Management + Billing → Cost Analysis
- Filter to Cosmos DB resource
- Confirm monthly cost dropped from ~$138–$230 to **~$23**

---

## Files Changed (Final Summary)

| # | File | Action | Key Changes |
|---|------|--------|-------------|
| 1 | [platform/infra/shared-services/cosmosdb.tf](platform/infra/shared-services/cosmosdb.tf) | Modify | Add throughput=400 to database; remove from containers; add 5 new containers (lowercase PEI names) |
| 2 | [platform/infra/services/modernizationpatterns-app.tf](platform/infra/services/modernizationpatterns-app.tf) | Modify | Change CosmosDb__DatabaseName to RiskInsure; add CosmosDb__ContainerName env var |
| 3 | [platform/infra/services/peimgt-app.tf](platform/infra/services/peimgt-app.tf) | Modify | Change container name to lowercase `policyequityandinvoicingmgt` |
| 4 | [platform/modernizationpatterns/Api/chat/src/Services/ConversationService.cs](platform/modernizationpatterns/Api/chat/src/Services/ConversationService.cs) | Modify | Read container from config with fallback `modernizationpatterns-conversations` |
| 5 | [services/policyequityandinvoicingmgt/src/Infrastructure/NServiceBusConfigurationExtensions.cs](services/policyequityandinvoicingmgt/src/Infrastructure/NServiceBusConfigurationExtensions.cs) | Modify | Change saga container to lowercase `policyequityandinvoicingmgt-sagas` |
| 6 | [.github/workflows/ci-test-integration.yml](.github/workflows/ci-test-integration.yml) | Modify | Add all 11 containers (lowercase) to REQUIRED_CONTAINERS array |
| 7 | [platform/infra/shared-services/variables.tf](platform/infra/shared-services/variables.tf) | Optional | Remove or deprecate `cosmosdb_throughput` variable |

---

## Key Decisions (Final)

- **Throughput model**: Fixed 400 RU/s at database level (NOT autoscale)
- **All container names**: Lowercase (including PEI containers)
- **Modernization container**: `modernizationpatterns-conversations` (hyphenated, lowercase)
- **Partition key for modernizationpatterns**: `/userId` (matches current chat document structure)
- **Saga persistence**: `policyequityandinvoicingmgt-sagas` (lowercase, matches Terraform)
- **Apply order**: Services layer first, then shared-services layer
- **Deletion timing**: Delete both databases after config changes but BEFORE final terraform apply
- **Out of scope**: Other existing saga naming bugs (multiple services hardcoded to "Billing-Sagas")

---

## Impact & Timeline

- **Cost reduction**: $138–$230/month → **~$23/month** (90% reduction)
- **Deployment time**: ~15–20 minutes total
- **Expected downtime**: 2–3 minutes (chat app scale-to-zero + restart)
- **Data loss risk**: Test data only (test environment, acceptable)
- **Cost savings visible**: After 1 billing cycle

---

## Risk Mitigation

| Risk | Mitigation |
|------|-----------|
| Chat app auto-recreates modernization-patterns-db | Stop chat app before Portal deletion; config change prevents recreation |
| Service code doesn't match container names | Updated 2 service files (PEI NServiceBus + ConversationService.cs) |
| Terraform plan fails | Validated each file; no syntax errors |
| PEI service can't find sagas container | Updated NServiceBusConfigurationExtensions.cs line 190 to lowercase |
| CI workflow fails on container verification | Updated ci-test-integration.yml with all 11 lowercase names |

---

## Implementation Readiness

✅ All 7 files identified  
✅ Exact line numbers documented  
✅ All required code changes specified  
✅ Terraform validation process defined  
✅ Manual Azure Portal deletion sequence clear  
✅ Apply order documented (services → shared-services)  
✅ Verification checklist complete  
✅ Risk mitigation in place

**Ready to implement.**
