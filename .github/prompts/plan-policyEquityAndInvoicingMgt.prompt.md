## Plan: Rename Billing Bounded Context to PolicyEquityAndInvoicingMgt

**TL;DR**: Comprehensive rename of the 'billing' bounded context to 'PolicyEquityAndInvoicingMgt', affecting folder structure, namespaces, project files, solution references, Docker configurations, GitHub workflows, documentation, and NServiceBus endpoint names. This is a large-scale refactoring that requires careful coordination across ~100+ files.

**Steps**

### Phase 1: Preparation and Validation (Pre-Change)
1. **Verify current state** — Confirm no uncommitted changes in billing service, run `dotnet build` and `dotnet test` to ensure clean baseline
2. **Create feature branch** — Create new branch from current branch (meghan/renameBillingService)
3. **Document port conventions** — Note that billing uses ports 7071/7072 (API/Endpoint) - decide whether to keep these or reassign

### Phase 2: File System and Project Structure (*must complete before code changes*)
4. **Rename root service folder** (*blocking for all other steps*):
   - `services/billing/` → `services/policyequityandinvoicingmgt/`
   - Preserves internal structure: `src/`, `test/`, `docs/`

5. **Update solution file** — Modify `RiskInsure.slnx`:
   - Line 7: `/services/billing/` → `/services/policyequityandinvoicingmgt/`
   - Line 8: `/services/billing/src/` → `/services/policyequityandinvoicingmgt/src/`
   - Lines 9-12: Update all project paths (4 projects in src/)
   - Line 14: `/services/billing/test/` → `/services/policyequityandinvoicingmgt/test/`
   - Line 15: Update test project path

### Phase 3: .NET Project Files and Namespaces (*parallel with Phase 2*)
6. **Update .csproj RootNamespace** (3 files):
   - `services/policyequityandinvoicingmgt/src/Api/Api.csproj`: `RiskInsure.Billing.Api` → `RiskInsure.PolicyEquityAndInvoicingMgt.Api`
   - `services/policyequityandinvoicingmgt/src/Domain/Domain.csproj`: `RiskInsure.Billing.Domain` → `RiskInsure.PolicyEquityAndInvoicingMgt.Domain`
   - `services/policyequityandinvoicingmgt/src/Endpoint.In/Endpoint.In.csproj`: `RiskInsure.Billing.Endpoint.In` → `RiskInsure.PolicyEquityAndInvoicingMgt.Endpoint.In`
   - `services/policyequityandinvoicingmgt/src/Infrastructure/Infrastructure.csproj`: `RiskInsure.Billing.Infrastructure` → `RiskInsure.PolicyEquityAndInvoicingMgt.Infrastructure`

7. **Update C# namespace declarations** (~60 .cs files) — Replace `namespace RiskInsure.Billing.*` with `namespace RiskInsure.PolicyEquityAndInvoicingMgt.*` in:
   - All files in `src/Api/` (Program.cs, Controllers/, Models/)
   - All files in `src/Domain/` (Managers/, Services/, Contracts/, Models/)
   - All files in `src/Infrastructure/` (NServiceBusConfigurationExtensions.cs, etc.)
   - All files in `src/Endpoint.In/` (Program.cs, Handlers/)
   - All files in `test/Unit.Tests/`

8. **Update using directives** — Replace `using RiskInsure.Billing.*` references across all .cs files

### Phase 4: NServiceBus Configuration (Critical for Message Routing)
9. **Update endpoint names** (2 files):
   - `src/Api/Program.cs` line 162: `"RiskInsure.Billing.Api"` → `"RiskInsure.PolicyEquityAndInvoicingMgt.Api"`
   - `src/Api/Program.cs` line 168: `"RiskInsure.Billing.Endpoint"` → `"RiskInsure.PolicyEquityAndInvoicingMgt.Endpoint"`
   - `src/Endpoint.In/Program.cs` line 37: `"RiskInsure.Billing.Endpoint"` → `"RiskInsure.PolicyEquityAndInvoicingMgt.Endpoint"`

10. **Update log messages** — Replace "Billing API", "Billing Endpoint.In" with "PolicyEquityAndInvoicingMgt API", etc. in Program.cs files

### Phase 5: Docker and Local Development
11. **Update docker-compose.domain.yml** (9 references):
    - Line 6: Comment `# Billing Domain` → `# PolicyEquityAndInvoicingMgt Domain`
    - Line 8: `billing-api:` → `policyequityandinvoicingmgt-api:`
    - Line 13: `PROJECT_PATH: services/billing/src/Api/Api.csproj` → `services/policyequityandinvoicingmgt/src/Api/Api.csproj`
    - Line 26: `CosmosDb__BillingContainerName=Billing` → `CosmosDb__BillingContainerName=PolicyEquityAndInvoicingMgt` (or keep as "Billing" if preserving data - **needs decision**)
    - Line 37: `billing-endpoint:` → `policyequityandinvoicingmgt-endpoint:`
    - Line 42: `PROJECT_PATH: services/billing/src/Endpoint.In/Endpoint.In.csproj` → `services/policyequityandinvoicingmgt/src/Endpoint.In/Endpoint.In.csproj`
    - Line 52: Update second `CosmosDb__BillingContainerName` reference

12. **Update appsettings files** (2 template files):
    - `src/Api/appsettings.Development.json.template`: Update container name reference if present
    - `src/Endpoint.In/appsettings.Development.json.template`: Update container name reference if present

13. **Update devcontainer configuration**:
    - `.devcontainer/devcontainer.json` lines 52, 62: "Billing API" → "PolicyEquityAndInvoicingMgt API"
    - `.devcontainer/post-create.sh` line 29: Update echo message
    - `.devcontainer/README.md` line 36, 76: Update service name

### Phase 6: GitHub Workflows and CI/CD (*can run parallel with Phase 5*)
14. **Update workflow files** (6 active workflows):
    - `.github/workflows/agentic-e2e-validate.yml` line 54: `BILLING_API_URL` → `POLICYEQUITYANDINVOICINGMGT_API_URL`
    - `.github/workflows/cd-services-dev.yml` lines 68, 129: Replace `"billing"` with `"policyequityandinvoicingmgt"` in service arrays
    - `.github/workflows/ci-build-services.yml` lines 65, 117, 188: Update service references
    - `.github/workflows/ci-test-integration.yml` lines 86, 112, 161, 169, 231-232: Update endpoint names and environment variables
    - `.github/workflows/ops-rollback.yml` line 18: Update service list
    - Legacy workflow files in `.github/workflows/legacy/`: Update for consistency but verify they're not actively used

### Phase 7: Documentation Updates (*parallel with Phase 6*)
15. **Update copilot-instructions** (19 files):
    - `.github/copilot-instructions.md` lines 23, 132, 137, 142, 150, 180, 185, 252, 277, 508-509: Replace "Billing" references with "PolicyEquityAndInvoicingMgt"
    - `copilot-instructions/api-conventions.md`: Update all BillingAccountManager/BillingPaymentManager examples
    - `copilot-instructions/cross-domain-integration.md`: Update extensive examples in lines 495-782
    - `copilot-instructions/data-patterns.md`: Update BillingAccount examples (lines 89-524)

16. **Update agent documentation** (`.github/agents/` - 10 files):
    - `document-manager.md`: Replace BillingAccountManager examples
    - `e2e-contract-verifier-agent.md`: Update domain references
    - `integration-contract-verifier-agent.md`: Update service name and paths
    - `integration-handler-validator-agent.md`: Update handler examples
    - `documentation-sync-agent.md`: Update service list
    - `domain-builder.agent.md`: Update template references
    - `local-smoke-test-agent.md`: Update API URLs
    - Others as identified in search results

17. **Update WORKFLOWS-GUIDE.md**: Replace 50+ references to billing service with new name

18. **Update service-specific documentation**:
    - `services/policyequityandinvoicingmgt/README.md`: Update title and descriptions
    - `services/policyequityandinvoicingmgt/docs/business/*.md`: Review and update domain-specific docs
    - `services/policyequityandinvoicingmgt/docs/technical/*.md`: Update technical specifications

### Phase 8: Integration Testing Configuration
19. **Update Playwright tests**:
    - `services/policyequityandinvoicingmgt/test/Integration.Tests/playwright.config.ts`: Update baseURL references
    - `services/policyequityandinvoicingmgt/test/Integration.Tests/tests/billing-account-lifecycle.spec.ts`: Rename file and update test descriptions
    - `services/policyequityandinvoicingmgt/test/Integration.Tests/package.json`: Update project name and scripts

### Phase 9: OpenAPI Documentation
20. **Update API documentation** in `src/Api/Program.cs`:
    - Line 66: `"RiskInsure Billing API"` → `"RiskInsure PolicyEquityAndInvoicingMgt API"`
    - Lines 70-81: Update capability descriptions (remove "billing" terminology if domain-specific)
    - Update route prefixes in controllers if needed: `/api/billing/*` → `/api/policyequityandinvoicingmgt/*` (**needs decision - breaking change**)

### Phase 10: Verification and Testing
21. **Build verification** (*depends on steps 1-8*):
    - Run `dotnet restore` from solution root
    - Run `dotnet build` — expect 0 errors
    - Verify all projects in solution compile

22. **Test verification** (*depends on step 21*):
    - Run `dotnet test` on Unit.Tests project
    - Start local infrastructure (Cosmos emulator, RabbitMQ)
    - Start API and Endpoint.In projects locally
    - Run Playwright integration tests

23. **Docker verification** (*depends on step 21*):
    - Run `docker-compose -f docker-compose.domain.yml build policyequityandinvoicingmgt-api policyequityandinvoicingmgt-endpoint`
    - Run `docker-compose -f docker-compose.domain.yml up policyequityandinvoicingmgt-api policyequityandinvoicingmgt-endpoint`
    - Verify containers start successfully and health checks pass

24. **Integration verification**:
    - Test message routing to other services (FundsTransferMgt sends FundsSettled → should reach PolicyEquityAndInvoicingMgt)
    - Verify Cosmos DB container access (confirm container name decision from step 11)

**Relevant Files**

Primary Service Files:
- **Folder**: `services/billing/` (entire directory - 71 files total)
- **Solution**: `RiskInsure.slnx` (lines 7-15)
- **Program.cs**: `src/Api/Program.cs` (NServiceBus config, OpenAPI docs), `src/Endpoint.In/Program.cs`
- **Project files**: 4 projects in `src/` (Api.csproj, Domain.csproj, Infrastructure.csproj, Endpoint.In.csproj)
- **All C# files**: ~60 files across Api/, Domain/, Infrastructure/, Endpoint.In/, test/

Docker & Local Dev:
- `docker-compose.domain.yml` (lines 6-52)
- `.devcontainer/devcontainer.json`, `.devcontainer/post-create.sh`, `.devcontainer/README.md`

GitHub Workflows:
- `.github/workflows/agentic-e2e-validate.yml`
- `.github/workflows/cd-services-dev.yml`
- `.github/workflows/ci-build-services.yml`
- `.github/workflows/ci-test-integration.yml`
- `.github/workflows/ops-rollback.yml`
- Legacy workflows in `.github/workflows/legacy/*.yml` (6 files)

Documentation (100+ references):
- `.github/copilot-instructions.md`
- All files in `copilot-instructions/*.md` (19 files)
- All files in `.github/agents/*.md` (10+ files)
- `.github/WORKFLOWS-GUIDE.md`
- Service docs in `services/billing/docs/` (business/, technical/)

**Verification**

1. **Build verification**: Run `dotnet build` from solution root — expect 0 errors
2. **Test verification**: Run `dotnet test` — all unit tests pass
3. **Solution verification**: Open solution in Visual Studio 2025 — all projects load without errors
4. **Docker verification**: Build and run docker-compose with renamed services — containers start successfully
5. **Integration verification**: Test message flow from FundsTransferMgt → PolicyEquityAndInvoicingMgt endpoint
6. **API verification**: Access Swagger UI at http://localhost:7071/swagger (if port unchanged) — API documentation displays correctly
7. **Namespace verification**: Run `grep -r "RiskInsure.Billing" services/policyequityandinvoicingmgt/` — expect 0 matches
8. **Configuration verification**: Check `appsettings.json` files in both API and Endpoint.In — no legacy references

**Decisions**

**Decision 1: Cosmos DB Container Name** (blocking for step 11)
- Option A: Keep existing container name "Billing" in Cosmos DB (preserves data, minimal migration)
- Option B: Rename container to "PolicyEquityAndInvoicingMgt" (requires data migration, cleaner naming)
- **Recommendation**: Option A initially (preserve data), plan separate data migration if needed

**Decision 2: API Route Prefix** (blocking for step 20)
- Option A: Keep `/api/billing/*` routes (backward compatibility, no breaking change)
- Option B: Change to `/api/policyequityandinvoicingmgt/*` (consistent naming, BREAKING CHANGE)
- Option C: Change to shorter alias `/api/peiam/*` (pragmatic, still breaking)
- **Recommendation**: Option A initially (avoid breaking contracts), deprecate old routes later if needed

**Decision 3: Port Numbers** (blocking for step 13)
- Option A: Keep existing ports 7071/7072 (no infrastructure changes needed)
- Option B: Reassign ports to reflect alphabetical ordering (e.g., 7075/7076)
- **Recommendation**: Option A (ports are arbitrary identifiers, no need to change)

**Decision 4: NServiceBus Queue Names** (blocking for step 9)
- Physical queue names in RabbitMQ will automatically change from "RiskInsure.Billing.Endpoint" to "RiskInsure.PolicyEquityAndInvoicingMgt.Endpoint"
- **Impact**: Other services routing commands must update their routing configuration to new endpoint name
- **Action Required**: After this rename, update FundsTransferMgt and any other services that send commands to this endpoint

**Decision 5: PublicContracts Events** (investigate before Phase 9)
- Check if any events in `platform/RiskInsure.PublicContracts/` reference "Billing" in their names (e.g., BillingAccountCreated)
- **If yes**: These are PUBLIC contracts — renaming creates breaking changes for subscribers
- **Recommendation**: Keep public event names unchanged OR create new events with deprecated markers on old ones

**Further Considerations**

1. **Data Migration** — If choosing to rename Cosmos container (Decision 1 Option B), need data migration script to copy documents from "Billing" to "PolicyEquityAndInvoicingMgt" container
2. **Deployment Coordination** — Renaming NServiceBus endpoints requires coordinated deployment:
   - Deploy routing config updates to consumer services FIRST
   - Then deploy renamed PolicyEquityAndInvoicingMgt service
   - Monitor RabbitMQ for messages sent to old endpoint name (should be zero)
3. **Documentation Debt** — Many example files reference "Billing" as the template service — update these to use a different example or create explicit "Example Service" documentation
4. **Terraform Updates** — If infrastructure is managed via Terraform (platform/infra/), update container app names and configuration
5. **Monitoring and Alerts** — Update any monitoring dashboards, alerts, or log queries that reference "billing" service names
