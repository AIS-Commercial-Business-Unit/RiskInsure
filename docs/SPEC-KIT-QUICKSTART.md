# Spec Kit Integration - Quick Start Guide

**Status**: ✅ Spec Kit templates integrated and customized for RiskInsure  
**Date**: 2026-02-16

---

## What's Been Done

✅ Spec Kit CLI installed and verified  
✅ Templates customized for RiskInsure patterns:
- .NET 10 + NServiceBus 9.x stack enforced
- Persistence choice gate (Cosmos vs PostgreSQL) added
- Constitution compliance checks integrated
- RiskInsure service structure (Domain → Infrastructure → Api → Endpoint.In)
- Message metadata requirements (MessageId, OccurredUtc, IdempotencyKey)

✅ Two spec templates available:
- **Full**: `spec-template.md` (30–60 min, comprehensive)
- **Quick**: `spec-template-quick.md` (10–20 min, lightweight delta capture)

---

## Your First Feature (5-Step Workflow)

### 1. Create Feature Spec (10–15 min)

```bash
/speckit.specify Build invoice cancellation feature that allows users to cancel unpaid invoices
```

**What happens**:
- Creates branch (e.g., `001-invoice-cancellation`)
- Creates `specs/001-invoice-cancellation/spec.md` using quick template
- Agent fills in: bounded context, scenarios, acceptance criteria
- You review and fill in: specific messages, partition key, edge cases

**Edit the spec** to add:
- Exact command/event names (PascalCase, past-tense for events)
- Partition key (e.g., `/orderId`)
- Idempotency strategy
- Any missing edge cases

---

### 2. Create Implementation Plan (10–15 min)

```bash
/speckit.plan Use Cosmos DB with single-partition strategy. Invoice entity will track cancellation state atomically. NServiceBus handler processes CancelInvoice command.
```

**What happens**:
- Reads your spec from step 1
- Creates `specs/001-invoice-cancellation/plan.md` with:
  - Technology stack (enforced: .NET 10, NServiceBus 9.x)
  - **Persistence decision** (you stated Cosmos DB - required)
  - Constitution compliance checklist
  - Service structure (which projects/layers)
- Creates supporting files:
  - `data-model.md` (entity schemas)
  - `contracts/` (endpoint definitions if API)
  - `quickstart.md` (validation scenarios)

**Review the plan** for:
- [ ] Persistence choice is correct (Cosmos DB or PostgreSQL)
- [ ] All 9 constitution principles pass (or violations justified)
- [ ] Service location correct (e.g., `services/billing/`)

---

### 3. Generate Task List (5 min)

```bash
/speckit.tasks
```

**What happens**:
- Reads `plan.md` and `spec.md`
- Creates `specs/001-invoice-cancellation/tasks.md` with:
  - Phase 1: Setup (project structure)
  - Phase 2: Foundation (message contracts, NServiceBus config, persistence)
  - Phase 3+: Tasks per user story (independently implementable)
- Tasks follow RiskInsure layering: Domain → Infrastructure → Api → Tests

**Task format**: `[ID] [P] [Story] Description with exact file path`  
**[P] = Parallel** - tasks that can run at the same time

---

### 4. Implement (varies)

**Follow task order** (Foundation → Story 1 → Story 2...):

**Foundation tasks** (must complete first):
1. Define message contracts in `services/<domain>/src/Domain/Contracts/`
2. Create domain models in `services/<domain>/src/Domain/Models/`
3. Setup persistence (Cosmos container or PostgreSQL schema)
4. Implement repositories with idempotency checks

**Per-story tasks** (can do in parallel):
1. Write tests first (xUnit unit tests, Playwright for API)
2. Implement handlers in `services/<domain>/src/Infrastructure/Handlers/`
3. Verify handlers are thin (delegate to domain services/managers)
4. Add structured logging with correlation IDs

**Key checks during implementation**:
- [ ] Handlers check existing state before creating (idempotent)
- [ ] Messages include MessageId, OccurredUtc, IdempotencyKey
- [ ] All logs include processing unit ID (fileRunId/orderId/etc.)
- [ ] Domain layer has NO infrastructure dependencies
- [ ] Test coverage: Domain 90%+, Handlers 80%+

---

### 5. Validate (ongoing)

**Constitution compliance**:
```bash
# Run your domain tests
dotnet test services/<domain>/test/Domain.Tests/

# Run integration tests
cd services/<domain>/test/Api.Tests
npm test
```

**Verify**:
- [ ] Idempotency test passes (replay message doesn't cause duplicates)
- [ ] All user stories independently testable
- [ ] Acceptance criteria from spec all have passing tests
- [ ] No cross-service HTTP calls (only Service Bus messages)

---

## Template Quick Reference

### When to Use Quick Template ⚡

**Use `spec-template-quick.md` (default) for:**
- Adding features to existing services
- Domain docs already exist
- Straightforward scenarios + messages
- Time-sensitive features

**Authoring**: 10–20 minutes  
**Focus**: Scenarios, messages, partition key, idempotency

---

### When to Use Full Template 📋

**Use `spec-template.md` for:**
- New domain areas (no existing docs)
- Complex multi-service features
- Exploratory requirements
- Need extensive functional requirements

**Authoring**: 30–60 minutes  
**Focus**: Comprehensive requirements, entities, edge cases

---

## Common Workflows

### Adding a Feature to Existing Service

```bash
# 1. Spec (reference existing domain docs)
/speckit.specify Add premium payment processing to billing service

# 2. Plan (choose persistence to match service)
/speckit.plan Use existing Cosmos DB billing container, partition by /orderId

# 3. Tasks
/speckit.tasks

# 4. Implement following task order
```

---

### Creating Cross-Service Integration

```bash
# 1. Spec (identify bounded contexts)
/speckit.specify Billing publishes InvoiceCreated event, Policy service subscribes to create policy documents

# 2. Plan (specify public contracts)
/speckit.plan InvoiceCreated goes in platform/RiskInsure.PublicContracts/Events/. Policy service uses PostgreSQL.

# 3. Tasks (will span both services)
/speckit.tasks
```

---

### Starting a New Service (Full Process)

```bash
# 1. Create domain standards doc first
# Create: services/newservice/docs/domain-specific-standards.md

# 2. Use full template
/speckit.specify (comprehensive description of new service)
# Manually choose spec-template.md in step 3 of agent workflow

# 3. Plan with full tech stack
/speckit.plan .NET 10, NServiceBus 9.x, choose Cosmos or PostgreSQL, define all layers

# 4. Tasks (includes project creation)
/speckit.tasks
```

---

## Persistence Decision Guide

**Choose Cosmos DB when:**
- Event sourcing or event-driven workflows
- High write throughput (>1000 ops/sec)
- Queries are partition-aligned (by fileRunId, orderId, etc.)
- Flexible schema evolution needed
- Co-locating related documents for free queries

**Choose PostgreSQL when:**
- Complex relational queries across entities
- Strong referential integrity requirements
- Reporting/analytics heavy workloads
- Team expertise favors relational
- Need mature tooling (ORMs, query builders)

**Required in plan**: State choice + brief rationale

---

## Constitution Principles (Quick Reference)

1. **Domain Language** - Use ubiquitous language from domain docs
2. **Single-Partition (Cosmos) or Normalized (PostgreSQL)** - Choose and stick
3. **Atomic State Transitions** - ETags (Cosmos) or transactions (PostgreSQL)
4. **Idempotent Handlers** - Check existing state, safe to retry
5. **Observability** - Correlation IDs in all logs
6. **Message-Based Integration** - Service Bus only (no HTTP between services)
7. **Thin Handlers** - Validate → delegate → publish
8. **Test Coverage** - Domain 90%+, Application 80%+
9. **Tech Constraints** - .NET 10, NServiceBus 9.x, approved persistence

**See**: [.specify/memory/constitution.md](../.specify/memory/constitution.md)

---

## Next Steps

You're ready to create your first feature! Try this:

```bash
# Pick a small feature from your backlog
/speckit.specify Add [simple feature in 1-2 sentences]

# After spec is generated, create the plan
/speckit.plan Use [Cosmos DB | PostgreSQL] because [brief reason]

# Generate tasks
/speckit.tasks

# Start coding (follow task order)
```

**Tips**:
- Start with quick template (most features)
- Always choose persistence in plan (required gate)
- Foundation tasks must complete before story tasks
- Test coverage is enforced (90% domain, 80% application)
- Message metadata is non-negotiable (MessageId, OccurredUtc, IdempotencyKey)

---

## Getting Help

- **Templates explained**: [.specify/templates/README.md](../.specify/templates/README.md)
- **Constitution**: [.specify/memory/constitution.md](../.specify/memory/constitution.md)
- **Project structure**: [copilot-instructions/project-structure.md](../copilot-instructions/project-structure.md)
- **Messaging patterns**: [copilot-instructions/messaging-patterns.md](../copilot-instructions/messaging-patterns.md)
