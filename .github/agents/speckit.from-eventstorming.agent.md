# Agent: Convert EventStorming Model to Spec

**Agent ID**: speckit.from-eventstorming  
**Purpose**: Transform EventStorming output into RiskInsure feature specification  
**Input**: EventStorming model file (`*_Systems_single_context_final.md`)  
**Output**: Feature specification ready for `/speckit.plan`

---

## Workflow

When user invokes `/speckit.from-eventstorming` or provides an EventStorming model:

### Step 1: Locate EventStorming Model

**Prompt user**:
- "Which EventStorming model file? (e.g., `services/{context}/.nsb_example/Sales_Systems_single_context_final.md`)"
- OR auto-detect if only one `*_Systems_single_context_final.md` file in current context

### Step 2: Parse EventStorming Model

**Extract from model**:
1. **Contract Meta**:
   - Purpose Statement
   - Stakeholders (domain experts)
   - Version
2. **Context**:
   - Context name (bounded context)
   - Description
   - Development Type
3. **Units of Work**:
   - Name
   - Type (Transaction Script | Aggregate)
   - InvariantList
   - Commands (with parameters)
   - Events (with data elements, published flag)
4. **Policies**:
   - Name
   - Handler name
   - Parameters
   - External Subscriptions (integration events)
5. **Views/Personas**:
   - UI interactions
   - User roles

### Step 3: Create Spec from Template

**Use**: `.specify/templates/spec-template-eventstorming.md`

**Auto-populate**:
- **Overview**:
  - Feature Name: [Infer from Context name or ask user]
  - Bounded Context: [From EventStorming Context name]
  - Domain Expert(s): [From Contract Meta Stakeholders]
  - EventStorming Model: [Relative path to model file]
- **Domain Model Reference**:
  - Units of Work: [List all from model]
  - Commands: [Generate C# record stubs with refined types]
  - Events: [Generate C# record stubs with data elements]
  - Policies: [List all with trigger event → command mapping]
  - External Integrations: [From Policy External Subscriptions]
- **RiskInsure Context**:
  - Service: `services/[context-service]/` (must be an existing service root)
  - Auto-check constitution principles
- **Message Contracts Metadata**:
  - Add `MessageId`, `OccurredUtc`, `IdempotencyKey` to all command/event stubs
- **Data Strategy**:
  - Suggest partition key based on command parameters (usually first parameter)

**Prompt user for**:
- [ ] **Problem Statement**: "What business problem does this solve?"
- [ ] **Business Value**: "Why build this now?"
- [ ] **Partition Key**: "Suggested `/orderId` based on parameters. Confirm or change?"
- [ ] **Persistence Technology**: "Cosmos DB or PostgreSQL? Why?"

### Step 4: Generate Implementation Refinements Section

**Data Type Refinements**:
- Convert EventStorming syntax to C# types:
  - `{GUID}` → `Guid`
  - `{String}` → `string`
  - `{Int}` → `int`
  - `{Decimal}` → `decimal`
  - `{DateTime}` → `DateTimeOffset`

**Business Rules**:
- Extract from "Logic Details" (usually "Intuitive" or specific rules)
- Prompt user: "Are there specific business rules for Unit of Work `[Name]`?"

**Policy Logic**:
- For each Policy:
  - Trigger: [External Subscription event]
  - Action: [Command from relationship]
  - Prompt: "Any conditions before sending command?"

### Step 5: Generate Acceptance Criteria

**For each Policy**:
```gherkin
Scenario: [Policy Name]
Given: [External event preconditions]
When: [Trigger event] is received
Then: [Command] is sent
And: [Event] is published
```

**Auto-generate verification checklist**:
- [ ] Event published with correct data elements
- [ ] Handler idempotent
- [ ] Structured logs include correlation ID

### Step 6: Validate Against Constitution

**Auto-check**:
- [x] Commands use imperative names (PlaceOrder, BillOrder)
- [x] Events use past-tense names (OrderPlaced, OrderBilled)
- [ ] Single-partition strategy identified (prompt user)
- [ ] Idempotency strategy stated (prompt user)
- [ ] Test coverage thresholds noted (auto-populate)

### Step 7: Save Spec

**Default location**: `services/[context-service]/specs/###-[feature-name]/spec.md`

**Also create**: `services/[context-service]/specs/###-[feature-name]/eventstorming-model.md` (copy of original EventStorming file for versioning)

**Update**: `services/[context-service]/docs/specs/README.md` to add this feature to "Active Features"

**Creation mechanism**: Use `.specify/scripts/powershell/create-new-feature.ps1` with explicit `-ServicePath services/[context-service]`.

### Step 8: Confirm Next Steps

**Output to user**:
```
✅ Spec created: services/[context-service]/specs/###-[feature-name]/spec.md
✅ EventStorming model archived: services/[context-service]/specs/###-[feature-name]/eventstorming-model.md
✅ Updated feature index: services/[context-service]/docs/specs/README.md

Next steps:
1. Review spec for completeness
2. Run: /speckit.plan to generate technical plan
3. Run: /speckit.tasks to generate implementation checklist
4. Implement: Domain → Infrastructure → API → Endpoint
```

---

## Example Transformation

### Input: EventStorming Model

```markdown
## **Context: Sales**

### **Units of Work for Module Ordering**
- **Unit of Work: PlacingOrder**
  - **Commands for UoW PlacingOrder**
    - **Command: PlaceOrder**
      - *Parameters*
        - OrderID: {GUID}
  - **Events for UoW PlacingOrder**
    - **Event: OrderPlaced**
      - *Published: Yes*
      - *Data Elements*
        - OrderID: {GUID}
```

### Output: Feature Spec

```markdown
## Domain Model Reference

**EventStorming Model**: [`services/sales/.nsb_example/Sales_Systems_single_context_final.md`](../../services/sales/.nsb_example/Sales_Systems_single_context_final.md)

### Commands

```csharp
namespace RiskInsure.Sales.Domain.Contracts;

public record PlaceOrder(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    Guid OrderId,
    string CustomerId,  // Added - inferred from context
    string IdempotencyKey
);
```

### Events

```csharp
namespace RiskInsure.Sales.Domain.Contracts;

public record OrderPlaced(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    Guid OrderId,
    string CustomerId,
    DateTimeOffset PlacedUtc,
    string IdempotencyKey
);
```

### Policies

- **MustPlaceOrder**: When user requests order → Send `PlaceOrder` command → Publish `OrderPlaced` event
```

---

## Anti-Patterns to Avoid

❌ **Don't rewrite requirements** - The EventStorming model IS the requirements  
✅ **Do add technical refinements** - Types, persistence, error handling

❌ **Don't skip validation** - Confirm partition key, idempotency, persistence choice  
✅ **Do prompt domain experts** - If business rules unclear

❌ **Don't lose traceability** - Always link back to EventStorming model  
✅ **Do archive model** - Copy into spec folder for versioning

---

## Quality Checks

Before finalizing spec:
- [ ] All Commands from EventStorming model included
- [ ] All Events from EventStorming model included
- [ ] All Policies mapped to handlers
- [ ] External Subscriptions identified for cross-context integration
- [ ] C# record syntax correct (parameter types refined)
- [ ] Required message metadata added (MessageId, OccurredUtc, IdempotencyKey)
- [ ] Partition key identified
- [ ] Persistence technology chosen with rationale
- [ ] Acceptance criteria testable
- [ ] EventStorming model linked and archived
