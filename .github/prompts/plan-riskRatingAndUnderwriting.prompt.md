## Plan: Clone RatingAndUnderwriting to RiskRatingAndUnderwriting

Create a new RiskRatingAndUnderwriting bounded context by cloning RatingAndUnderwriting and renaming namespaces, contracts, endpoints, domain language, and models. Use a fresh Cosmos container, run both services in parallel with feature-flagged/partial traffic, then gradually cut over while RatingAndUnderwriting remains stable until retirement.

**TL;DR**: RatingAndUnderwriting is a quote lifecycle service with rating engines, underwriting rules, and premium calculations. Clone it to RiskRatingAndUnderwriting by renaming namespaces (RatingAndUnderwriting → RiskRatingAndUnderwriting), updating domain language (Quote → Assessment/Evaluation terminology if desired, or keep Quote), renaming managers/repositories/services, updating API routes (/api/quotes → /api/assessments or /api/risk-quotes), and using a fresh Cosmos container. Run parallel with traffic gradually shifted from old service to new, monitoring for identical underwriting decisions.

**Steps**

1. Discovery and baseline mapping: inventory RatingAndUnderwriting projects (Api, Domain, Infrastructure, Endpoint.In), 4 domain events (QuoteStarted, QuoteCalculated, QuoteDeclined, UnderwritingSubmitted), 2 core managers (QuoteManager with 5 methods), 1 repository (IQuoteRepository with CRUD + customer lookup), 2 engines (RatingEngine, UnderwritingEngine), Quote model with partition key strategy, and Cosmos container usage to define the exact clone surface and rename targets. *completed - full RatingAndUnderwriting structure documented*

2. Scaffold the new service by copying RatingAndUnderwriting structure into services/riskratingandunderwriting and creating new project files with updated RootNamespace/AssemblyName/Port (7079 → 7081 or per plan decision). *parallel with step 3*

3. Rename domain language: update namespaces from `RiskInsure.RatingAndUnderwriting.*` to `RiskInsure.RiskRatingAndUnderwriting.*`, rename event names (QuoteStarted → RiskQuoteStarted, QuoteCalculated → RiskQuoteCalculated, QuoteDeclined → RiskQuoteDeclined, UnderwritingSubmitted → UnderwritingRiskSubmitted), handler names, managers (QuoteManager → RiskQuoteManager or AssessmentManager), repository types (IQuoteRepository → IRiskQuoteRepository), API controller (QuotesController → RiskQuotesController), routes (/api/quotes → /api/risk-quotes), and model types (Quote → RiskQuote). Ensure commands/events follow C# naming rules. *parallel with step 2*

4. Update infrastructure wiring: NServiceBus endpoint names from "RiskInsure.RatingAndUnderwriting.*" to "RiskInsure.RiskRatingAndUnderwriting.*", routing configurations, appsettings keys, and **use modern Cosmos DB initialization pattern** (reference services/ratingandunderwriting/src/Api/Program.cs): configuration-driven container names via `CosmosDb:DatabaseName` and `CosmosDb:ContainerName` in appsettings, connection string validation, comprehensive `CosmosClientOptions` with `ConnectionMode.Direct`, 10s request timeout, 3 retry attempts with 5s backoff, consistent `JsonSerializerOptions` (CamelCase + NullIgnore + EnumConverter), and static `EnsureDbAndContainerAsync()` method. Rename container from "ratingunderwriting" to "riskratingandunderwriting", keeping partition key `/quoteId` unchanged. *depends on step 2*

5. Update domain-specific terminology: rename internal model references (Quote → RiskQuote if chosen, or keep Quote for domain continuity), update DTOs (QuoteResponse → RiskQuoteResponse, StartQuoteRequest → StartRiskQuoteRequest, SubmitUnderwritingRequest → SubmitRiskUnderwritingRequest), maintain underwriting class terminology and status values (Draft/Quoted/Submitted/Approved/Declined/Expired), RatingEngine → RiskRatingEngine, UnderwritingEngine → RiskUnderwritingEngine. *depends on step 3*

6. Register in solution and documentation: add new projects to RiskInsure.slnx (Api, Domain, Infrastructure, Endpoint.In, Unit.Tests, Integration.Tests) and create/update docs under services/riskratingandunderwriting/docs (overview.md, business/, technical/) to reflect RiskRatingAndUnderwriting domain language and rating/underwriting standards. *depends on step 2*

7. Update test structure: rename test namespaces (QuoteManagerTests → RiskQuoteManagerTests), update Playwright config to new API port, update test data and assertions to use new terminology (quote → riskquote, quoteId → riskquoteId), configure Integration.Tests to point to new API endpoint, verify rating engine test cases produce identical output. *depends on step 5*

8. Update cross-service integration: review PublicContracts usage - if QuoteAccepted event is consumed by other services (Policy, PolicyLifeCycleMgt), decide if new events (RiskQuoteAccepted) should be published or if old QuoteAccepted events remain authoritative. Verify downstream services subscribe to correct events during transition. *depends on step 4*

9. Parallel-run readiness: add feature-flag/config gate in API to direct traffic to new service; keep RatingAndUnderwriting stable and set partial traffic routing strategy. Coordinate with Policy and PolicyLifeCycleMgt services to dual-route QuoteAccepted subscriptions if needed during transition. *depends on step 8*

10. Data independence verification: ensure new service uses its own Cosmos container and does not read RatingAndUnderwriting data; confirm idempotency keys work correctly (quoteId-based deduplication), logging contains new correlation identifiers (riskquoteId), rating engine output consistency (identical underwriting classes and premium calculations for same inputs), and partition key `/quoteId` isolation between containers. *depends on step 4*

11. Cutover plan: define traffic ramp steps (0% → 10% → 50% → 100%), monitoring signals (error rates, latency, rating calculation accuracy, underwriting decision consistency between old and new service, premium variance <0.01%), rollback criteria (error threshold, rating divergence, policy binding mismatches), and RatingAndUnderwriting service retirement timeline. Coordinate cutover timing with downstream Policy services. *depends on step 9*

**Relevant files**

**Source (RatingAndUnderwriting service)**:
- services/ratingandunderwriting/src/Api/Program.cs — **REFERENCE TEMPLATE FOR MODERN COSMOS DB PATTERN**: API wiring, NServiceBus send-only setup, production-ready Cosmos initialization (configuration-driven container names, connection validation, comprehensive options, Direct mode, timeout, retries, consistent JSON serialization, static method), Serilog config (port 7079)
- services/ratingandunderwriting/src/Api/Controllers/QuotesController.cs — 5 endpoints (POST /start, POST /{id}/submit-underwriting, POST /{id}/accept, GET /{id}, GET /customers/{id}/quotes)
- services/ratingandunderwriting/src/Domain/Contracts/Events/ — 4 events: QuoteStarted, QuoteCalculated, QuoteDeclined, UnderwritingSubmitted
- services/ratingandunderwriting/src/Domain/Managers/QuoteManager.cs — 5 methods: StartQuoteAsync, SubmitUnderwritingAsync, AcceptQuoteAsync, GetQuoteAsync, GetCustomerQuotesAsync
- services/ratingandunderwriting/src/Domain/Repositories/ — IQuoteRepository interface + QuoteRepository implementation (CRUD operations, GetByCustomerIdAsync)
- services/ratingandunderwriting/src/Domain/Services/RatingEngine.cs — Rating calculation logic based on property characteristics
- services/ratingandunderwriting/src/Domain/Services/UnderwritingEngine.cs — Underwriting decision logic (approve/decline based on risk factors)
- services/ratingandunderwriting/src/Domain/Models/Quote.cs — Aggregate root with partition key quoteId, status state machine, premium calculations
- services/ratingandunderwriting/src/Infrastructure/CosmosDbInitializer.cs — Modern static method pattern: `EnsureDbAndContainerAsync()`, container "ratingunderwriting" with partition key "/quoteId", database "RiskInsure", configuration-driven from appsettings
- services/ratingandunderwriting/src/Infrastructure/NServiceBusConfigurationExtensions.cs — Unified configuration pattern (reuse as-is)
- services/ratingandunderwriting/src/Endpoint.In/Program.cs — Message handler host wiring (no explicit port in docker-compose)
- services/ratingandunderwriting/test/Unit.Tests/ — xUnit tests for QuoteManager, RatingEngine, UnderwritingEngine
- services/ratingandunderwriting/test/Integration.Tests/ — Playwright tests (Node.js/TypeScript)
- services/ratingandunderwriting/docs/ — overview.md, business/, technical/ documentation

**Target (new location)**:
- services/riskratingandunderwriting — complete cloned structure

**Global references**:
- RiskInsure.slnx — add 6 new projects (Api, Domain, Infrastructure, Endpoint.In, Unit.Tests, Integration.Tests)
- platform/RiskInsure.PublicContracts/ — review QuoteAccepted event usage and decide on new event names if applicable
- services/policy/src/Endpoint.In/Handlers/ — may consume QuoteAccepted (coordinate cutover)
- services/policylifecyclemgt/src/Endpoint.In/Handlers/ — may consume QuoteAccepted (coordinate cutover)
- copilot-instructions/naming-conventions.md — naming rules for commands/events/classes
- copilot-instructions/project-structure.md — required layer structure
- .specify/memory/constitution.md — non-negotiable architecture rules

**Verification**

1. Build new service projects: `dotnet build RiskInsure.slnx` (should compile without errors)
2. Run unit tests: `dotnet test services/riskratingandunderwriting/test/Unit.Tests/Unit.Tests.csproj` (all tests should pass with updated assertions)
3. Start Cosmos DB Emulator: verify new container "riskratingandunderwriting" is created automatically with partition key "/quoteId"
4. **Verify appsettings configuration**: Check `appsettings.Development.json` contains required Cosmos settings (`CosmosDb:DatabaseName=RiskInsure`, `CosmosDb:ContainerName=riskratingandunderwriting`) and connection string (`ConnectionStrings:CosmosDb`) - these drive the configuration-based initialization pattern
4b. Run API locally: `dotnet run --project services/riskratingandunderwriting/src/Api/Api.csproj` (should start on configured port, 7081 if using plan port, logs should show configuration-driven Cosmos initialization)
5. Run Endpoint.In locally: `dotnet run --project services/riskratingandunderwriting/src/Endpoint.In/Endpoint.In.csproj` (should connect to RabbitMQ if handlers configured)
6. Exercise API endpoints via Postman/curl: POST /start quote, POST /{id}/submit-underwriting, POST /{id}/accept, GET retrieve by ID, GET customer quotes
7. Verify events published: confirm RiskQuoteStarted, RiskQuoteCalculated, RiskQuoteDeclined, UnderwritingRiskSubmitted events appear in RabbitMQ management UI (localhost:15672)
8. Test rating engine consistency: submit identical underwriting data to both old and new service, verify underwriting class and premium match exactly
9. Confirm no cross-service data access: query both "ratingunderwriting" and "riskratingandunderwriting" containers to verify data isolation
10. Test premium calculation accuracy: calculate premiums for multiple property types and verify calculations match original service
11. Verify logging: check logs contain new correlation identifiers (riskquoteId), proper log levels (Information/Warning/Error), structured logging format, and tracing context
12. Run Playwright integration tests: `cd services/riskratingandunderwriting/test/Integration.Tests && npm test` (update port in playwright.config.ts first)
13. Test status machine transitions: verify Draft → Submitted → Approved/Declined → Expired flow with proper validations
14. Cross-service integration: send QuoteAccepted to Policy service, verify it processes correctly from both old and new rating services

**Decisions**

- Rename/reshape all contracts for RiskRatingAndUnderwriting domain language (Quote → RiskQuote terminology if chosen, or keep Quote for cross-service message continuity)
- Use a fresh Cosmos container "riskratingandunderwriting" (no shared data with original RatingAndUnderwriting service)
- **Keep partition key unchanged as `/quoteId`** (business domain independent of service)
- Rename managers/repositories/services to include "Risk" prefix (RiskQuoteManager, IRiskQuoteRepository, RiskRatingEngine, RiskUnderwritingEngine)
- Rename API controller from QuotesController to RiskQuotesController
- Change API routes from /api/quotes to /api/risk-quotes
- **Keep Cosmos partition key `/quoteId` unchanged** (independent of service name)
- Cutover via parallel run with feature-flagged/partial traffic routing
- API port: determine next available port (7079 currently used by RatingAndUnderwriting, suggest 7081 for API, no explicit Endpoint port needed)
- Coordinate with Policy and PolicyLifeCycleMgt services to ensure QuoteAccepted subscriptions work during transition
- No external deadline constraints - prioritize correctness and rating consistency over speed
- Maintain backward compatibility during transition period (RatingAndUnderwriting service remains operational)

**Further Considerations**

0. **Cosmos DB Initialization Pattern Requirement**: RiskRatingAndUnderwriting MUST use the modern production-ready pattern (not the basic pattern). Reference services/ratingandunderwriting/src/Api/Program.cs for the template. Key requirements: (1) Configuration-driven container names from appsettings (CosmosDb:DatabaseName, CosmosDb:ContainerName), (2) Connection string validation with error throw, (3) Comprehensive CosmosClientOptions (Direct mode for production networking, 10s request timeout, 3 retry attempts with 5s backoff), (4) Consistent JsonSerializerOptions with CamelCase naming, null ignoring, and enum converters, (5) Static method call pattern EnsureDbAndContainerAsync(). Do NOT use hardcoded container names or minimal CosmosClientOptions. This pattern ensures reliability, testability, and production readiness from day one.

1. **Partition Key Naming**: Keep `/quoteId` unchanged in new container. The partition key is determined by the business domain (quote entity) and should be independent of service naming. No change needed from original RatingAndUnderwriting service.

2. **Model Naming Strategy**: 
   - Option A: Rename Quote → RiskQuote throughout (domain clarity, but more verbose)
   - Option B: Keep Quote model name, only rename managers/repositories/services to include "Risk" (less intrusive, maintains cross-service message compatibility)
   - Recommendation: Option B - reduces surface area of changes and keeps quote concept consistent across services.

3. **Event Naming for Cross-Service Integration**: 
   - Option A: Rename all events locally (QuoteStarted → RiskQuoteStarted) but keep PublicContracts events (QuoteAccepted) unchanged
   - Option B: Rename all events including PublicContracts to maintain consistency
   - Recommendation: Option A - minimize impact on downstream Policy services during transition.

4. **API Route Prefix**: Use /api/risk-quotes (explicit "risk" namespace) OR /api/quotes (keep existing concept) OR /api/assessments (if rebranding quote as assessment)? Recommendation: /api/risk-quotes for clear differentiation from old service during parallel run.

5. **Rating Engine Compatibility**: Should rating calculations be identical to RatingAndUnderwriting service OR allow for improved logic in new service? Recommendation: Identical calculations during transition period (measure any variance in premium calculations <0.01% due to rounding). After cutover stabilizes, may improve logic in future versions.

6. **Underwriting Status Terminology**: Keep Draft/Quoted/Submitted/Approved/Declined/Expired statuses OR rename (AssessmentDraft/RiskSubmitted)? Recommendation: Keep existing statuses - business domain language should be consistent.

7. **Error Handling During Transition**: How should services handle QuoteAccepted messages from both old and new services? Recommendation: Implement message routing layer in NServiceBus to gradually shift subscriptions - start with 0% to new service, ramp to 100%, then deprecate old endpoint.

8. **Data Migration Strategy**: Should existing quote data be migrated to RiskRatingAndUnderwriting container OR start fresh with new quotes only? Recommendation: Start fresh - new quotes go to RiskRatingAndUnderwriting container, existing quotes remain in RatingAndUnderwriting until natural expiration. Avoids complex data migration.

9. **Rating Calculation Variance Monitoring**: During parallel run, what premium variance threshold triggers rollback? Recommendation: <0.01% variance (rounding differences acceptable), >0.1% variance triggers incident review.

10. **Cross-Service Coordination Timeline**: Should cutover happen simultaneously with Policy/PolicyLifeCycleMgt services OR independently? Recommendation: Independent cutover for RiskRatingAndUnderwriting; only coordinate timing to avoid overlapping major deployments.

11. **Traffic Cutover Strategy**: Should cutover be gradual (0% → 10% → 50% → 100%) OR big-bang switch? Recommendation: Gradual with canary deployment - route new quote requests to RiskRatingAndUnderwriting starting at 10%, monitor underwriting consistency, ramp up over 1-2 weeks.

12. **Monitoring & Observability**: What additional metrics should be tracked during transition? Recommendation: Track dual dashboards comparing RatingAndUnderwriting vs RiskRatingAndUnderwriting for: quote acceptance rate, underwriting approval rate, premium calculation accuracy, average quote time, error rates, and downstream policy binding success rates.
