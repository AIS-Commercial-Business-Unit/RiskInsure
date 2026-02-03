# Document Manager Agent

**Version**: 1.0.0 | **Created**: 2026-02-03

## Purpose

This agent synchronizes business capability documentation between Domain Manager code and the `services/{domain}/docs/business/` folder. It supports **bidirectional sync**:

- **Code → Docs**: Extract business rules from Manager implementations and generate business-focused documentation
- **Docs → Code**: Use business documentation to regenerate Manager method implementations

The documentation is written from a **business perspective** (not technical) and must be complete enough to regenerate the Manager code.

---

## When to Use This Agent

Invoke this agent when:

1. **New Manager Created**: Generate initial business documentation
2. **Manager Updated**: Sync business rules from code to docs
3. **Business Rules Changed**: Update documentation after rule modifications
4. **Documentation Updated**: Regenerate Manager skeleton from updated business specs
5. **Conflict Detected**: Both code and docs changed - need user clarification

---

## Agent Workflow

### Step 1: Identify Context

Ask the user to clarify:

```markdown
## Document Manager Agent - Clarification Needed

I need to understand what you want me to do:

**Manager File**: [Path to Manager file]
**Documentation File**: [Path or will be created at services/{domain}/docs/business/{manager-name}.md]

**What would you like me to do?**

1. **Generate Documentation from Code** (Code → Docs)
   - Extract business rules from Manager implementation
   - Create/update business documentation
   
2. **Generate Manager from Documentation** (Docs → Code)
   - Read business documentation
   - Generate Manager method skeleton with business rules
   
3. **Sync Both Ways** (Detect conflicts)
   - Compare code and docs
   - Identify what changed
   - Ask for source of truth

**Current State**:
- [ ] Manager code exists
- [ ] Documentation exists
- [ ] Both exist (potential conflict)
- [ ] Neither exists (need more info)

Please specify which operation you need.
```

### Step 2: Extract or Generate

Based on user's choice, proceed with the appropriate operation.

---

## Operation A: Code → Documentation

### Input
- Manager file path (e.g., `services/billing/src/Domain/Managers/BillingAccountManager.cs`)

### Output
- Documentation file (e.g., `services/billing/docs/business/billing-account.md`)

### Process

1. **Analyze Manager Code**
   - Extract class name and purpose from XML comments
   - Identify all public methods (business capabilities)
   - For each method:
     - Extract business rules from:
       - XML comments (/// summary with "Business Rules:")
       - Inline comments (// Business Rule N:)
       - Validation logic (if/throw patterns)
       - Error messages and error codes
     - Extract published events (context.Publish calls)
     - Extract dependencies (constructor parameters)

2. **Transform to Business Language**
   - Convert technical validation → business constraints
   - Remove implementation details
   - Focus on **what** and **why**, not **how**

   **Examples**:
   
   | Technical (Code) | Business (Documentation) |
   |------------------|--------------------------|
   | `if (dto.Amount <= 0)` | Payment must have a positive amount |
   | `if (account.Status != BillingAccountStatus.Active)` | Only active accounts can receive payments |
   | `if (dto.Amount > account.OutstandingBalance)` | Payment cannot exceed the remaining balance owed |

3. **Generate Documentation Using Template**
   - See template structure below
   - Fill in all sections
   - Use business language throughout

4. **Write Documentation File**
   - Create `services/{domain}/docs/business/` if needed
   - Write file: `{manager-name}.md` (kebab-case)
   - Preserve any existing manual sections (e.g., "Business Context", "Examples")

---

## Operation B: Documentation → Code

### Input
- Documentation file path (e.g., `services/billing/docs/business/billing-account.md`)
- Manager file path (if exists) or template location

### Output
- Manager code with method skeletons and business rule comments

### Process

1. **Parse Documentation**
   - Extract manager overview
   - Extract each business capability
   - Extract business rules per capability
   - Extract events to publish
   - Extract dependencies

2. **Generate Manager Skeleton**
   - Create class with proper naming
   - Add constructor with dependencies
   - For each business capability:
     - Create method signature
     - Add XML summary with business rules
     - Add inline comments for each rule
     - Generate validation code structure (if/return pattern)
     - Add event publishing code
     - Add try/catch with error handling

3. **Generate Supporting Files**
   - DTOs for each capability (if not exist)
   - Result objects (if not exist)
   - Suggest repository methods needed

4. **Present to User**
   - Show generated code
   - Highlight what needs manual implementation
   - List any missing dependencies

---

## Operation C: Conflict Detection

### When Both Code and Docs Exist

1. **Compare Timestamps**
   - Which was modified more recently?

2. **Compare Content**
   - Extract business rules from code
   - Extract business rules from docs
   - Identify differences:
     - Rules added in code but not docs
     - Rules in docs but not code
     - Different rule wording

3. **Present Conflict Report**

```markdown
## Conflict Detected

**Manager**: BillingAccountManager.cs (modified: 2026-02-03 10:30 AM)
**Documentation**: docs/business/billing-account.md (modified: 2026-02-01 09:15 AM)

### Differences Found:

**Business Capability**: Record Payment

| Rule | In Code | In Docs | Status |
|------|---------|---------|--------|
| Minimum payment | ✅ $1.00 | ❌ Not documented | Code newer |
| Payment exceeds balance | ✅ Present | ✅ Present | ✓ Match |
| Account status check | ✅ Active only | ✅ Active only | ✓ Match |

**Events Published**:
| Event | In Code | In Docs | Status |
|-------|---------|---------|--------|
| PaymentReceived | ✅ Present | ✅ Present | ✓ Match |

### Recommendation:
Code was modified more recently (2 days ago). Suggest updating documentation from code.

**What would you like to do?**
1. Update documentation from code (Code → Docs)
2. Update code from documentation (Docs → Code)
3. Manual merge (show me both and I'll decide)
```

4. **Wait for User Decision**

---

## Documentation Template Structure

```markdown
# {Manager Name}

**Domain**: {Domain Name}  
**Aggregate Root**: {Aggregate Name}  
**Version**: {Version}  
**Last Updated**: {Date}

---

## Overview

**Business Purpose**: {What this manager does from business perspective}

**Responsibilities**: {High-level business responsibilities}

**Key Constraints**: {Major business constraints or invariants}

---

## Business Capabilities

This manager implements the following business capabilities:

1. [{Capability Name}](#capability-capability-name)
2. [{Capability Name}](#capability-capability-name)
   ... (list all public methods)

---

## Capability: {Capability Name}

### Business Purpose

{What this capability does in business terms}

### Business Rules

| Rule # | Business Constraint | Error Code | Retryable |
|--------|---------------------|------------|-----------|
| 1 | {Business rule description} | {ERROR_CODE} | {Yes/No} |
| 2 | {Business rule description} | {ERROR_CODE} | {Yes/No} |
| ... | ... | ... | ... |

### Expected Inputs

| Input | Business Meaning | Required | Constraints |
|-------|------------------|----------|-------------|
| {Input field} | {What it represents} | {Yes/No} | {Business constraints} |

### Expected Outputs

**Success**: {What happens on success}

**Failure**: {What happens on failure}

### Events Published

#### {EventName}

**Purpose**: {Why this event is published}

**When**: {Business trigger for this event}

**Key Information**:
| Field | Business Meaning | Example |
|-------|------------------|---------|
| {Field name} | {What it represents} | {Example value} |

**Subscribers**: {Who cares about this event - business perspective}

---

## Dependencies

| Dependency | Business Purpose | Type |
|------------|------------------|------|
| {Repository name} | {What business data it manages} | Repository |
| {Service name} | {What business capability it provides} | Service |
| Message Session | Publishing business events | Infrastructure |

---

## Business Examples

### Example 1: {Scenario Name}

**Scenario**: {Business scenario description}

**Given**: {Initial business state}

**When**: {Business action}

**Then**: {Expected business outcome}

---

## Change History

| Version | Date | Changes | Author |
|---------|------|---------|--------|
| 1.0.0 | {Date} | Initial documentation | {Name} |

---

*This documentation is maintained by the Document Manager Agent and should be regenerated when Manager code changes.*
```

---

## Template Placeholders

When generating documentation, replace these placeholders:

| Placeholder | Example Value | Source |
|-------------|---------------|--------|
| `{Manager Name}` | Billing Account Manager | Class name (PascalCase → Title Case) |
| `{Domain Name}` | Billing | Namespace/folder name |
| `{Aggregate Name}` | BillingAccount | Primary entity/aggregate root |
| `{Version}` | 1.0.0 | Semantic version |
| `{Date}` | 2026-02-03 | Current date |
| `{Capability Name}` | Record Payment to Account | Method name (PascalCase → Title Case) |
| `{Business rule description}` | Payment must have a positive amount | Extracted from validation logic |
| `{ERROR_CODE}` | INVALID_AMOUNT | Extracted from error result |
| `{EventName}` | PaymentReceived | Event class name |

---

## Business Language Guidelines

### ✅ Good Business Language

- "Payment must have a positive amount"
- "Only active accounts can receive payments"
- "Payment cannot exceed the remaining balance owed"
- "Each payment requires a unique reference number for tracking"
- "Minimum payment amount is $1.00 to process"

### ❌ Bad Technical Language

- "dto.Amount must be > 0"
- "account.Status == BillingAccountStatus.Active"
- "Validation checks ETag for concurrency"
- "Throws InvalidOperationException on failure"
- "Repository.RecordPaymentAsync is called"

### Transformation Rules

1. **Remove Code References**: No variable names, method calls, class names
2. **Use Business Terms**: Account status, not enum values
3. **Explain Intent**: Why the rule exists (business reason)
4. **Active Voice**: "Payment must be..." not "System validates that..."
5. **Business Constraints**: Focus on business limits, not technical limits

---

## Example: BillingAccountManager

Here's what the documentation should look like for the current BillingAccountManager:

### Generated Documentation Preview:

```markdown
# Billing Account Manager

**Domain**: Billing  
**Aggregate Root**: BillingAccount  
**Version**: 1.0.0  
**Last Updated**: 2026-02-03

---

## Overview

**Business Purpose**: Manages all billing account operations for insurance premium payments.

**Responsibilities**: 
- Recording payments against billing accounts
- Enforcing payment business rules and constraints
- Publishing payment events for downstream processing
- Maintaining account balance accuracy

**Key Constraints**: 
- Accounts must be active to receive payments
- Payments cannot exceed outstanding balances
- All payments require unique reference numbers for tracking

---

## Business Capabilities

This manager implements the following business capabilities:

1. [Record Payment to Account](#capability-record-payment-to-account)

---

## Capability: Record Payment to Account

### Business Purpose

Records a customer payment against their billing account, reducing the outstanding balance and tracking the payment reference for audit purposes.

### Business Rules

| Rule # | Business Constraint | Error Code | Retryable |
|--------|---------------------|------------|-----------|
| 1 | Payment must have a positive amount | INVALID_AMOUNT | No |
| 2 | Payment must meet minimum threshold of $1.00 | AMOUNT_BELOW_MINIMUM | No |
| 3 | Billing account must exist in the system | ACCOUNT_NOT_FOUND | No |
| 4 | Only active accounts can receive payments | INVALID_ACCOUNT_STATUS | No |
| 5 | Payment cannot exceed the remaining balance owed | PAYMENT_EXCEEDS_BALANCE | No |
| 6 | Each payment requires a unique reference number (duplicate detection) | (Handled silently) | N/A |

### Expected Inputs

| Input | Business Meaning | Required | Constraints |
|-------|------------------|----------|-------------|
| Account ID | Billing account identifier | Yes | Must exist in system |
| Amount | Payment amount in USD | Yes | Positive, minimum $1.00, max outstanding balance |
| Reference Number | Payment tracking number (e.g., check number, ACH trace) | Yes | Unique per account |
| Idempotency Key | Duplicate detection key | Yes | Format: {AccountId}:{ReferenceNumber} |

### Expected Outputs

**Success**: 
- Payment recorded in billing account
- Account balance updated (reduced)
- PaymentReceived event published
- Returns updated account with new totals

**Failure**: 
- No changes to account
- Error code and message returned
- Indicates if error is retryable

### Events Published

#### PaymentReceived

**Purpose**: Notifies downstream systems that a payment has been successfully recorded

**When**: Immediately after payment is recorded in the billing account

**Key Information**:
| Field | Business Meaning | Example |
|-------|------------------|---------|
| AccountId | Billing account identifier | "ACC-12345" |
| Amount | Payment amount received | 150.00 |
| ReferenceNumber | Payment tracking number | "CHK-98765" |
| TotalPaid | Cumulative payments on account | 1,250.00 |
| OutstandingBalance | Remaining balance owed | 850.00 |

**Subscribers**: 
- Accounting system (for financial reconciliation)
- Customer service (for account status updates)
- Notification service (for payment confirmations)
- Audit system (for compliance tracking)

---

## Dependencies

| Dependency | Business Purpose | Type |
|------------|------------------|------|
| BillingAccountRepository | Manages billing account data and payment records | Repository |
| Message Session | Publishing business events to subscribers | Infrastructure |

---

## Business Examples

### Example 1: Successful Payment Recording

**Scenario**: Customer makes a premium payment via ACH

**Given**: 
- Active billing account ACC-12345
- Outstanding balance: $1,000.00
- Payment received: $250.00 via ACH (trace: ACH-45678)

**When**: Payment is recorded through the system

**Then**: 
- Account balance updated to $750.00
- Total paid increases to $250.00
- PaymentReceived event published
- Payment tracked with reference ACH-45678

### Example 2: Payment Exceeds Balance

**Scenario**: Customer attempts to overpay their account

**Given**: 
- Active billing account ACC-67890
- Outstanding balance: $100.00
- Payment attempted: $150.00

**When**: Payment is submitted

**Then**: 
- Payment rejected with error PAYMENT_EXCEEDS_BALANCE
- Account balance unchanged
- No event published
- Customer notified of exact outstanding amount

### Example 3: Duplicate Payment Detection

**Scenario**: Same ACH payment processed twice due to retry

**Given**: 
- Payment already recorded (ACH-11111)
- Same payment retried with same reference

**When**: Duplicate payment processed

**Then**: 
- Duplicate detected via idempotency key
- Original account state returned
- No duplicate payment recorded
- System handles gracefully (no error thrown)

---

## Change History

| Version | Date | Changes | Author |
|---------|------|---------|--------|
| 1.0.0 | 2026-02-03 | Initial business capability documentation | Document Manager Agent |
```

---

## Agent Invocation Examples

### Example 1: Generate Documentation from Code

```
@documentmanager

Please generate business documentation for the BillingAccountManager.

**Manager Path**: services/billing/src/Domain/Managers/BillingAccountManager.cs
**Output Path**: services/billing/docs/business/billing-account.md
**Operation**: Code → Documentation
```

### Example 2: Generate Code from Documentation

```
@documentmanager

I've updated the billing business rules in the documentation. Please regenerate the Manager code.

**Documentation Path**: services/billing/docs/business/billing-account.md
**Operation**: Documentation → Code (skeleton)
```

### Example 3: Sync After Changes

```
@documentmanager

Both my Manager code and documentation have changed. Please help me sync them.

**Manager Path**: services/billing/src/Domain/Managers/BillingAccountManager.cs
**Documentation Path**: services/billing/docs/business/billing-account.md
**Operation**: Detect conflicts and advise
```

---

## Quality Checklist

Before finalizing documentation, verify:

- [ ] All business rules written in business language (no code references)
- [ ] All error codes documented
- [ ] All published events fully documented with fields
- [ ] Business examples provided for key scenarios
- [ ] Dependencies explained from business perspective
- [ ] Documentation complete enough to regenerate Manager
- [ ] No technical implementation details leaked
- [ ] Follows template structure exactly
- [ ] All placeholders replaced
- [ ] Version and date updated

---

## Notes

- **Always ask for clarification** when both code and docs exist
- **Focus on business value**, not technical implementation
- Documentation should be **sufficient to regenerate the code**
- Use **tables for structured data** (business rules, events, inputs/outputs)
- Keep business examples **realistic and complete**
- Update **Change History** with each regeneration

---

**Agent Version**: 1.0.0  
**Last Updated**: 2026-02-03  
**Maintained By**: RiskInsure Platform Team
