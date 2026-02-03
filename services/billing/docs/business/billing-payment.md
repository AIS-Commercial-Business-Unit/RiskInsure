# Billing Payment Manager

**Domain**: Billing  
**Aggregate Root**: BillingAccount  
**Version**: 1.0.0  
**Last Updated**: 2026-02-03

---

## Overview

**Business Purpose**: Manages payment recording operations for insurance premium payments, ensuring accurate payment processing and balance tracking.

**Responsibilities**: 
- Recording customer payments against billing accounts
- Enforcing payment business rules and constraints
- Publishing payment events for downstream processing and notifications
- Maintaining account balance accuracy through payment application
- Preventing payment processing errors through validation

**Key Constraints**: 
- Accounts must be in active status to receive payments
- Payments cannot exceed outstanding balances (no overpayment allowed)
- All payments require unique reference numbers for tracking and auditing
- Minimum payment threshold enforced to prevent processing costs exceeding payment value

---

## Business Capabilities

This manager implements the following business capabilities:

1. [Record Payment to Account](#capability-record-payment-to-account)

---

## Capability: Record Payment to Account

### Business Purpose

Records a customer payment against their billing account, reducing the outstanding balance and tracking the payment reference for audit purposes. This capability ensures that only valid payments are processed while maintaining accurate account balances and providing complete payment traceability.

### Business Rules

| Rule # | Business Constraint | Error Code | Retryable |
|--------|---------------------|------------|-----------|
| 1 | Payment must have a positive amount greater than zero | INVALID_AMOUNT | No |
| 2 | Payment must meet minimum processing threshold of $1.00 | AMOUNT_BELOW_MINIMUM | No |
| 3 | Billing account must exist in the system | ACCOUNT_NOT_FOUND | No |
| 4 | Only active accounts can receive payments (not closed, suspended, or cancelled) | INVALID_ACCOUNT_STATUS | No |
| 5 | Payment amount cannot exceed the remaining balance owed on the account | PAYMENT_EXCEEDS_BALANCE | No |
| 6 | Each payment requires a unique reference number per account (duplicate payments are silently ignored for idempotency) | N/A | N/A |

### Expected Inputs

| Input | Business Meaning | Required | Constraints |
|-------|------------------|----------|-------------|
| Account ID | Unique identifier for the billing account | Yes | Must exist in system |
| Amount | Payment amount in US dollars | Yes | Positive number, minimum $1.00, cannot exceed outstanding balance |
| Reference Number | Payment tracking number from payment source (e.g., check number, ACH trace number, wire transfer ID) | Yes | Unique per account for duplicate detection |
| Idempotency Key | Duplicate detection key to prevent processing same payment twice | Yes | Format: {AccountId}:{ReferenceNumber} |
| Occurred Date/Time | When the payment actually occurred (for audit tracking) | Yes | Should reflect actual payment timestamp |

### Expected Outputs

**Success**: 
- Payment successfully recorded in billing account
- Account balance reduced by payment amount
- Total paid amount increased by payment amount
- PaymentReceived event published to notify subscribers
- Returns updated account with new balance totals
- Idempotent: Returns existing account if duplicate payment detected

**Failure**: 
- No changes made to billing account
- Error code and descriptive message returned explaining violation
- Indicates whether error is retryable (for transient issues) or permanent (business rule violation)
- System logs warning or error for monitoring and troubleshooting

### Events Published

#### PaymentReceived

**Purpose**: Notifies downstream systems that a payment has been successfully recorded against a billing account, enabling coordinated processing across the platform.

**When**: Immediately after payment is recorded and account balance is updated in the database.

**Key Information**:
| Field | Business Meaning | Example |
|-------|------------------|---------|
| MessageId | Unique identifier for this specific event | 3fa85f64-5717-4562-b3fc-2c963f66afa6 |
| OccurredUtc | When the payment actually occurred (business timestamp) | 2026-02-03T14:30:00Z |
| AccountId | Billing account that received the payment | ACC-12345 |
| Amount | Payment amount received in USD | 250.00 |
| ReferenceNumber | Payment tracking number for reconciliation | ACH-98765 or CHK-54321 |
| TotalPaid | Cumulative total of all payments on this account | 1,250.00 |
| OutstandingBalance | Remaining balance owed after this payment | 750.00 |
| IdempotencyKey | Key for duplicate detection (AccountId:ReferenceNumber) | ACC-12345:ACH-98765 |

**Subscribers**: 
- **Accounting System**: Updates financial records and reconciliation reports
- **Customer Service Portal**: Displays real-time payment status to representatives
- **Notification Service**: Sends payment confirmation emails/SMS to customers
- **Audit System**: Records payment activity for compliance and regulatory reporting
- **Collections System**: Updates delinquency status and payment plans
- **Reporting/Analytics**: Tracks payment trends and account health metrics

---

## Dependencies

| Dependency | Business Purpose | Type |
|------------|------------------|------|
| BillingAccountRepository | Manages billing account data, payment records, and account balance updates with optimistic concurrency control | Repository |
| Message Session | Publishing business events to subscribers across the platform | Infrastructure |
| Logger | Tracking payment processing activities, warnings, and errors for monitoring and troubleshooting | Infrastructure |

---

## Business Examples

### Example 1: Successful Premium Payment via ACH

**Scenario**: Customer makes a monthly premium payment through automated ACH debit

**Given**: 
- Active billing account ACC-12345 with outstanding balance of $1,000.00
- Total paid to date: $0.00
- Payment received via ACH with trace number ACH-45678
- Payment amount: $250.00 (quarterly premium installment)

**When**: Payment is processed through the billing system

**Then**: 
- Payment successfully recorded with reference ACH-45678
- Account outstanding balance reduced to $750.00
- Total paid increased to $250.00
- PaymentReceived event published with all payment details
- Customer receives payment confirmation notification
- Accounting system updates receivables

### Example 2: Payment Exceeds Outstanding Balance

**Scenario**: Customer attempts to pay more than they owe on their account

**Given**: 
- Active billing account ACC-67890
- Outstanding balance: $100.00
- Customer submits payment for: $150.00 (attempting to overpay)

**When**: Payment is submitted for processing

**Then**: 
- Payment rejected with error code PAYMENT_EXCEEDS_BALANCE
- Error message: "Payment amount $150.00 exceeds outstanding balance $100.00"
- Account balance remains unchanged at $100.00
- No PaymentReceived event published
- Customer service notified to contact customer about exact amount owed
- Customer can resubmit payment for correct amount ($100.00)

### Example 3: Payment Below Minimum Threshold

**Scenario**: Customer attempts to make a small payment below processing threshold

**Given**: 
- Active billing account ACC-11111
- Outstanding balance: $1,000.00
- Customer submits payment for: $0.50 (below $1.00 minimum)

**When**: Payment is submitted for processing

**Then**: 
- Payment rejected with error code AMOUNT_BELOW_MINIMUM
- Error message: "Payment amount must be at least $1.00"
- Account balance unchanged
- No payment processing costs incurred
- Customer notified of minimum payment requirement

### Example 4: Duplicate ACH Payment Detection (Idempotency)

**Scenario**: Same ACH payment is retried due to network timeout, ensuring no duplicate charges

**Given**: 
- Payment already successfully processed (ACH-99999 for $200.00)
- Account balance already updated (reduced by $200.00)
- Same payment retried with identical reference ACH-99999

**When**: Duplicate payment is processed

**Then**: 
- Duplicate detected via idempotency key (ACC-12345:ACH-99999)
- Original account state returned without changes
- No duplicate payment recorded in system
- No second PaymentReceived event published
- System handles gracefully (no error thrown to caller)
- Audit log shows duplicate detected and ignored

### Example 5: Payment to Inactive Account

**Scenario**: Customer attempts payment on a suspended account due to fraud investigation

**Given**: 
- Billing account ACC-55555 with status: Suspended
- Outstanding balance: $500.00
- Customer submits payment for: $100.00

**When**: Payment is submitted for processing

**Then**: 
- Payment rejected with error code INVALID_ACCOUNT_STATUS
- Error message: "Cannot record payment for account with status Suspended"
- Account balance unchanged
- Customer service alerted to handle payment offline
- Payment held pending fraud investigation resolution
- Customer contacted about account status

---

## Change History

| Version | Date | Changes | Author |
|---------|------|---------|--------|
| 1.0.0 | 2026-02-03 | Initial business capability documentation from code | Document Manager Agent |

---

*This documentation is maintained by the Document Manager Agent and should be regenerated when Manager code changes.*
