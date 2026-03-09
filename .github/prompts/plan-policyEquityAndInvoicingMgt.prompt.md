## Plan: Clone Billing to PolicyEquityAndInvoicingMgt

Create a new PolicyEquityAndInvoicingMgt bounded context by cloning Billing and immediately renaming namespaces, contracts, endpoints, and domain language. Use a fresh Cosmos container, run both services in parallel with feature-flagged/partial traffic, then gradually cut over while Billing remains stable until retirement.

**Steps**
1. Discovery and baseline mapping: inventory Billing projects, contracts, routes, and Cosmos container usage to define the exact clone surface and rename targets. *depends on current Billing state*
2. Scaffold the new service by copying Billing structure into services/policyequityandinvoicingmgt and creating new project files with updated RootNamespace/AssemblyName. *parallel with step 3*
3. Rename domain language: update namespaces, contract names, handler names, managers, repository types, and API routes to PolicyEquityAndInvoicingMgt terminology. Ensure commands/events follow naming rules. *parallel with step 2*
4. Update infrastructure wiring: NServiceBus endpoint names, routing, configuration keys, and Cosmos settings for a fresh container (new container name + partition key review). *depends on step 2*
5. Register in solution and documentation: add new projects to RiskInsure.slnx and create/update docs under services/policyequityandinvoicingmgt/docs to reflect domain language and standards. *depends on step 2*
6. Parallel-run readiness: add feature-flag/config gate in API to direct traffic to new service; keep Billing stable and set partial traffic routing strategy. *depends on step 3*
7. Data independence verification: ensure new service uses its own container and does not read Billing data; confirm idempotency and logging requirements. *depends on step 4*
8. Cutover plan: define traffic ramp steps, monitoring signals, and rollback criteria; retire Billing after steady-state success. *depends on step 6*

**Relevant files**
- services/billing/src/Api/Program.cs — baseline API wiring and NServiceBus send-only setup
- services/billing/src/Endpoint.In/Program.cs — endpoint host wiring and service registrations
- services/billing/src/Domain — contracts, managers, repositories to be renamed
- services/billing/src/Infrastructure — Cosmos initializer and NServiceBus configuration patterns
- services/policyequityandinvoicingmgt — target clone location
- RiskInsure.slnx — add new projects
- copilot-instructions/naming-conventions.md — naming rules for commands/events/classes
- copilot-instructions/project-structure.md — required layer structure
- .specify/memory/constitution.md — non-negotiable architecture rules

**Verification**
1. Build new service projects and solution (dotnet build RiskInsure.slnx)
2. Run API and Endpoint.In for the new service locally with fresh Cosmos container
3. Exercise key endpoints and verify events/commands flow to the new endpoint
4. Confirm no cross-service data access and required logs contain correlation identifiers

**Decisions**
- Rename/reshape contracts now for PolicyEquityAndInvoicingMgt domain language
- Use a fresh Cosmos container (no shared data)
- Cutover via parallel run with feature-flagged/partial traffic
- No external deadline constraints

**Further Considerations**
1. Determine which Billing endpoints are in-scope for PolicyEquityAndInvoicingMgt vs. to be deprecated
2. Decide whether any events should move to PublicContracts for cross-service use
3. Define traffic allocation percentages and rollback triggers for cutover
