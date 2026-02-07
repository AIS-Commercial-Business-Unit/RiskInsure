# Domain Scaffolder Prompt

**Purpose**: Create initial documentation structure for new domains (bounded contexts) from DDD specification files.

**Agent**: [domain-scaffolder.agent.md](../agents/domain-scaffolder.agent.md)

---

## Quick Start

### Auto-Discovery from DDD Specs

```
@workspace Scaffold domains from DDD specifications in services/.rawservice/

Use domain-scaffolder agent to discover and process all DDD specification files, generating complete business and technical documentation for each context.
```

### Explicit Domain Names

```
@workspace Add 3 domains to the solution: NsbSales, NsbBilling, NsbShipping

Use domain-scaffolder agent to parse DDD specifications and create complete documentation structure.
```

---

## Full Invocation

### Process DDD Specifications (Recommended)

```
@workspace I have DDD specification files in services/.rawservice/ for a new feature set.

**Task**: Parse DDD specifications and generate RiskInsure documentation

**DDD Files**:
- Sales_Systems_single_context_final.md
- Billing_Systems_single_context_final.md
- Shipping_Systems_single_context_final.md

Please use the domain-scaffolder agent to:
1. Parse each DDD specification file
2. Extract Contexts, Commands, Events, Policies, and External Subscriptions
3. Map DDD concepts to RiskInsure architecture patterns
4. Generate business requirements documentation
5. Generate technical specifications with:
   - API endpoints (for domains with Views)
   - Message contracts (Commands and Events)
   - Message handlers (for External Subscriptions)
   - Domain managers and methods
6. Document event flows and integration points
7. Create README with domain overview

**Expected Output**: Complete documentation structure for NsbSales, NsbBilling, and NsbShipping domains ready for implementation via domain-builder agent.
```

---

## Usage Patterns

### Pattern 1: DDD Specification Processing (Primary Use Case)

```
@workspace We have DDD specifications for a new order processing system.

**DDD Specs Location**: services/.rawservice/
- Sales_Systems_single_context_final.md (entry point with API)
- Billing_Systems_single_context_final.md (message-driven)
- Shipping_Systems_single_context_final.md (message-driven)

Use domain-scaffolder agent to:
1. Parse all DDD files
2. Understand event flow relationships:
   - Sales publishes OrderPlaced
   - Billing subscribes to OrderPlaced, publishes OrderBilled
   - Shipping subscribes to OrderPlaced and OrderBilled
3. Generate documentation showing:
   - Which domains have APIs (Sales only)
   - Event contracts in PublicContracts
   - Message handlers in Endpoint.In
   - Integration points between domains

This is the standard DDD-to-RiskInsure translation workflow.
```

### Pattern 2: Manual Domain Creation (Legacy)

```
@workspace Create initial structure for the Customer domain.

**Domain**: Customer
**Description**: Manages customer identity, contact information, and account relationships.

Use domain-scaffolder agent to create:
- Directory structure in services/customer/
- README.md with domain overview
- Documentation templates (overview, business, technical)

Note: This creates placeholder documentation only. For DDD-based generation, provide specification files.
```

### Pattern 3: Verify DDD Parsing

```
@workspace Review DDD specification parsing for quality assurance.

**Files**: services/.rawservice/*.md

Use domain-scaffolder agent to:
1. Parse all DDD specifications
2. Show extracted domain model for each:
   - Context name and type (API vs message-driven)
   - Commands with parameters
   - Events with Published flag
   - External Subscriptions (handlers)
   - Event flow relationships
3. Identify any parsing issues or ambiguities
4. Generate documentation

Review output BEFORE using domain-builder to ensure correct interpretation.
```

---

## DDD Specification Requirements

### Expected File Format

```markdown
# **Contract: {ContractName} Systems**

## **Context: {ContextName}**
  - *Context Meta*
    - Description: {Description}

### **Context Elements for {ContextName}**

##### **Modules for {ContextName}**
- **Module: {ModuleName}**
  - **Views for Module** (indicates API needed)
  - **Policies for Module** (indicates handlers)
  - **Units of Work for Module**
    - **Command: {CommandName}**
      - *Parameters*
    - **Event: {EventName}**
      - *Event Meta*
        - Published: Yes/No
      - *Data Elements*

#### **Context Relationships**
##### **Policy Relationships**
  - *External Subscriptions*: {Context}.{EventName}
```

### Key Elements
- **Context**: Domain name
- **Views**: Indicates API layer needed
- **Commands**: Internal operations
- **Events (Published: Yes)**: Public events in PublicContracts
- **External Subscriptions**: Message handlers to create
- **Units of Work**: Manager methods

---

## Expected Output

After running the agent with DDD specifications, you should see:

```
📊 Parsed DDD Specifications:

✅ Sales (Entry Point)
   Source: services/.rawservice/Sales_Systems_single_context_final.md
   Type: API-driven (has Views)
   Commands: PlaceOrder
   Events Published: OrderPlaced
   Events Subscribed: (none)
   API: POST /api/orders

✅ Billing (Message-Driven)
   Source: services/.rawservice/Billing_Systems_single_context_final.md
   Type: Message-driven only
   Commands: BillOrder
   Events Published: OrderBilled
   Events Subscribed: Sales.OrderPlaced
   Handlers: OrderPlacedHandler

✅ Shipping (Message-Driven)
   Source: services/.rawservice/Shipping_Systems_single_context_final.md
   Type: Message-driven only
   Commands: ReserveInventory, ShipOrder
   Events Published: InventoryReserved, OrderShipped
   Events Subscribed: Sales.OrderPlaced, Billing.OrderBilled
   Handlers: OrderPlacedHandler, OrderBilledHandler

📁 Created Documentation:

services/nsbsales/
├── README.md (with event flow)
└── docs/
    ├── overview.md (with integration points)
    ├── business/
    │   └── nsbsales-management.md (use cases from DDD)
    └── technical/
        └── nsbsales-technical-spec.md (API + contracts)

services/nsbbilling/
├── README.md
└── docs/
    ├── overview.md
    ├── business/
    │   └── nsbbilling-management.md
    └── technical/
        └── nsbbilling-technical-spec.md (handlers + contracts)

services/nsbshipping/
├── README.md
└── docs/
    ├── overview.md
    ├── business/
    │   └── nsbshipping-management.md
    └── technical/
        └── nsbshipping-technical-spec.md (handlers + contracts)

🔄 Event Flow:
  POST /api/orders (Sales)
    ↓ PlaceOrder command
    ↓ OrderPlaced event (PublicContracts)
    ├──→ Billing.OrderPlacedHandler
    │      ↓ BillOrder command
    │      ↓ OrderBilled event (PublicContracts)
    │      └──→ Shipping.OrderBilledHandler
    │             ↓ ShipOrder command
    │             └──→ OrderShipped event
    │
    └──→ Shipping.OrderPlacedHandler
           ↓ ReserveInventory command
           └──→ InventoryReserved event

📝 Next Steps:
1. Review generated documentation for accuracy
2. Validate event contracts match across domains
3. Use domain-builder agent to generate implementation:
   @workspace Build NsbSales, NsbBilling, NsbShipping domains
```

---

## Validation

After scaffolding, verify:

### Structure
- [ ] Each domain has `services/{domain}/` directory
- [ ] README.md exists with domain name and integration summary
- [ ] `docs/overview.md` has event flow documentation
- [ ] `docs/business/{domain}-management.md` has use cases from DDD
- [ ] `docs/technical/{domain}-technical-spec.md` has contracts and handlers

### Content Quality
- [ ] API endpoints only for domains with Views
- [ ] Published events listed in PublicContracts section
- [ ] External Subscriptions mapped to handlers
- [ ] Event flow shows relationships correctly
- [ ] Business use cases reflect Units of Work
- [ ] Technical specs include all Commands and Events

### DDD Mapping
- [ ] Context names correctly extracted
- [ ] Commands mapped with parameters
- [ ] Events classified (Public vs Internal)
- [ ] Handlers mapped to External Subscriptions
- [ ] Correlation IDs identified (OrderID, etc.)

---

## Integration with Other Agents

### Complete Workflow: DDD → Implementation

1. **Domain Scaffolder**: Generate documentation from DDD specs
   ```
   @workspace Process DDD specifications in .rawservice/ using domain-scaffolder
   ```

2. **Review & Refine**: Validate generated documentation
   - Check event contracts
   - Verify handler mappings
   - Confirm API design

3. **Domain Builder**: Generate implementation
   ```
   @workspace Build NsbSales domain using domain-builder agent
   ```

4. **E2E Contract Verifier**: Validate cross-domain contracts
   ```
   @workspace Verify event contracts between Sales, Billing, Shipping
   ```

5. **Local Smoke Test**: Test integrated system
   ```
   @workspace Run smoke tests for order processing flow
   ```

---

## Common Scenarios

### Scenario 1: New Feature Set from DDD Workshop

```
@workspace We completed an Event Storming workshop and have DDD specifications.

**Location**: services/.rawservice/
- OrderManagement_Systems_single_context_final.md
- InventoryManagement_Systems_single_context_final.md
- PaymentProcessing_Systems_single_context_final.md

Use domain-scaffolder to:
1. Parse all three DDD specifications
2. Identify event flow between contexts
3. Generate RiskInsure documentation
4. Highlight integration points

This will help us transition from DDD design to implementation.
```

### Scenario 2: Validate DDD Interpretation

```
@workspace Before we start coding, validate our DDD interpretation.

**DDD Specs**: services/.rawservice/Sales_Systems_*.md, Billing_Systems_*.md

Use domain-scaffolder to parse and show:
1. Extracted domain model (Commands, Events, Handlers)
2. Event flow relationships
3. API vs message-driven classification
4. Expected integration patterns

We want to confirm the agent understands the DDD format correctly.
```

### Scenario 3: Simplified Test Domains

```
@workspace We have 3 simplified test domains to validate the generation pipeline.

**Purpose**: Test run of DDD-to-RiskInsure translation

**Domains**: NsbSales, NsbBilling, NsbShipping
**Specs**: services/.rawservice/*.md

Use domain-scaffolder to generate docs. These are intentionally simple to validate the process before applying to complex production domains.
```

---

## Tips

1. **DDD File Naming**: Must be `{Context}_Systems_single_context_final.md`
2. **Location**: Place in `services/.rawservice/` directory
3. **Event Flow**: Agent automatically extracts from External Subscriptions
4. **API Detection**: Only domains with Views get API layers
5. **Correlation IDs**: Agent identifies shared parameters (OrderID, CustomerId)
6. **Iterate**: Review generated docs before using domain-builder

---

## Troubleshooting

### "DDD file not found"
- Check file is in `services/.rawservice/`
- Verify filename format: `{Context}_Systems_single_context_final.md`

### "No API generated"
- Domain may not have Views (message-driven by design)
- This is correct for Billing, Shipping (react to events)

### "Events not mapping"
- Verify `Published: Yes` in Event Meta
- Check External Subscriptions format: `{Context}.{EventName}`

### "Handler unclear"
- Ensure Policy has External Subscriptions section
- Verify Policy has Command section

---

## Related Documentation

- [Constitution](../../copilot-instructions/constitution.md) - Architectural principles
- [Project Structure](../../copilot-instructions/project-structure.md) - Directory layout
- [Domain Builder Agent](../agents/domain-builder.agent.md) - Full implementation
- [Messaging Patterns](../../copilot-instructions/messaging-patterns.md) - Event patterns
- [E2E Contract Verifier](../agents/e2e-contract-verifier-agent.md) - Contract validation
