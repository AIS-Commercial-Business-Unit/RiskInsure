# Rating & Underwriting - Business Requirements

**Domain**: Rating & Underwriting  
**Version**: 1.0.0  
**Date**: February 5, 2026

---

## Business Purpose

The Rating & Underwriting domain is responsible for **risk assessment, pricing determination, and insurability evaluation** for Kwegibo insurance products. This domain manages the quote lifecycle from initial quote creation through acceptance, calculating premiums based on risk factors and determining whether the risk is acceptable for coverage.

---

## Product: Kwegibo Insurance

### Product Definition

**Kwegibo** is a fictitious property-like insurance product with two coverage options:

**Coverage A - Kwegibo Structure Protection**:
- Coverage limits: $50,000 to $500,000
- Deductible options: $500, $1,000, $2,500, $5,000
- Protects the physical Kwegibo structure from covered perils

**Coverage B - Kwegibo Contents Protection**:
- Coverage limits: $10,000 to $150,000
- Deductible options: $250, $500, $1,000, $2,500
- Protects contents within the Kwegibo from covered perils

**Term Options**: 6-month or 12-month policy periods

---

## Business Capabilities

### 1. Quote Management

**Capability**: Create and manage insurance quotes with unique identifiers.

**Quote Lifecycle**:
```
Draft → UnderwritingPending → Quoted → Accepted → Expired
```

**Business Rules**:
- Each quote receives unique `QuoteId` (GUID)
- Quote expires 30 days from creation if not accepted
- Customer can have multiple quotes simultaneously
- Quote modifications create new quote version
- Accepted quotes transition to Policy domain

**Quote Status Definitions**:
- **Draft**: Quote in progress, incomplete data
- **UnderwritingPending**: Waiting for underwriting decision
- **Quoted**: Premium calculated, awaiting customer acceptance
- **Accepted**: Customer accepted quote, ready for policy issuance
- **Expired**: Quote exceeded 30-day validity period
- **Declined**: Underwriting determined risk is not acceptable

---

### 2. Risk Rating (Premium Calculation)

**Capability**: Calculate insurance premium based on risk factors.

**Rating Formula**:
```
Premium = Base Rate × Coverage Factor × Term Factor × Age Factor × Territory Factor
```

**Rating Factors**:

1. **Base Rate**: $500 (standard starting point)

2. **Coverage Factor**:
   - Structure coverage limit / $100,000
   - Contents coverage limit / $50,000
   - Combined coverage factor = Structure factor + Contents factor

3. **Term Factor**:
   - 6-month term: 0.55
   - 12-month term: 1.00

4. **Age Factor** (Kwegibo age, not customer age):
   - Age 0-5 years: 0.80 (newer, lower risk)
   - Age 6-15 years: 1.00 (standard risk)
   - Age 16-30 years: 1.20 (older, higher risk)
   - Age 31+ years: 1.50 (significantly higher risk)

5. **Territory Factor** (based on zip code):
   - Zone 1 (90210, 10001): 0.90 (low risk)
   - Zone 2 (60601, 33101): 1.00 (standard risk)
   - Zone 3 (70112, 94102): 1.20 (elevated risk)
   - Zone 4 (all others): 1.10 (moderate risk)

**Example Calculation**:
- Structure Coverage: $200,000
- Contents Coverage: $50,000
- Term: 12 months
- Kwegibo Age: 10 years
- Zip Code: 90210

```
Coverage Factor = ($200,000 / $100,000) + ($50,000 / $50,000) = 2.0 + 1.0 = 3.0
Premium = $500 × 3.0 × 1.0 × 1.0 × 0.90 = $1,350
```

---

### 3. Underwriting (Risk Acceptability)

**Capability**: Evaluate insurability based on risk characteristics.

**Underwriting Factors**:
1. **Prior Claims History**: Number of claims in past 3 years
2. **Kwegibo Age**: Age of the Kwegibo being insured
3. **Customer Credit Tier**: Credit-based insurance score

**Underwriting Classification**:

**Class A (Preferred Risk)**:
- 0 prior claims AND
- Kwegibo age ≤ 15 years AND
- Credit tier = Excellent

**Class B (Standard Risk)**:
- 0-1 prior claims AND
- Kwegibo age ≤ 30 years AND
- Credit tier = Good or Excellent

**Declined (Unacceptable Risk)**:
- 3+ prior claims OR
- Kwegibo age > 30 years OR
- Credit tier = Poor

**Underwriting Actions**:
- **Class A**: Auto-approved, quote proceeds to rating
- **Class B**: Auto-approved, quote proceeds to rating (may have premium surcharge in future)
- **Declined**: Quote status set to "Declined", customer notified

---

### 4. Quote Acceptance

**Capability**: Customer accepts quoted premium and coverage terms.

**Process**:
1. Customer reviews quote details (coverages, limits, deductibles, premium)
2. Customer accepts quote via API call
3. Quote status changes to "Accepted"
4. `QuoteAccepted` event published via RabbitMQ transport
5. Policy domain receives event and creates policy

**Business Rules**:
- Only quotes in "Quoted" status can be accepted
- Quote must not be expired
- Acceptance locks coverage terms and premium (no changes post-acceptance)
- Acceptance triggers policy creation workflow

---

## Business Events

### QuoteStarted

**When**: Customer initiates new quote request

**Business Significance**: 
- Marks beginning of quote lifecycle
- Captures initial customer intent and coverage needs

**Key Data**:
- QuoteId
- CustomerId
- Requested coverages (Structure limit, Contents limit)
- Requested deductibles
- Term length (6 or 12 months)

---

### UnderwritingSubmitted

**When**: Customer provides underwriting information (claims history, Kwegibo details)

**Business Significance**:
- Triggers underwriting evaluation
- Determines risk acceptability
- May result in quote approval or decline

**Key Data**:
- QuoteId
- Prior claims count
- Kwegibo age
- Credit tier

---

### QuoteApproved

**When**: Underwriting evaluation completes successfully (Class A or B)

**Business Significance**:
- Risk accepted for coverage
- Quote can proceed to rating
- Customer can continue to policy purchase

**Key Data**:
- QuoteId
- Underwriting class (A or B)
- Approval timestamp

---

### QuoteDeclined

**When**: Underwriting evaluation determines risk is unacceptable

**Business Significance**:
- Quote cannot proceed to policy
- Customer notified of declination with reason
- End of quote lifecycle

**Key Data**:
- QuoteId
- Decline reason (e.g., "Excessive prior claims", "Kwegibo age exceeds limits")

---

### QuoteCalculated

**When**: Premium calculation completes

**Business Significance**:
- Final premium amount determined
- Quote ready for customer acceptance
- All rating factors applied and documented

**Key Data**:
- QuoteId
- Premium amount
- Rating factors applied
- Breakdown of premium calculation

---

### QuoteAccepted

**When**: Customer accepts quote and agrees to coverage terms

**Business Significance**:
- **Critical event**: Triggers policy creation in Policy domain
- Locks coverage terms and premium
- Marks transition from quote to policy

**Key Data**:
- QuoteId
- CustomerId
- Accepted premium
- Coverages (structure limit, contents limit, deductibles)
- Term length
- Effective date

**Subscribers**: 
- **Policy domain**: Creates policy based on accepted quote

---

### QuoteExpired

**When**: Quote reaches 30-day expiration without acceptance

**Business Significance**:
- Quote no longer valid
- Customer must request new quote for current pricing
- Historical record of customer interest

**Key Data**:
- QuoteId
- Expiration date
- Original premium (for reference)

---

## Business Constraints

### Data Validation Requirements

1. **Coverage Limits**:
   - Structure: $50,000 ≤ limit ≤ $500,000
   - Contents: $10,000 ≤ limit ≤ $150,000

2. **Deductibles**:
   - Structure: $500, $1,000, $2,500, $5,000 only
   - Contents: $250, $500, $1,000, $2,500 only

3. **Term**:
   - 6 months or 12 months only

4. **Kwegibo Age**:
   - 0 ≤ age ≤ 50 years
   - Age > 30 triggers underwriting decline

5. **Prior Claims**:
   - 3+ claims triggers underwriting decline

---

### Regulatory Compliance

1. **Rate Filing**:
   - Rating formula must be filed with state regulators
   - Changes to rating factors require regulatory approval
   - Rate changes apply to new quotes only (not existing)

2. **Underwriting Guidelines**:
   - Declination reasons must be documented
   - Cannot discriminate based on protected classes
   - Must provide declination notice to customer

3. **Quote Validity Period**:
   - 30-day quote validity standard in most states
   - Some states may require longer periods

---

### Business Process Rules

1. **Quote Modification**:
   - Changes to coverages create new quote version
   - Previous quote versions retained for audit
   - Only latest quote version can be accepted

2. **Expiration Handling**:
   - Automated job runs daily to expire old quotes
   - Customer notified 7 days before expiration
   - Expired quotes cannot be reinstated (must re-quote)

3. **Concurrent Quotes**:
   - Customer can have multiple active quotes
   - Each quote is independent
   - Only one quote can be accepted (others auto-expire)

---

## Success Metrics

- **Quote-to-Bind Ratio**: % of quotes accepted by customers
- **Average Quote Turnaround Time**: Time from start to quoted status
- **Underwriting Approval Rate**: % of quotes approved vs declined
- **Quote Expiration Rate**: % of quotes expiring without acceptance
- **Premium Accuracy**: Discrepancies between quoted and final premium
- **Declination Rate by Reason**: Distribution of decline reasons

---

## Integration Points

### Inbound Dependencies

- **Customer domain**: CustomerId, birth date, zip code for rating

### Outbound Events

- **QuoteAccepted** → Policy domain (triggers policy creation)

---

## Future Enhancements

1. **Advanced Rating Factors**:
   - Occupancy type (owner-occupied, rental, vacant)
   - Construction type (frame, masonry, fire-resistive)
   - Protection class (fire department proximity)
   - Security features (alarm systems, sprinklers)

2. **Dynamic Pricing**:
   - Real-time risk scoring
   - Competitor price monitoring
   - Demand-based pricing adjustments

3. **Automated Underwriting**:
   - AI/ML risk models
   - External data integrations (claims databases, credit bureaus)
   - Automated document review

4. **Quote Comparison**:
   - Side-by-side quote comparison (different coverage options)
   - "What-if" scenarios (change deductible, see premium impact)

5. **Endorsements**:
   - Mid-term coverage changes
   - Add/remove coverages
   - Recalculate premium pro-rata

---

## Glossary

- **QuoteId**: Unique identifier (GUID) for an insurance quote
- **Premium**: Amount customer pays for insurance coverage
- **Underwriting**: Process of evaluating risk acceptability
- **Rating**: Process of calculating insurance premium
- **Deductible**: Amount customer pays before insurance coverage applies
- **Coverage Limit**: Maximum amount insurance will pay for a covered loss
- **Term**: Duration of insurance policy (6 or 12 months)
- **Class A/B**: Underwriting risk classification (A=preferred, B=standard)
- **Territory**: Geographic rating zone based on zip code
- **Base Rate**: Starting premium before risk factors applied
