# Premium Domain

## Purpose

The Premium domain is responsible for **calculating and tracking premium amounts** over the policy lifecycle. This domain manages the distinction between written premium (total charged) and earned premium (amount earned over time), which is critical for financial reporting and revenue recognition.

---

## Key Responsibilities

1. **Written Premium Calculation**
   * Calculate total premium charged for new policies
   * Recalculate premiums for endorsements and renewals
   * Track premium adjustments and refunds

2. **Earned Premium Tracking**
   * Calculate premium earned over time based on coverage period
   * Apply pro-rata earning for partial periods
   * Handle earned premium for cancelled policies

3. **Premium Adjustments**
   * Process mid-term premium changes from endorsements
   * Calculate return premiums for cancellations
   * Handle premium corrections and adjustments

4. **Premium Reporting**
   * Provide written vs. earned premium data for financial reporting
   * Support premium reconciliation and auditing
   * Calculate premium reserves (unearned premium)

---

## Integration Points

* **Policy**: Receives policy events (issue, endorse, renew, cancel) triggering premium calculations
* **Rating and Underwriting**: Uses calculated premiums from rating engine
* **Billing**: Provides premium amounts for customer invoicing
* **Accounting**: Supplies earned premium data for revenue recognition

---

## Business Events

* `WrittenPremiumCalculated` - Total premium determined for policy period
* `EarnedPremiumUpdated` - Premium earning progression calculated
* `PremiumAdjusted` - Premium amount changed due to endorsement or correction
* `ReturnPremiumCalculated` - Refund amount determined for cancellation

---

## Documentation Structure

* **Business**: Premium calculation rules, earning methods, adjustment policies, and financial requirements
* **Technical**: APIs, calculation engines, data models, and reporting integrations
