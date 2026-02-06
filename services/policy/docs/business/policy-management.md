# Policy Management - Business Requirements

**Domain**: Policy  
**Version**: 1.0.0  
**Date**: February 5, 2026

---

## Business Purpose

The Policy domain manages the **policy lifecycle from issuance through expiration**, including endorsements, renewals, cancellations, and reinstatements. It serves as the system of record for active insurance policies and their coverage terms.

---

## Business Capabilities

### 1. Policy Creation & Binding

**Capability**: Convert accepted quotes into active insurance policies.

**Binding Process**:
1. Receive `QuoteAccepted` event from Rating & Underwriting domain
2. Create policy document with coverage terms from quote
3. Assign unique `PolicyId` and policy number
4. Set policy status to "Bound" (awaiting payment)
5. Publish `PolicyBound` event

**Business Rules**:
- Each policy receives unique `PolicyId` (GUID)
- Policy number follows format: `KWG-{YEAR}-{SEQUENCE}` (e.g., KWG-2026-000001)
- Policy effective date matches quote effective date
- Policy expiration date = effective date + term (6 or 12 months)
- Coverage terms locked from quote (no changes during binding)

**Policy Status**: Bound → PendingPayment → Issued → Active

---

### 2. Policy Issuance

**Capability**: Finalize policy after payment method is added.

**Issuance Process**:
1. Receive API call to issue policy (triggered by UI after payment method added)
2. Verify policy is in "Bound" status
3. Update status to "Issued"
4. Generate policy documents (PDF)
5. Publish `PolicyIssued` event
6. Send policy documents to customer

**Business Rules**:
- Only "Bound" policies can be issued
- Policy cannot be issued after expiration date
- Issuance triggers billing account creation
- Policy documents include: declarations page, coverage summary, terms & conditions

**Critical Event**: `PolicyIssued` triggers billing account creation and premium calculation

---

### 3. Policy Lifecycle Management

**Capability**: Manage policy status through its lifecycle.

**Policy Status Definitions**:

- **Bound**: Quote accepted, policy created, awaiting payment method
- **Issued**: Policy finalized after payment method added (billing account created)
- **Active**: Policy in force, coverage active (after first payment received)
- **Cancelled**: Policy terminated before expiration
- **Expired**: Policy reached natural expiration date
- **Lapsed**: Policy cancelled due to non-payment
- **Reinstated**: Previously lapsed policy restored

**State Transition Rules**:
```
Bound → Issued (payment method added)
Issued → Active (first payment received)
Active → Cancelled (customer request, underwriting change)
Active → Lapsed (non-payment)
Active → Expired (natural expiration)
Lapsed → Reinstated (within 30 days, payment received)
```

---

### 4. Policy Endorsements (Future)

**Capability**: Modify coverage during policy term.

**Endorsement Types**:
- Coverage change (increase/decrease limits)
- Deductible change
- Add/remove coverages
- Address change
- Named insured change

**Business Rules**:
- Endorsements create policy versions
- Premium adjusted pro-rata for term remaining
- Underwriting approval may be required for coverage increases
- Endorsement effective dates must be ≥ current date

---

### 5. Policy Cancellation

**Capability**: Terminate policy before expiration.

**Cancellation Types**:

**Customer-Initiated**:
- Voluntary cancellation (customer request)
- Effective date selected by customer (minimum 1 day notice)
- Premium refund calculated pro-rata

**Company-Initiated**:
- Non-payment (lapse)
- Underwriting grounds (material misrepresentation)
- Effective immediately or with notice period

**Cancellation Process**:
1. Receive cancellation request (API or internal system)
2. Calculate cancellation date
3. Calculate refund amount (unearned premium)
4. Update policy status to "Cancelled" or "Lapsed"
5. Publish `PolicyCancelled` event
6. Trigger refund workflow in Billing domain

**Business Rules**:
- Minimum 1-day notice for customer cancellations
- Immediate cancellation for fraud or non-payment
- Pro-rata refund of unearned premium
- No refund if policy active < 30 days and no claims

---

### 6. Policy Renewal (Future)

**Capability**: Generate renewal policy for expiring policies.

**Renewal Process**:
1. 60 days before expiration: Generate renewal quote
2. 45 days before: Send renewal notice to customer
3. 30 days before: Send reminder with updated premium
4. Customer accepts renewal quote
5. New policy created for next term

**Business Rules**:
- Renewal premium may differ based on updated rating factors
- Coverage terms carry forward unless customer changes
- Underwriting review required for claims activity
- Auto-renewal if customer has auto-pay enabled

---

### 7. Policy Reinstatement

**Capability**: Restore lapsed policies within grace period.

**Reinstatement Conditions**:
- Policy lapsed due to non-payment
- Within 30 days of lapse date
- Customer pays past due amount + current premium
- No claims filed during lapse period

**Reinstatement Process**:
1. Customer requests reinstatement
2. Verify reinstatement eligibility
3. Calculate total amount due
4. Receive payment
5. Update policy status to "Active"
6. Publish `PolicyReinstated` event

---

## Business Events

### PolicyBound

**When**: Quote accepted and policy document created

**Business Significance**:
- Policy officially created in system
- Coverage terms locked
- Awaiting payment method to issue

**Key Data**:
- PolicyId
- PolicyNumber (KWG-2026-000001)
- QuoteId (source quote)
- CustomerId
- Coverage details (structure limit, contents limit, deductibles)
- Term (6 or 12 months)
- Effective date
- Expiration date
- Premium

---

### PolicyIssued

**When**: Policy finalized after payment method added

**Business Significance**:
- **Critical event**: Triggers billing account creation
- Triggers premium earning calculations
- Policy documents generated
- Coverage becomes active (pending first payment)

**Key Data**:
- PolicyId
- PolicyNumber
- CustomerId
- Coverage details
- Premium
- Effective date
- Expiration date

**Subscribers**:
- **Billing domain**: Creates billing account with policy premium
- **Premium domain**: Starts premium earning calculation (future)

---

### PolicyCancelled

**When**: Policy terminated before natural expiration

**Business Significance**:
- Coverage ends on cancellation date
- Triggers refund calculation
- Ends billing obligations

**Key Data**:
- PolicyId
- CustomerId
- Cancellation date
- Cancellation reason
- Unearned premium (refund amount)

**Subscribers**:
- **Billing domain**: Calculate and process refund

---

### PolicyExpired

**When**: Policy reaches natural expiration date

**Business Significance**:
- Coverage ends
- No refund (policy ran full term)
- Renewal opportunity

**Key Data**:
- PolicyId
- Expiration date

---

### PolicyReinstated

**When**: Lapsed policy restored within grace period

**Business Significance**:
- Coverage restored
- Billing obligations resume

**Key Data**:
- PolicyId
- Reinstatement date
- Amount paid

---

## Business Constraints

### Data Validation Requirements

1. **Policy Number Uniqueness**:
   - Must be unique across all policies
   - Format: KWG-{YEAR}-{SEQUENCE}
   - Sequence resets annually

2. **Effective Date**:
   - Cannot be in the past (at binding time)
   - Cannot be > 60 days in future
   - Must match quote effective date

3. **Coverage Limits**:
   - Must match accepted quote exactly
   - No modifications during binding

4. **Status Transitions**:
   - Only valid transitions allowed (Bound → Issued → Active)
   - Cannot skip statuses

---

### Regulatory Compliance

1. **Policy Documents**:
   - Must be generated within 24 hours of issuance
   - Must include all required disclosures
   - Customer must receive copy (email + mailing)

2. **Cancellation Notices**:
   - Customer cancellations: 10-day notice required in some states
   - Company cancellations: 30-day notice for non-payment
   - Cancellation reason must be documented

3. **Data Retention**:
   - Active policies: Indefinite retention
   - Expired policies: 7 years after expiration
   - Cancelled policies: 7 years after cancellation

---

### Business Process Rules

1. **Policy Modification**:
   - Post-issuance changes require endorsement
   - Endorsements create new policy version
   - Premium changes calculated pro-rata

2. **Concurrent Policies**:
   - Customer can have multiple active policies
   - Each policy operates independently
   - Billing may be consolidated (future)

3. **Grace Period**:
   - 30-day grace period for premium payment
   - Coverage remains active during grace period
   - Policy lapses if payment not received

---

## Success Metrics

- **Policy Issuance Rate**: % of bound policies successfully issued
- **Time to Issue**: Average time from quote acceptance to policy issuance
- **Policy Retention**: % of policies reaching natural expiration
- **Lapse Rate**: % of policies lapsing due to non-payment
- **Reinstatement Rate**: % of lapsed policies reinstated
- **Cancellation Rate by Reason**: Distribution of cancellation reasons

---

## Integration Points

### Inbound Events

- **QuoteAccepted** (from Rating & Underwriting): Triggers policy creation

### Outbound Events

- **PolicyIssued** → Billing domain (create billing account)
- **PolicyIssued** → Premium domain (start earning calculations - future)
- **PolicyCancelled** → Billing domain (calculate refund)

### API Dependencies

- **Customer domain**: Retrieve customer contact information for documents
- **Rating & Underwriting domain**: Retrieve quote details during binding

---

## Future Enhancements

1. **Multi-Policy Discounts**:
   - Apply discounts for multiple policies
   - Bundled coverage (auto + home equivalent)

2. **Usage-Based Insurance**:
   - Telematics integration
   - Risk-based pricing adjustments mid-term

3. **Automated Underwriting Review**:
   - Trigger re-underwriting on endorsement requests
   - Risk score recalculation

4. **Policy Portals**:
   - Customer self-service for endorsements
   - Document download
   - Coverage certificates

5. **Claims Integration**:
   - Verify coverage on claim submission
   - Track claims history by policy

---

## Glossary

- **PolicyId**: Unique identifier (GUID) for an insurance policy
- **Policy Number**: Human-readable policy identifier (KWG-2026-000001)
- **Binding**: Process of converting quote to policy
- **Endorsement**: Mid-term policy modification
- **Lapse**: Policy cancellation due to non-payment
- **Reinstatement**: Restoration of lapsed policy
- **Earned Premium**: Portion of premium corresponding to elapsed policy term
- **Unearned Premium**: Portion of premium corresponding to future coverage (refundable)
- **Pro-Rata**: Proportional calculation (used for refunds and endorsements)
