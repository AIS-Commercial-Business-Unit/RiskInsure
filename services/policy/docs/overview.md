# Policy Domain

## Purpose

The Policy domain manages the **complete lifecycle of insurance policies** from initial issuance through termination. This domain is the system of record for policy details, coverage terms, and all policy modifications throughout the policy's lifetime.

---

## Key Responsibilities

1. **Policy Issuance**
   * Create new policies based on approved applications
   * Establish coverage effective dates and terms
   * Generate policy documents and confirmations

2. **Policy Endorsements**
   * Process mid-term policy changes (coverage updates, beneficiary changes, etc.)
   * Recalculate premiums for endorsements
   * Document policy modifications with effective dates

3. **Policy Renewals**
   * Manage renewal workflow at policy expiration
   * Generate renewal offers with updated terms/pricing
   * Process customer renewal decisions

4. **Policy Cancellations**
   * Handle voluntary and involuntary cancellations
   * Calculate refund amounts for early termination
   * Track cancellation reasons and effective dates

5. **Policy Reinstatements**
   * Restore previously cancelled or lapsed policies
   * Validate reinstatement eligibility
   * Collect outstanding premiums if required

---

## Integration Points

* **Rating and Underwriting**: Receives approved applications for policy issuance
* **Billing**: Triggers billing account creation and premium collection
* **Premium**: Coordinates premium calculations for policy changes
* **Customer**: Maintains customer-policy relationships
* **Claims**: Validates policy status and coverage for claims

---

## Business Events

* `PolicyIssued` - New policy created and activated
* `PolicyEndorsed` - Mid-term policy change processed
* `PolicyRenewed` - Policy term extended
* `PolicyCancelled` - Policy terminated
* `PolicyReinstated` - Cancelled/lapsed policy restored
* `PolicyExpired` - Policy reached end of term without renewal

---

## Documentation Philosophy

**This document and all files in `docs/` represent CURRENT STATE** - the living truth of how the Policy domain works today.

**Feature specifications** (in `/specs/###-feature-name/`) capture **CHANGE SLICES** - specific additions or modifications being made. Once a feature ships, this domain documentation is updated to reflect the new current state.

**See**: [../../docs/SPEC-KIT-QUICKSTART.md](../../../docs/SPEC-KIT-QUICKSTART.md) for how specs and domain docs work together.

---

## Documentation Structure

* **Business**: Policy lifecycle rules, endorsement types, cancellation policies, and renewal processes
* **Technical**: APIs, data models, policy state machines, and document generation
