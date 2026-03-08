# Multi-Policy Billing - Business Requirements

**Domain**: Billing  
**Version**: 2.0.0  
**Date**: February 5, 2026  
**Updates**: Added multi-policy billing requirements

---

## Business Purpose

The Billing domain manages **billing accounts, payment tracking, and financial transactions** for insurance policies. It supports **multi-policy billing** where customers with multiple active policies have a consolidated billing account with per-policy premium tracking and targeted payment allocation.

---

## Business Capabilities

### 1. Billing Account Management

**Capability**: Create and maintain billing accounts associated with customer policies.

**Account Creation**:
- Triggered by `PolicyIssued` event from Policy domain
- One billing account per customer (supports multiple policies)
- Each policy added as a "billing line item" with its own premium and balance

**Business Rules**:
- BillingAccountId is unique (GUID)
- Account links to CustomerId
- Supports multiple policies per account
- Each policy tracks its own premium and balance separately
- Aggregate balance = sum of all policy balances

**Account Status**:
- **Active**: Has at least one active policy
- **PaidInFull**: All policies have $0 balance
- **PastDue**: Any policy balance overdue
- **Suspended**: Account suspended due to non-payment (all policies lapse)

---

### 2. Multi-Policy Billing (MVP Requirement)

**Capability**: Manage multiple policies under a single billing account with per-policy premium tracking.

**Data Model**:
```
BillingAccount
├── CustomerId
├── TotalBalance (aggregate of all policy balances)
├── Policies[]
    ├── PolicyId
    ├── PolicyNumber
    ├── Premium (total policy premium)
    ├── Balance (current amount owed for this policy)
    ├── EffectiveDate
    ├── ExpirationDate
    └── Status (Active, PaidInFull, Cancelled)
```

**Business Rules**:
- Customer can have multiple active policies in one billing account
- Each policy has its own premium amount and current balance
- Payments are allocated to specific policies (targeted allocation)
- TotalBalance = sum of all individual policy balances
- Account status reflects worst-case policy status (if any policy is PastDue, account is PastDue)

**Example**:
- Customer has Policy A ($1,200 annual) and Policy B ($800 annual)
- Both policies added to same billing account
- TotalBalance initially = $2,000
- Customer pays $1,200 for Policy A specifically
- Policy A balance = $0, Policy B balance = $800, TotalBalance = $800

---

### 3. Payment Processing

**Capability**: Record payments and allocate to policy balances.

**Payment Allocation Strategies**:

**1. Targeted Allocation (Primary)**:
- Customer specifies which policy(ies) to pay
- Payment allocated 100% to specified policy
- Reduces that policy's balance

**2. Proportional Allocation (Future)**:
- Payment split proportionally across all policies with balances
- Example: Policy A owes 60%, Policy B owes 40% → Payment splits 60/40

**3. Oldest First (Future)**:
- Payment allocated to policy with oldest balance first
- Common in collections scenarios

**Payment Process**:
1. Customer specifies payment amount and target policyId(s)
2. Validate sufficient payment method available
3. Create payment record linked to policy
4. Publish `InitiateFundTransfer` command to FundsTransferMgt
5. Update billing status to "PaymentPending"
6. Await `FundsSettled` event
7. Update policy balance (reduce by payment amount)
8. Update account TotalBalance
9. Publish `PaymentRecorded` event

**Business Rules**:
- Payment must specify target policyId
- Payment amount cannot exceed policy balance
- Partial payments allowed (pay portion of balance)
- Overpayments not allowed (payment ≤ balance)
- Failed payments do not reduce balance
- Multiple payments allowed per policy

---

### 4. Balance Tracking

**Capability**: Track outstanding balances per policy and aggregate across account.

**Balance Calculations**:
- **Policy Balance**: Premium - (sum of successful payments for that policy)
- **Total Account Balance**: Sum of all policy balances

**Balance Updates Triggered By**:
- PolicyIssued: Add new policy premium to account balance
- PaymentRecorded: Reduce policy balance by payment amount
- PolicyCancelled: Adjust balance for unearned premium (refund)
- PaymentFailed: No balance change

**Business Rules**:
- Balance cannot be negative (except during refund processing)
- Zero balance indicates policy is paid in full
- Account balance must equal sum of policy balances (consistency check)

---

### 5. Refund Processing

**Capability**: Process refunds for cancelled policies (unearned premium).

**Refund Scenarios**:
- Policy cancelled mid-term (customer request)
- Policy lapsed and reinstated (overpayment)
- Endorsement reducing premium mid-term

**Refund Process**:
1. Receive `PolicyCancelled` event with unearnedPremium amount
2. Update policy balance (reduce by unearned premium, may go negative)
3. If balance becomes negative, issue refund for negative amount
4. Publish `RefundInitiated` command to FundsTransferMgt
5. Await `FundsRefunded` event
6. Update policy balance to $0
7. Publish `RefundProcessed` event

**Business Rules**:
- Refund amount = unearned premium from policy cancellation
- Refund only if original payment was successful
- Refund uses same payment method as original payment
- Refund cannot exceed amount paid

---

### 6. Payment Status Tracking

**Capability**: Track payment lifecycle from initiation through settlement.

**Payment Statuses**:
- **Pending**: Payment initiated, awaiting fund transfer
- **Settled**: Funds successfully transferred (balance updated)
- **Failed**: Fund transfer failed (balance unchanged, retry may occur)
- **Refunded**: Payment reversed due to cancellation/error

**Status Transitions**:
```
Pending → Settled (on FundsSettled event)
Pending → Failed (on FundsTransferFailed event)
Settled → Refunded (on refund processing)
```

---

## Business Events

### BillingAccountCreated

**When**: PolicyIssued event received from Policy domain

**Business Significance**:
- Billing account established for customer
- Policy added as first line item
- Payment obligations begin

**Key Data**:
- BillingAccountId
- CustomerId
- PolicyId (initial policy)
- Premium
- Balance (initially equals premium)

**Subscribers**: None (informational)

---

### PaymentRecorded

**When**: Payment successfully processed and funds settled

**Business Significance**:
- Policy balance reduced
- Account balance updated
- May trigger policy activation if first payment

**Key Data**:
- BillingAccountId
- PolicyId (which policy was paid)
- PaymentAmount
- RemainingBalance (for that policy)
- TotalAccountBalance

**Subscribers**: Policy domain (may activate policy on first payment)

---

### RefundProcessed

**When**: Refund successfully issued to customer

**Business Significance**:
- Unearned premium returned
- Policy balance adjusted
- Financial reconciliation complete

**Key Data**:
- BillingAccountId
- PolicyId
- RefundAmount
- Reason (cancellation, endorsement, etc.)

---

### PolicyAdded

**When**: Second (or additional) policy issued for existing customer

**Business Significance**:
- Multi-policy billing activated
- New premium added to account balance
- Customer has multiple active policies

**Key Data**:
- BillingAccountId
- PolicyId (new policy)
- Premium
- UpdatedTotalBalance

---

## Business Constraints

### Data Validation Requirements

1. **Payment Amount**:
   - Must be > 0
   - Cannot exceed policy balance
   - Must specify target policyId

2. **Refund Amount**:
   - Must be ≤ amount paid for policy
   - Calculated from unearned premium (Policy domain)

3. **Balance Consistency**:
   - TotalBalance MUST equal sum of all policy balances
   - Atomic updates required when policies added/removed

---

### Regulatory Compliance

1. **Premium Refunds**:
   - Must be processed within 30 days of cancellation
   - Refund amount must match unearned premium calculation
   - Customer notification required

2. **Payment Allocation Disclosure**:
   - Customer must be informed how payment is allocated
   - Receipt must show payment applied to specific policy

3. **Non-Payment Consequences**:
   - 30-day grace period before lapse
   - Notices required at 15 days and 5 days before lapse
   - Policy lapse impacts all policies in account if aggregate balance unpaid

---

### Business Process Rules

1. **Multi-Policy Payment Prioritization**:
   - Customer chooses which policy to pay (targeted allocation)
   - Future: Auto-allocate to policy closest to lapse
   - Future: Allow partial payment across multiple policies

2. **Account Closure**:
   - Account remains active as long as one policy is active
   - Account closed when all policies expired/cancelled and balance = $0

3. **Payment Method Management**:
   - One or more payment methods per customer
   - Primary payment method designated for auto-pay
   - Payment method can be used across all policies

---

## Success Metrics

- **Payment Success Rate**: % of payments successfully settled
- **On-Time Payment Rate**: % of payments received before due date
- **Multi-Policy Adoption**: % of customers with 2+ policies
- **Refund Processing Time**: Average days to issue refund
- **Balance Accuracy**: % of accounts with consistent policy vs. total balances
- **Payment Allocation Accuracy**: % of payments correctly allocated to target policy

---

## Integration Points

### Inbound Events

- **PolicyIssued** (from Policy domain): Create billing account or add policy
- **PolicyCancelled** (from Policy domain): Process refund (unearned premium)
- **FundsSettled** (from FundsTransferMgt): Update balance after payment
- **FundsRefunded** (from FundsTransferMgt): Confirm refund completed

### Outbound Commands

- **InitiateFundTransfer** (to FundsTransferMgt): Process payment
- **RefundInitiated** (to FundsTransferMgt): Process refund

### Outbound Events

- **BillingAccountCreated**: Billing account established
- **PaymentRecorded**: Payment processed and balance updated
- **PolicyAdded**: Additional policy added to billing account

---

## Multi-Policy Scenarios

### Scenario 1: Customer Adds Second Policy

**Flow**:
1. Customer has Policy A ($1,200 annual) with billing account
2. Customer purchases Policy B ($800 annual)
3. `PolicyIssued` event received for Policy B
4. Policy B added to existing billing account
5. TotalBalance updated: $1,200 → $2,000
6. `PolicyAdded` event published

**Result**: One billing account with two policies, aggregate balance $2,000

---

### Scenario 2: Targeted Payment Allocation

**Flow**:
1. Customer has Policy A (balance $1,200) and Policy B (balance $800)
2. Customer pays $1,200 for Policy A specifically
3. Payment allocated 100% to Policy A
4. Policy A balance = $0, Policy B balance = $800
5. TotalBalance = $800
6. Policy A status = "PaidInFull", Policy B status = "Active"

**Result**: One policy paid in full, other still has balance

---

### Scenario 3: Cancellation with Multi-Policy Refund

**Flow**:
1. Customer has Policy A (balance $600) and Policy B (balance $400)
2. Policy A cancelled mid-term, unearned premium = $300
3. `PolicyCancelled` event received
4. Policy A balance reduced by $300: $600 → $300
5. Policy A still has $300 owed (overpayment scenario not applied in MVP)
6. Future: Issue $300 refund if overpaid

**Result**: Cancellation adjusts policy balance, refund only if overpaid

---

## Future Enhancements

1. **Installment Plans**:
   - Monthly/quarterly payment plans
   - Auto-payments on schedule
   - Installment fees

2. **Auto-Pay**:
   - Automatic payments from primary payment method
   - Payment due date tracking
   - Failed payment retry logic

3. **Payment Reminders**:
   - Email/SMS reminders before due date
   - Past due notices
   - Lapse warnings

4. **Proportional Payment Allocation**:
   - Split payment across multiple policies
   - Configurable allocation strategies

5. **Billing History**:
   - Transaction history per policy
   - Payment receipts
   - Refund documentation

6. **Dunning Process**:
   - Automated collection workflows
   - Payment plan offers for past due
   - Grace period management

---

## Glossary

- **BillingAccountId**: Unique identifier for a customer's billing account
- **TotalBalance**: Aggregate amount owed across all policies in account
- **Policy Balance**: Amount owed for a specific policy
- **Targeted Allocation**: Payment applied to specific policy chosen by customer
- **Proportional Allocation**: Payment split across multiple policies based on balance ratios
- **Unearned Premium**: Portion of premium corresponding to future coverage (refundable)
- **Grace Period**: 30-day period after due date before policy lapses
- **Lapse**: Policy cancellation due to non-payment
