# RiskInsure Documentation Philosophy

**Version**: 1.0 | **Date**: 2026-02-16

---

## Two Types of Documentation

RiskInsure maintains two complementary but distinct types of documentation:

### 1. Domain Documentation (Current State) 📘

**Location**: `services/<domain>/docs/`  
**Purpose**: Living documentation of how each bounded context works **today**  
**Nature**: Continuously updated to reflect current production behavior

**Contents**:
- `overview.md` - Domain purpose, responsibilities, integration points
- `business/` - Business rules, processes, terminology
- `technical/` - APIs, data models, implementation details
- `specs/README.md` - Index of features affecting this domain

**Updates**: Modified whenever production behavior changes (features ship)

**Examples**:
- [services/policy/docs/overview.md](../services/policy/docs/overview.md)
- [services/customer/docs/overview.md](../services/customer/docs/overview.md)
- [services/billing/docs/business/](../services/billing/docs/business/)

---

### 2. Feature Specifications (Change Slices) 📝

**Location**: `/specs/###-feature-name/`  
**Purpose**: Captures the **delta** (what's changing) for a specific feature  
**Nature**: Immutable once complete (historical record of intent + implementation)

**Contents per feature**:
- `spec.md` - User scenarios, acceptance criteria, messages, data strategy
- `plan.md` - Technical approach, persistence choice, constitution compliance
- `tasks.md` - Implementation task list (Domain → Infrastructure → API/Tests)
- `data-model.md` - Entity schemas (optional)
- `contracts/` - API/endpoint definitions (optional)
- `quickstart.md` - Validation scenarios (optional)

**Updates**: Created during feature development, then archived as history

**Examples**: *(none yet - you'll create your first one soon)*

---

## Why Two Documentation Types?

### Problem: Event-Sourced Documentation is Hard to Navigate

If you treat feature specs like "events" that stack up forever, finding "current truth" becomes painful:
- Reading 27 feature folders to understand one domain
- Piecing together which features are still relevant
- Losing sight of the coherent "now" state

### Solution: Projection + Event Log Pattern

- **Domain docs = Projection** (current state, easy to navigate)
- **Feature specs = Event log** (historical intent, change rationale)

This gives you:
- ✅ **Quick onboarding** - Read domain docs to understand current behavior
- ✅ **Historical context** - Feature specs explain *why* decisions were made
- ✅ **Change tracking** - See evolution of domain over time
- ✅ **Traceability** - Every behavior traces back to a spec + constitution principles

---

## Workflow: Feature Spec → Production → Domain Docs

### During Feature Development

1. **Create feature spec** (10–20 min using quick template)
   ```bash
   /speckit.specify Add invoice cancellation for unpaid invoices
   ```
   - Creates `/specs/001-invoice-cancellation/spec.md`
   - Captures: scenarios, messages, partition key, idempotency
   - References: existing domain docs + constitution

2. **Create implementation plan** (10–15 min)
   ```bash
   /speckit.plan Use Cosmos DB, partition by /orderId
   ```
   - Creates `plan.md` with persistence choice + constitution checks
   - Generates supporting files (data-model, contracts, quickstart)

3. **Generate tasks** (5 min)
   ```bash
   /speckit.tasks
   ```
   - Creates `tasks.md` organized by user story
   - Foundation → Story 1 → Story 2 → ...

4. **Add to domain spec index**
   - Edit `services/<domain>/docs/specs/README.md`
   - Add feature to "Active Features (In Development)" section

5. **Implement following task order**
   - Domain layer (pure logic) → Infrastructure → API/Endpoint → Tests
   - Constitution compliance verified (handlers thin, idempotent, etc.)

---

### After Feature Ships

1. **Update domain docs** to reflect new behavior
   - Add new business rules to `business/`
   - Update API docs in `technical/`
   - Modify `overview.md` if responsibilities changed

2. **Move feature in spec index**
   - Edit `services/<domain>/docs/specs/README.md`
   - Move from "Active" to "Recently Shipped"
   - Add shipped date

3. **Feature spec remains unchanged**
   - Lives in `/specs/###-feature-name/` as historical record
   - Provides context for future maintainers ("why did we do this?")

---

## Per-Domain Spec Indexes

Each domain maintains its own **feature index** at `services/<domain>/docs/specs/README.md`:

**Purpose**:
- Filter features relevant to this domain
- Track active vs shipped features
- Provide domain-specific entry point to specs

**Sections**:
- Active Features (in development now)
- Recently Shipped (last 90 days)
- All Features (complete chronological list)

**Maintenance**:
- Developers update when creating/shipping features
- Keeps domain view clean and discoverable

**Example**: [services/billing/docs/specs/README.md](../services/billing/docs/specs/README.md)

---

## Navigation Patterns

### "I want to understand the current Billing domain"
1. Start: `services/billing/docs/overview.md`
2. Dive into: `services/billing/docs/business/` for rules
3. Check: `services/billing/docs/technical/` for implementation

### "I want to see what features are being worked on for Policy"
1. Go to: `services/policy/docs/specs/README.md`
2. Look at: "Active Features" section
3. Click through to: `/specs/###-feature-name/spec.md` for details

### "I want to understand why invoice cancellation works the way it does"
1. Find in: `services/billing/docs/specs/README.md` (shipped features)
2. Read: `/specs/###-invoice-cancellation/spec.md` for original intent
3. Compare: Current behavior in `services/billing/docs/business/` (may have evolved)

### "I'm adding a new feature to Customer domain"
1. Read: `services/customer/docs/overview.md` (understand current state)
2. Create spec: `/speckit.specify [your feature]` (captures delta)
3. Update index: `services/customer/docs/specs/README.md` (add to active)
4. After ship: Update `services/customer/docs/` with new state

---

## Benefits of This Approach

### For Developers
- **Fast onboarding** - Read domain docs (current state) first
- **Clear task order** - Feature specs generate prioritized task lists
- **Constitution enforcement** - Plans validate against architectural principles
- **Independent work** - Tasks organized by user story (parallel implementation)

### For Product/Business
- **Traceability** - Every feature has documented user scenarios + success criteria
- **Change history** - Feature specs show evolution of domain over time
- **Visibility** - Spec indexes show what's in progress per domain

### For Maintenance
- **"Why was this built?"** - Feature specs preserve original intent
- **"What changed when?"** - Spec indexes track shipped dates
- **"What's the current behavior?"** - Domain docs are always up-to-date

---

## Anti-Patterns to Avoid

❌ **Don't let domain docs get stale**
- Update them when features ship (part of Definition of Done)

❌ **Don't put implementation details in feature specs**
- Specs describe WHAT/WHY, plans describe HOW
- Keep specs technology-agnostic where possible

❌ **Don't create feature specs for every tiny change**
- Bug fixes, refactors, trivial updates → just update domain docs
- Use feature specs when you need: acceptance criteria, task breakdown, or historical record of "why"

❌ **Don't let spec indexes become outdated**
- Add features to "Active" when created
- Move to "Shipped" when delivered (include date)

---

## Related Documentation

- **Quick Start**: [SPEC-KIT-QUICKSTART.md](./SPEC-KIT-QUICKSTART.md) - How to use Spec Kit tooling
- **Constitution**: [.specify/memory/constitution.md](../.specify/memory/constitution.md) - Non-negotiable principles
- **Project Structure**: [copilot-instructions/project-structure.md](../copilot-instructions/project-structure.md) - Service layering
- **Templates**: [.specify/templates/README.md](../.specify/templates/README.md) - Spec/plan/tasks templates

---

## Questions?

**"Should I backfill existing features into specs?"**  
No (unless selectively for high-value flows). Spec Kit is "future only" by default. Current state is already in domain docs.

**"What if a feature touches multiple domains?"**  
The spec lives in one place (`/specs/###-feature-name/`), but gets indexed in multiple domain spec indexes (`services/domain-a/docs/specs/README.md` and `services/domain-b/docs/specs/README.md`).

**"How do I know if I need a full spec vs quick spec?"**  
Default to quick template (10–20 min). Use full template only for new domain areas or complex exploratory work.

**"What goes in domain docs vs feature specs?"**  
- Domain docs: Current behavior, business rules, APIs
- Feature specs: What's changing, user scenarios, acceptance criteria, implementation plan

---

**Maintained by**: Architecture team  
**Last Updated**: 2026-02-16
