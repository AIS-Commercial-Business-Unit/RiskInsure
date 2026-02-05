# Integration Verification Quick Reference

**Agent**: `integration-contract-verifier`  
**Purpose**: Verify cross-domain integration contracts are valid and complete

---

## Quick Start

### Run Full Verification
```
@agent integration-contract-verifier: Verify cross-domain integration contracts
```

### Fix Issues Interactively
```
@agent integration-contract-verifier: Fix integration concerns interactively
```

### Check Specific Service
```
@agent integration-contract-verifier: Verify Billing service integration contracts
```

---

## What Gets Verified

### ✅ Published Events
- Event documented in business docs
- Event documented in technical spec "Events Published" table
- Contract file exists at documented location
- Contract has standard metadata (MessageId, OccurredUtc, IdempotencyKey)
- Payload fields match documentation

### ✅ Subscribed Events
- Publishing domain specified
- Publishing domain actually publishes this event
- Event names match exactly
- Contract locations match
- Handler name follows convention: `{EventName}Handler`

### ✅ Business-Technical Alignment
- Events in business docs appear in technical specs
- Technical specs explain business purpose
- Integration patterns align with business workflows

---

## Common Concerns & Fixes

### Concern: "Event not documented by publisher"
**Fix**: Add event to publisher's "Events Published" table in `highlevel-tech.md`

### Concern: "Contract file missing"
**Fix**: Create contract file at: `platform/RiskInsure.PublicContracts/Events/{EventName}.cs`

### Concern: "Handler name doesn't follow convention"
**Fix**: Rename handler to: `{EventName}Handler.cs`

### Concern: "Publishing domain not specified"
**Fix**: Add "Publishing Domain" column value in "Events Subscribed To" table

---

## Documentation Templates

### Events Published Table
```markdown
| Event Name | Purpose | Trigger | Payload Summary | Contract Location |
|------------|---------|---------|-----------------|-------------------|
| `FundsSettled` | Payment settled | Transfer completes | CustomerId, Amount, TransactionId | `platform/.../FundsSettled.cs` |
```

### Events Subscribed To Table
```markdown
| Event Name | Publishing Domain | Handler Name | Purpose | Action Taken | Contract Location |
|------------|------------------|--------------|---------|--------------|-------------------|
| `FundsSettled` | FundTransferMgt | `FundsSettledHandler` | Record payment | Apply payment via manager | `platform/.../FundsSettled.cs` |
```

---

## Verification Report

After running verification, report generated at:
```
docs/verification-reports/integration-contracts-{timestamp}.md
```

Contains:
- Services scanned
- Events verified
- Concerns raised (with severity)
- Remediation steps
- Recommendations

---

## Two-Agent Approach

### 1. Contract Verifier (This Agent)
**When**: During planning, before implementation  
**Verifies**: Documentation (business docs, technical specs, contracts)  
**Output**: Concerns about documentation gaps

### 2. Handler Validator (Separate Agent - Recommended)
**When**: After implementation, before PR merge  
**Verifies**: Code (handler implementations, manager calls, idempotency)  
**Output**: Concerns about code quality

**Workflow**:
```
Design Phase → Contract Verifier → Fix Docs → Implement Code → Handler Validator → PR Merge
```

---

## Integration with CI/CD

### Pre-Commit Hook
```bash
# .git/hooks/pre-commit
@agent integration-contract-verifier: Verify cross-domain integration contracts
```

### GitHub Actions
```yaml
# .github/workflows/verify-contracts.yml
on:
  pull_request:
    paths:
      - 'services/**/docs/**'
      - 'platform/RiskInsure.PublicContracts/**'

jobs:
  verify:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Verify Integration Contracts
        run: |
          # Run agent verification
          # Fail PR if Critical or High severity concerns
```

---

## Related Documentation

- **[cross-domain-integration.md](../../copilot-instructions/cross-domain-integration.md)** - How to document integration points
- **[integration-contract-verifier.md](./integration-contract-verifier.md)** - Full agent specification
- **[copilot-instructions.md](../copilot-instructions.md)** - Development standards

---

## FAQs

**Q: When should I run this agent?**  
A: Before implementing new cross-domain integrations, after updating documentation, before PR approval

**Q: What's the difference between Published and Subscribed events?**  
A: Published = events YOUR domain sends out. Subscribed = events your domain listens for from OTHER domains.

**Q: Why separate contract verification from code validation?**  
A: Different lifecycle stages. Verify contracts during design/planning. Validate code after implementation.

**Q: Can I verify a single service?**  
A: Yes! Use: `@agent integration-contract-verifier: Verify {ServiceName} service integration contracts`

**Q: What if a concern is a false positive?**  
A: Add suppression comment in technical spec or create issue for agent improvement

---

**Last Updated**: 2026-02-04
