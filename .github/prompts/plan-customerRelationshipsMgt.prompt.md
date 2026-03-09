## Plan: Clone Customer to CustomerRelationshipsMgt

Create a new CustomerRelationshipsMgt bounded context by cloning Customer and renaming namespaces, contracts, endpoints, and domain language. Use a fresh Cosmos container, run both services in parallel with feature-flagged/partial traffic, then gradually cut over while Customer remains stable until retirement.

**Steps**
1. Discovery and baseline mapping: inventory Customer projects (Api, Domain, Infrastructure, Endpoint.In), 4 events (CustomerCreated, CustomerInformationUpdated, ContactInformationChanged, CustomerClosed), 5 API endpoints, CustomerManager with 5 methods, CustomerRepository with 6 methods, CustomerValidator, and Cosmos container usage to define the exact clone surface and rename targets. *completed - full Customer structure documented*
2. Scaffold the new service by copying Customer structure into services/customerrelationshipsmgt and creating new project files with updated RootNamespace/AssemblyName/Port (7075 → next available port). *parallel with step 3*
3. Rename domain language: update namespaces from `RiskInsure.Customer.*` to `RiskInsure.CustomerRelationshipsMgt.*`, rename event names, handler names, managers (CustomerManager → RelationshipManager), repository types (ICustomerRepository → IRelationshipRepository), API controller (CustomersController → RelationshipsController), routes (api/customers → api/relationships), and model types (Customer → Relationship). Ensure commands/events follow naming rules. *parallel with step 2*
4. Update infrastructure wiring: NServiceBus endpoint names from "RiskInsure.Customer.*" to "RiskInsure.CustomerRelationshipsMgt.*", routing configurations, appsettings keys, and Cosmos settings for a fresh container (rename container from "customer" to "customerrelationships" + retain partition key strategy `/customerId` or rename to `/relationshipId`). *depends on step 2*
5. Update domain-specific terminology: rename CustomerId generation pattern (CUST-{timestamp} → CRM-{timestamp} or REL-{timestamp}), update DTOs (CreateCustomerRequest → CreateRelationshipRequest, CustomerResponse → RelationshipResponse), update status terminology if needed (Active/Inactive/Suspended/Closed), and validation logic references. *depends on step 3*
6. Register in solution and documentation: add new projects to RiskInsure.slnx (Api, Domain, Infrastructure, Endpoint.In, Unit.Tests, Integration.Tests) and create/update docs under services/customerrelationshipsmgt/docs (overview.md, business/, technical/) to reflect CustomerRelationshipsMgt domain language and standards. *depends on step 2*
7. Update test structure: rename test namespaces, update Playwright config to new API port, update test data and assertions to use new terminology (customer → relationship), configure Integration.Tests to point to new API endpoint. *depends on step 5*
8. Parallel-run readiness: add feature-flag/config gate in API to direct traffic to new service; keep Customer stable and set partial traffic routing strategy. *depends on step 4*
9. Data independence verification: ensure new service uses its own Cosmos container and does not read Customer data; confirm idempotency keys work correctly, logging contains new correlation identifiers (relationshipId), and ETag-based optimistic concurrency control functions. *depends on step 4*
10. Cutover plan: define traffic ramp steps (0% → 10% → 50% → 100%), monitoring signals (error rates, latency, event publishing metrics), rollback criteria (error threshold, data corruption detection), and Customer service retirement timeline. *depends on step 8*

**Relevant files**

**Source (Customer service)**:
- services/customer/src/Api/Program.cs — API wiring, NServiceBus send-only setup, Cosmos initialization, Serilog config
- services/customer/src/Api/Controllers/CustomersController.cs — 5 endpoints (POST /, GET /{id}, PUT /{id}, POST /{id}/change-email, DELETE /{id})
- services/customer/src/Domain/Contracts/Events/ — 4 events: CustomerCreated, CustomerInformationUpdated, ContactInformationChanged, CustomerClosed
- services/customer/src/Domain/Managers/CustomerManager.cs — 5 methods: CreateCustomerAsync, GetCustomerAsync, UpdateCustomerAsync, CloseCustomerAsync, IsEmailUniqueAsync
- services/customer/src/Domain/Repositories/ — ICustomerRepository interface + CustomerRepository implementation (6 methods)
- services/customer/src/Domain/Validation/CustomerValidator.cs — Email, ZipCode, Age validation rules
- services/customer/src/Domain/Models/ — Customer.cs (with partition key customerId), Address.cs
- services/customer/src/Infrastructure/CosmosDbInitializer.cs — Container "customer" with partition key "/customerId"
- services/customer/src/Infrastructure/NServiceBusConfigurationExtensions.cs — Unified configuration pattern
- services/customer/src/Endpoint.In/Program.cs — Message handler host wiring
- services/customer/test/Unit.Tests/ — xUnit tests for CustomerManager, CustomerValidator (19 tests total)
- services/customer/test/Integration.Tests/ — Playwright tests (Node.js/TypeScript)
- services/customer/docs/ — overview.md, business/customer-management.md, technical/customer-technical-spec.md

**Target (new location)**:
- services/customerrelationshipsmgt — complete cloned structure

**Global references**:
- RiskInsure.slnx — add 6 new projects (Api, Domain, Infrastructure, Endpoint.In, Unit.Tests, Integration.Tests)
- copilot-instructions/naming-conventions.md — naming rules for commands/events/classes
- copilot-instructions/project-structure.md — required layer structure
- .specify/memory/constitution.md — non-negotiable architecture rules

**Verification**
1. Build new service projects: `dotnet build RiskInsure.slnx` (should compile without errors)
2. Run unit tests: `dotnet test services/customerrelationshipsmgt/test/Unit.Tests/Unit.Tests.csproj` (all 19 tests should pass with updated assertions)
3. Start Cosmos DB Emulator: verify new container "customerrelationships" is created automatically
4. Run API locally: `dotnet run --project services/customerrelationshipsmgt/src/Api/Api.csproj` (should start on configured port)
5. Run Endpoint.In locally: `dotnet run --project services/customerrelationshipsmgt/src/Endpoint.In/Endpoint.In.csproj` (should connect to RabbitMQ)
6. Exercise API endpoints via Postman/curl: POST create relationship, GET retrieve, PUT update, POST change-email, DELETE close account
7. Verify events published: confirm RelationshipCreated, RelationshipInformationUpdated, RelationshipClosed events appear in RabbitMQ management UI (localhost:15672)
8. Run Playwright integration tests: `cd services/customerrelationshipsmgt/test/Integration.Tests && npm test` (update port in playwright.config.ts first)
9. Confirm no cross-service data access: query both "customer" and "customerrelationships" containers to verify data isolation
10. Verify logging: check logs contain new correlation identifiers (relationshipId), proper log levels (Information/Warning/Error), and structured logging format

**Decisions**
- Rename/reshape all contracts for CustomerRelationshipsMgt domain language (Customer → Relationship terminology)
- Use a fresh Cosmos container "customerrelationships" (no shared data with original Customer service)
- Retain partition key strategy `/customerId` initially (can rename to `/relationshipId` in step 5 if preferred)
- Change CustomerId generation from "CUST-{timestamp}" to "CRM-{timestamp}" or "REL-{timestamp}"
- Cutover via parallel run with feature-flagged/partial traffic routing
- API port: determine next available port (7075 currently used by Customer, suggest 7077 or next in sequence)
- No external deadline constraints - prioritize correctness over speed
- Maintain backward compatibility during transition period (Customer service remains operational)

**Further Considerations**
1. **Partition Key Naming**: Keep `/customerId` for consistency with Customer service, OR rename to `/relationshipId` for domain clarity? Recommendation: Keep `/customerId` initially to minimize changes, rename in a separate refactoring pass if needed.
2. **Identity Generation Pattern**: Use "CRM-{timestamp}" (CustomerRelationshipsMgt acronym) OR "REL-{timestamp}" (Relationship object)? Recommendation: Use "CRM-" to clearly differentiate from Customer service's "CUST-" prefix.
3. **API Port Assignment**: Use 7077 (next odd port after 7075) OR follow different port convention? Recommendation: Use 7077 for API, 7078 for Endpoint.In (if needed).
4. **Event Scope**: Should any events move to PublicContracts for cross-service integration? Recommendation: Keep all events internal (Domain/Contracts/Events) initially, promote to PublicContracts only when other services need to subscribe.
5. **Validation Rules**: Retain all Customer validation rules (email format, zipCode 5-digit, age 18+) OR adjust for CustomerRelationshipsMgt domain? Recommendation: Retain all rules initially unless business requirements differ.
6. **Status Terminology**: Keep Active/Inactive/Suspended/Closed status values OR rename for relationship context? Recommendation: Keep existing status values unless domain expert specifies different relationship lifecycle states.
7. **Traffic Cutover Strategy**: Should cutover be gradual (0% → 10% → 50% → 100%) OR big-bang switch? Recommendation: Gradual with traffic shadowing (dual-write) to verify data consistency before full cutover.
