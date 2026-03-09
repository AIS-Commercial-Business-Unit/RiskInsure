## Plan: Clone Policy to PolicyLifeCycleMgt

Create a new PolicyLifeCycleMgt bounded context by cloning Policy and renaming namespaces, contracts, endpoints, and domain language. Use a fresh Cosmos container, run both services in parallel with feature-flagged/partial traffic, then gradually cut over while Policy remains stable until retirement.

**Steps**
1. Discovery and baseline mapping: inventory Policy projects (Api, Domain, Infrastructure, Endpoint.In), 3 internal events (PolicyBound, PolicyCancelled, PolicyReinstated), 1 external event consumed (QuoteAccepted), 1 external event produced (PolicyIssued), 5 API endpoints, PolicyManager with 6 methods, PolicyRepository with 7 methods, PolicyNumberGenerator service, and Cosmos container usage to define the exact clone surface and rename targets. *completed - full Policy structure documented*
2. Scaffold the new service by copying Policy structure into services/policylifecyclemgt and creating new project files with updated RootNamespace/AssemblyName/Port (7077 → next available port). *parallel with step 3*
3. Rename domain language: update namespaces from `RiskInsure.Policy.*` to `RiskInsure.PolicyLifeCycleMgt.*`, rename event names (PolicyBound → LifeCycleInitiated, PolicyCancelled → LifeCycleCancelled, PolicyReinstated → LifeCycleReinstated), handler names (QuoteAcceptedHandler → QuoteAcceptedLifeCycleHandler), managers (PolicyManager → LifeCycleManager), repository types (IPolicyRepository → ILifeCycleRepository), API controller (PoliciesController → LifeCyclesController), routes (api/policies → api/lifecycles), and model types (Policy → LifeCycle). Ensure commands/events follow naming rules. *parallel with step 2*
4. Update infrastructure wiring: NServiceBus endpoint names from "RiskInsure.Policy.*" to "RiskInsure.PolicyLifeCycleMgt.*", routing configurations, appsettings keys, and Cosmos settings for a fresh container (rename container from "policy" to "policylifecycle", keeping partition key `/policyId`). *depends on step 2*
5. Update domain-specific terminology: rename PolicyNumber generation pattern (KWG-{year}-{sequence} → LCM-{year}-{sequence} or PLM-{year}-{sequence}), update DTOs (PolicyResponse → LifeCycleResponse, CancelPolicyRequest → CancelLifeCycleRequest), update status terminology (Bound/Issued/Active/Cancelled/Expired/Lapsed/Reinstated), PolicyNumberGenerator → LifeCycleNumberGenerator, and validation logic references. Update counter document type in Cosmos. *depends on step 3*
6. Register in solution and documentation: add new projects to RiskInsure.slnx (Api, Domain, Infrastructure, Endpoint.In, Unit.Tests, Integration.Tests) and create/update docs under services/policylifecyclemgt/docs (overview.md, business/, technical/) to reflect PolicyLifeCycleMgt domain language and standards. *depends on step 2*
7. Update test structure: rename test namespaces (PolicyManagerTests → LifeCycleManagerTests), update Playwright config to new API port, update test data and assertions to use new terminology (policy → lifecycle property names where appropriate, keeping policyId as identifier), configure Integration.Tests to point to new API endpoint. *depends on step 5*
8. Update cross-service integration: review PublicContracts usage - QuoteAccepted consumption and PolicyIssued production. Decide if new events (LifeCycleIssued) should be published or if Policy service events remain authoritative during transition. Update event subscriptions in billing service if needed. *depends on step 4*
9. Parallel-run readiness: add feature-flag/config gate in API to direct traffic to new service; keep Policy stable and set partial traffic routing strategy. Coordinate with Rating & Underwriting to dual-publish QuoteAccepted to both endpoints during transition. *depends on step 8*
10. Data independence verification: ensure new service uses its own Cosmos container and does not read Policy data; confirm idempotency keys work correctly (quoteId-based deduplication), logging contains new correlation identifiers (policyId), ETag-based optimistic concurrency control functions, and sequential number generation isolated from Policy counter. *depends on step 4*
11. Cutover plan: define traffic ramp steps (0% → 10% → 50% → 100%), monitoring signals (error rates, latency, event publishing metrics, premium calculation accuracy, sequential numbering gaps), rollback criteria (error threshold, data corruption detection, integration failures with billing), and Policy service retirement timeline. Coordinate cutover timing with billing service dependencies. *depends on step 9*

**Relevant files**

**Source (Policy service)**:
- services/policy/src/Api/Program.cs — API wiring, NServiceBus send-only setup, Cosmos initialization, Serilog config (port 7077)
- services/policy/src/Api/Controllers/PoliciesController.cs — 5 endpoints (POST /{id}/issue, GET /{id}, GET /customers/{id}/policies, POST /{id}/cancel, POST /{id}/reinstate)
- services/policy/src/Domain/Contracts/Events/ — 3 internal events: PolicyBound, PolicyCancelled, PolicyReinstated
- platform/RiskInsure.PublicContracts/ — QuoteAccepted (consumed), PolicyIssued (produced)
- services/policy/src/Domain/Managers/PolicyManager.cs — 6 methods: CreateFromQuoteAsync, IssuePolicyAsync, CancelPolicyAsync, ReinstatePolicyAsync, GetPolicyAsync, GetCustomerPoliciesAsync
- services/policy/src/Domain/Repositories/ — IPolicyRepository interface + PolicyRepository implementation (7 methods: GetByIdAsync, GetByQuoteIdAsync, GetByPolicyNumberAsync, GetByCustomerIdAsync, CreateAsync, UpdateAsync, GetExpirablePoliciesAsync)
- services/policy/src/Domain/Services/PolicyNumberGenerator.cs — Sequential numbering (KWG-{year}-{sequence:D6}) with optimistic concurrency, stores counter document in same container
- services/policy/src/Domain/Models/Policy.cs — Aggregate root with partition key policyId, status state machine, premium calculations
- services/policy/src/Infrastructure/CosmosDbInitializer.cs — Container "policy" with partition key "/policyId", database "RiskInsure"
- services/policy/src/Infrastructure/NServiceBusConfigurationExtensions.cs — Unified configuration pattern (reuse as-is)
- services/policy/src/Endpoint.In/Program.cs — Message handler host wiring (port 7078)
- services/policy/src/Endpoint.In/Handlers/QuoteAcceptedHandler.cs — Consumes QuoteAccepted, creates policy, publishes PolicyBound
- services/policy/test/Unit.Tests/ — xUnit tests for PolicyManager, PolicyRepository (CreateFromQuoteAsync, IssuePolicyAsync, CancelPolicyAsync tests)
- services/policy/test/Integration.Tests/ — Playwright tests (Node.js/TypeScript)
- services/policy/docs/ — overview.md, business/, technical/ documentation

**Target (new location)**:
- services/policylifecyclemgt — complete cloned structure

**Global references**:
- RiskInsure.slnx — add 6 new projects (Api, Domain, Infrastructure, Endpoint.In, Unit.Tests, Integration.Tests)
- platform/RiskInsure.PublicContracts/ — review QuoteAccepted/PolicyIssued usage, decide on new event names
- services/billing/ — downstream consumer of PolicyIssued events (coordinate cutover)
- copilot-instructions/naming-conventions.md — naming rules for commands/events/classes
- copilot-instructions/project-structure.md — required layer structure
- .specify/memory/constitution.md — non-negotiable architecture rules

**Verification**
1. Build new service projects: `dotnet build RiskInsure.slnx` (should compile without errors)
2. Run unit tests: `dotnet test services/policylifecyclemgt/test/Unit.Tests/Unit.Tests.csproj` (all tests should pass with updated assertions)
3. Start Cosmos DB Emulator: verify new container "policylifecycle" is created automatically with partition key "/policyId"
4. Verify sequential number generation: confirm counter document "PolicyNumberCounter-2026" (or "LifeCycleNumberCounter-2026") created in new container, isolated from Policy service counter
5. Run API locally: `dotnet run --project services/policylifecyclemgt/src/Api/Api.csproj` (should start on configured port)
6. Run Endpoint.In locally: `dotnet run --project services/policylifecyclemgt/src/Endpoint.In/Endpoint.In.csproj` (should connect to RabbitMQ and subscribe to QuoteAccepted)
7. Exercise API endpoints via Postman/curl: POST issue lifecycle, GET retrieve by ID, GET customer lifecycles, POST cancel lifecycle, POST reinstate lifecycle
8. Verify events published: confirm LifeCycleInitiated, LifeCycleCancelled, LifeCycleReinstated, LifeCycleIssued events appear in RabbitMQ management UI (localhost:15672)
9. Test cross-service integration: publish QuoteAccepted message to RabbitMQ, verify QuoteAcceptedHandler creates lifecycle and publishes events, confirm billing service receives LifeCycleIssued (or PolicyIssued) event
10. Confirm no cross-service data access: query both "policy" and "policylifecycle" containers to verify data isolation and separate sequential numbering
11. Test premium calculation: cancel a lifecycle mid-term and verify unearned premium calculation matches original Policy logic
12. Verify logging: check logs contain new correlation identifiers (policyId), proper log levels (Information/Warning/Error), structured logging format, and sequential number generation audit trail
13. Run Playwright integration tests: `cd services/policylifecyclemgt/test/Integration.Tests && npm test` (update port in playwright.config.ts first)
14. Test state machine transitions: verify Bound → Issued → Active → Cancelled/Expired/Lapsed → Reinstated status flow with proper validations

**Decisions**
- Rename/reshape all contracts for PolicyLifeCycleMgt domain language (Policy → LifeCycle terminology, PolicyBound → LifeCycleInitiated)
- Use a fresh Cosmos container "policylifecycle" (no shared data with original Policy service)
- Keep partition key `/policyId` (same as Policy service for consistency)
- Change sequential number generation from "KWG-{year}-{sequence}" to "LCM-{year}-{sequence}" (LifeCycle Management) or "PLM-{year}-{sequence}" (Policy LifeCycle Management)
- Rename PolicyNumberGenerator to LifeCycleNumberGenerator with separate counter document type
- Cutover via parallel run with feature-flagged/partial traffic routing
- API port: determine next available port (7077 currently used by Policy, suggest 7079 for API, 7080 for Endpoint.In)
- Coordinate with Rating & Underwriting service to dual-publish QuoteAccepted during transition period
- Coordinate with billing service for event subscription updates (PolicyIssued → LifeCycleIssued or maintain existing event names)
- No external deadline constraints - prioritize correctness over speed
- Maintain backward compatibility during transition period (Policy service remains operational)

**Further Considerations**
1. **Partition Key Naming**: Keep `/policyId` for consistency with Policy service data model and avoid unnecessary schema divergence. No change needed.

2. **Sequential Number Pattern**: Use "LCM-{year}-{sequence:D6}" (LifeCycle Management acronym) OR "PLM-{year}-{sequence:D6}" (Policy LifeCycle Management) OR "KWG-{year}-{sequence:D6}" (keep existing for customer continuity)? Recommendation: Use "LCM-" to clearly differentiate from Policy service's "KWG-" prefix while maintaining format compatibility.

3. **Counter Document Naming**: Rename from "PolicyNumberCounter-{year}" to "LifeCycleNumberCounter-{year}" OR "LCMNumberCounter-{year}"? Recommendation: Use "LifeCycleNumberCounter-{year}" for clarity and consistency with domain model.

4. **API Port Assignment**: Use 7079 (next odd port after 7077) for API and 7080 for Endpoint.In OR follow different port convention? Recommendation: Use 7079 for API, 7080 for Endpoint.In (mirrors Policy's +1 pattern).

5. **Event Naming Strategy**: 
   - Option A: Rename all events (PolicyBound → LifeCycleInitiated, PolicyIssued → LifeCycleIssued, PolicyCancelled → LifeCycleCancelled)
   - Option B: Keep event names for downstream compatibility (PolicyIssued remains PolicyIssued even from new service)
   - Recommendation: Option A for internal events (PolicyBound, PolicyCancelled, PolicyReinstated), Option B for PublicContracts events (keep PolicyIssued name) to minimize breaking changes in billing service during transition.

6. **Status Terminology**: Keep Bound/Issued/Active/Cancelled/Expired/Lapsed/Reinstated status values OR rename for lifecycle context (Initiated/Issued/Active/Terminated/Expired/Suspended/Reactivated)? Recommendation: Keep existing status values to maintain business domain language consistency and reduce testing surface area.

7. **Premium Calculation Logic**: Retain all Policy premium calculation rules (unearned premium = remaining days / total days × premium) OR adjust for LifeCycle domain? Recommendation: Retain all calculation rules and validation logic - this is core business logic that should not change during service cloning.

8. **Cross-Service Integration During Transition**:
   - QuoteAccepted routing: Should Rating & Underwriting dual-publish to both Policy and PolicyLifeCycleMgt endpoints during transition?
   - PolicyIssued consumption: Should billing service subscribe to both PolicyIssued and LifeCycleIssued events during transition?
   - Recommendation: Implement message routing in NServiceBus to gradually shift traffic - start with 0% to new service, ramp to 100%, then deprecate old endpoint. Use NServiceBus message distribution patterns rather than dual-publishing at source.

9. **Data Migration Strategy**: Should existing Policy data be migrated to PolicyLifeCycleMgt container OR start fresh with new policies only? Recommendation: Start fresh - new policies go to PolicyLifeCycleMgt, existing policies remain in Policy service until natural expiration. This avoids complex data migration and maintains clear audit trail.

10. **Sequential Numbering Continuity**: Should PolicyLifeCycleMgt continue the sequence from Policy service (read max KWG number, start LCM at next number) OR start fresh at LCM-2026-000001? Recommendation: Start fresh at LCM-2026-000001 - distinct prefix eliminates ambiguity and clearly identifies which service owns which policy for support/operations teams.

11. **Traffic Cutover Strategy**: Should cutover be gradual (0% → 10% → 50% → 100%) OR big-bang switch? Recommendation: Gradual with canary deployment - route new quote acceptances to PolicyLifeCycleMgt starting at 10%, monitor error rates and business metrics, ramp up over 2-4 weeks. Use feature flags in Rating & Underwriting to control routing.

12. **Monitoring & Observability**: What additional metrics should be tracked during transition? Recommendation: Track dual metrics dashboards comparing Policy vs PolicyLifeCycleMgt for: quote acceptance rate, policy issuance time, premium calculation accuracy, cancellation/reinstatement success rates, sequential number generation gaps, and downstream billing account creation success rates.
