# Premium Management - Business Requirements

**Domain**: Premium  
**Version**: 1.0.0  
**Date**: February 5, 2026  
**Status**: Design complete, implementation deferred post-MVP

---

## Business Purpose

The Premium domain manages **earned vs. unearned premium calculations** for accounting and financial reporting. It tracks how much premium has been "earned" (coverage period elapsed) versus "unearned" (future coverage not yet provided).

**Note**: This domain is **not required for MVP** but is included for completeness and future implementation.

---

## Business Capabilities

### 1. Premium Earning Calculation

**Capability**: Calculate earned premium based on elapsed policy term.

**Earning Methods**:

**Pro-Rata Method (Primary)**:
- Premium earns evenly over policy term
- Earned amount = (Days elapsed / Total days) × Total premium
- Example: 12-month policy at $1,200 → Earns $100/month

**365ths Method**:
- Premium earns based on days in year (not policy term)
- Earned amount = (Days elapsed / 365) × Annual premium
- Used for annual policies

**Business Rules**:
- Earning begins on policy effective date
- Earning stops on policy cancellation or expiration
- Earned premium cannot decrease (monotonic increase)
- Earned premium never exceeds total premium

---

### 2. Unearned Premium Tracking

**Capability**: Calculate unearned premium (future coverage not yet provided).

**Formula**:
```
Unearned Premium = Total Premium - Earned Premium
```

**Use Cases**:
- Cancellation refunds (return unearned premium)
- Financial reporting (balance sheet liability)
- Regulatory reporting (statutory accounting)

**Business Rules**:
- Unearned premium decreases as policy term progresses
- Unearned premium = $0 at policy expiration
- Used to calculate refund on cancellation

---

### 3. Earned Premium Reporting

**Capability**: Generate earned premium reports for accounting.

**Report Types**:
- **Daily**: Earned premium for all active policies (current day)
- **Monthly**: Total earned premium for month (accounting close)
- **Policy-Level**: Earned vs. unearned by policy
- **Customer-Level**: Aggregate earned across all customer policies

**Regulatory Requirements**:
- Statutory accounting (insurance accounting standards)
- GAAP reporting (Generally Accepted Accounting Principles)
- Quarterly filings with state insurance departments

---

### 4. Premium Adjustment on Cancellation

**Capability**: Calculate final earned premium and unearned refund on policy cancellation.

**Process**:
1. Calculate earned premium through cancellation date
2. Calculate unearned premium (refund amount)
3. Provide refund amount to Billing domain
4. Finalize premium earning (no further changes)

**Cancellation Types**:
- **Pro-Rata**: Customer receives full unearned premium refund
- **Short-Rate**: Customer receives reduced refund (penalty for early cancellation) - **Not in MVP**

---

## Business Events

### PremiumEarned (Daily)

**When**: Daily background job calculates earned premium

**Business Significance**:
- Updates earned premium for all active policies
- Provides data for financial reporting
- Tracks earning over time

**Key Data**:
- PolicyId
- EarnedAmount (for current day)
- CumulativeEarned (total earned to date)
- RemainingUnearned

---

### PremiumAdjusted

**When**: Policy cancelled or endorsed (premium changes mid-term)

**Business Significance**:
- Finalizes earned premium calculation
- Provides unearned premium for refund
- Closes earning for terminated policies

**Key Data**:
- PolicyId
- FinalEarnedPremium
- UnearnedPremium (refund amount)
- AdjustmentReason (cancellation, endorsement)

---

## Business Constraints

### Calculation Rules

1. **Earning Precision**:
   - Calculate to 2 decimal places
   - Use actual days (not average month = 30 days)
   - Account for leap years

2. **Boundary Conditions**:
   - Earned premium = $0 on effective date
   - Earned premium = Total premium on expiration date
   - Earned premium ≤ Total premium (always)

3. **Time Zone Handling**:
   - All dates in UTC
   - Earning calculated based on UTC date boundaries

---

### Regulatory Compliance

1. **Statutory Accounting**:
   - Unearned premium reported as liability
   - Earned premium reported as revenue
   - Quarterly reconciliation required

2. **GAAP Compliance**:
   - Revenue recognition rules (ASC 606)
   - Premium earned over coverage period
   - Cancellation adjustments in period of cancellation

3. **Audit Requirements**:
   - Daily earning calculations logged
   - Premium adjustments auditable
   - Reconciliation with billing domain

---

## Success Metrics

- **Earning Accuracy**: Reconciliation between Premium and Billing domains
- **Calculation Performance**: Time to calculate earned premium for all policies
- **Reporting Timeliness**: Daily reports generated before 9:00 AM
- **Adjustment Accuracy**: Cancellation refunds match unearned premium

---

## Integration Points

### Inbound Events

- **PolicyIssued** (from Policy domain): Start earning calculation
- **PolicyCancelled** (from Policy domain): Calculate final earned and unearned

### Outbound Events

- **PremiumEarned**: Daily earned premium update
- **PremiumAdjusted**: Finalized earning on cancellation

### Data Dependencies

- Policy domain: Policy details (effective date, expiration date, premium)
- Billing domain: Payments received (impacts unearned liability)

---

## Data Model (Future)

### PremiumEarning Document

```json
{
  "id": "guid",
  "policyId": "guid",
  "documentType": "PremiumEarning",
  "totalPremium": 1200.00,
  "earnedToDate": 300.00,
  "unearnedBalance": 900.00,
  "lastCalculatedUtc": "2026-05-01T00:00:00Z",
  "effectiveDate": "2026-03-01T00:00:00Z",
  "expirationDate": "2027-03-01T00:00:00Z",
  "status": "Active"
}
```

---

## Implementation Notes (Future)

### Daily Earning Job

**Schedule**: Runs daily at 1:00 AM UTC

**Process**:
1. Query all active policies
2. Calculate earned premium for each policy
3. Update PremiumEarning document
4. Publish PremiumEarned event
5. Generate daily earning report

**Performance Optimization**:
- Process policies in batches (1000 per batch)
- Parallel processing
- Cache policy details

---

### Cancellation Workflow

**Trigger**: Receive PolicyCancelled event

**Process**:
1. Calculate earned premium through cancellation date
2. Calculate unearned premium
3. Update PremiumEarning status to "Finalized"
4. Publish PremiumAdjusted event with refund amount
5. Billing domain processes refund

---

## Future Enhancements

1. **Endorsement Handling**:
   - Mid-term premium adjustments
   - Pro-rata earning recalculation
   - Additional premium earning

2. **Installment Plans**:
   - Earning vs. payment timing
   - Unearned premium with installment payments

3. **Advanced Reporting**:
   - Loss ratio calculations (earned premium vs. claims)
   - Profitability analysis by policy
   - Territory-based earning reports

4. **Reconciliation Automation**:
   - Auto-reconcile with Billing domain
   - Flag discrepancies for investigation
   - Monthly reconciliation workflows

---

## Glossary

- **Earned Premium**: Portion of premium corresponding to elapsed coverage period
- **Unearned Premium**: Portion of premium corresponding to future coverage (liability)
- **Pro-Rata**: Proportional calculation based on time
- **Short-Rate**: Penalty for early cancellation (customer receives less than pro-rata refund)
- **Statutory Accounting**: Insurance-specific accounting standards
- **GAAP**: Generally Accepted Accounting Principles (standard accounting rules)
- **ASC 606**: Revenue recognition accounting standard
