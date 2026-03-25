# Billing Account Management

**Domain**: Billing | **Manager**: BillingAccountManager | **Last Updated**: 2026-02-03

## Overview

The Billing Account Manager manages the complete lifecycle of insurance policy billing accounts, from initial creation through closure. This includes establishing accounts for new policies, managing premium changes, controlling account status, and handling billing cycle adjustments.

### Responsibilities

- Create billing accounts for insurance policies with initial premium amounts
- Manage account lifecycle transitions (Pending → Active → Suspended → Closed)
- Update premium amounts when policy terms change
- Control account access through activation and suspension
- Manage billing cycle preferences (Monthly, Quarterly, Semi-Annual, Annual)
- Publish lifecycle events for downstream systems

### Key Constraints

- Accounts start in **Pending** status and must be activated before accepting payments
- Premium amounts cannot be negative
- Policy numbers must be unique per customer
- Closed accounts cannot be modified
- All operations are idempotent and safe to retry

---

## Business Capabilities

The Billing Account Manager exposes six core business capabilities:

1. **Create Billing Account** - Establish a new billing account for an insurance policy
2. **Update Premium Owed** - Adjust the premium amount when policy terms change
3. **Activate Account** - Make a pending account active and ready for payments
4. **Suspend Account** - Temporarily block an account from accepting payments
5. **Close Account** - Permanently close an account (policy terminated)
6. **Update Billing Cycle** - Change the billing frequency for the account

---

## Business Rules

| Rule # | Capability | Business Rule | Error Code | Retryable |
|--------|-----------|---------------|------------|-----------|
| 1 | Create Account | Account ID must not already exist (duplicate creation is idempotent) | N/A (Success) | No |
| 2 | Create Account | Policy number must be unique per customer | DUPLICATE_POLICY_NUMBER | No |
| 3 | Create Account | Premium owed must be greater than or equal to zero | NEGATIVE_PREMIUM | No |
| 4 | Create Account | Effective date cannot be more than 90 days in the past | INVALID_EFFECTIVE_DATE | No |
| 5 | Update Premium | Account must not be in Closed status | ACCOUNT_CLOSED | No |
| 6 | Update Premium | New premium amount must be greater than or equal to zero | NEGATIVE_PREMIUM | No |
| 7 | Activate Account | Account must be in Pending status (other statuses succeed idempotently) | N/A (Success) | No |
| 8 | Suspend Account | Cannot suspend an account that is already Closed | ACCOUNT_CLOSED | No |
| 9 | Suspend Account | Suspending an already Suspended account succeeds idempotently | N/A (Success) | No |
| 10 | Close Account | Closing an already Closed account succeeds idempotently | N/A (Success) | No |
| 11 | Close Account | Closing with outstanding balance generates warning but succeeds | N/A (Warning logged) | No |
| 12 | Update Billing Cycle | Account must not be in Closed status | ACCOUNT_CLOSED | No |

---

## Events Published

### BillingAccountCreated

Published when a new billing account is successfully created for an insurance policy.

**Event Fields:**
- `MessageId` (Guid) - Unique event identifier
- `OccurredUtc` (DateTimeOffset) - When the account was created
- `AccountId` (string) - System-generated account identifier
- `CustomerId` (string) - Customer/policyholder identifier
- `PolicyNumber` (string) - Insurance policy number
- `PolicyHolderName` (string) - Name of the policy holder
- `CurrentPremiumOwed` (decimal) - Initial premium amount owed
- `BillingCycle` (enum) - Billing frequency (Monthly, Quarterly, SemiAnnual, Annual)
- `EffectiveDate` (DateTimeOffset) - When the policy becomes effective
- `IdempotencyKey` (string) - Deduplication key: `account-created-{AccountId}`

**Subscribers:**
- **Premium Billing System** - Sets up recurring billing schedule
- **Policy Management System** - Links billing account to policy record
- **Analytics System** - Tracks new account creation metrics
- **Notification Service** - Sends welcome communication to policy holder

---

### PremiumOwedUpdated

Published when the premium amount is adjusted due to policy changes.

**Event Fields:**
- `MessageId` (Guid) - Unique event identifier
- `OccurredUtc` (DateTimeOffset) - When the premium was updated
- `AccountId` (string) - Account identifier
- `OldPremiumOwed` (decimal) - Previous premium amount
- `NewPremiumOwed` (decimal) - New premium amount
- `ChangeReason` (string) - Business reason for the change
- `IdempotencyKey` (string) - Deduplication key with timestamp

**Subscribers:**
- **Premium Billing System** - Adjusts recurring billing amounts
- **Policy Management System** - Records premium change history
- **Analytics System** - Tracks premium adjustment patterns
- **Notification Service** - Informs policy holder of premium change

---

### AccountActivated

Published when a pending account is activated and ready for payments.

**Event Fields:**
- `MessageId` (Guid) - Unique event identifier
- `OccurredUtc` (DateTimeOffset) - When the account was activated
- `AccountId` (string) - Account identifier
- `PolicyNumber` (string) - Insurance policy number
- `IdempotencyKey` (string) - Deduplication key: `account-activated-{AccountId}`

**Subscribers:**
- **Payment Processing System** - Enables payment acceptance
- **Policy Management System** - Updates policy status to active
- **Notification Service** - Sends activation confirmation

---

### AccountSuspended

Published when an account is suspended and blocked from accepting payments.

**Event Fields:**
- `MessageId` (Guid) - Unique event identifier
- `OccurredUtc` (DateTimeOffset) - When the account was suspended
- `AccountId` (string) - Account identifier
- `PolicyNumber` (string) - Insurance policy number
- `SuspensionReason` (string) - Business reason for suspension
- `IdempotencyKey` (string) - Deduplication key with timestamp

**Subscribers:**
- **Payment Processing System** - Blocks payment acceptance
- **Collections System** - Initiates collection procedures if applicable
- **Policy Management System** - Updates policy status
- **Notification Service** - Notifies policy holder of suspension

---

### AccountClosed

Published when an account is permanently closed due to policy termination.

**Event Fields:**
- `MessageId` (Guid) - Unique event identifier
- `OccurredUtc` (DateTimeOffset) - When the account was closed
- `AccountId` (string) - Account identifier
- `PolicyNumber` (string) - Insurance policy number
- `ClosureReason` (string) - Business reason for closure
- `FinalOutstandingBalance` (decimal) - Remaining balance at closure
- `IdempotencyKey` (string) - Deduplication key: `account-closed-{AccountId}`

**Subscribers:**
- **Payment Processing System** - Permanently blocks payments
- **Collections System** - Handles final outstanding balance if present
- **Policy Management System** - Archives policy record
- **Analytics System** - Tracks account closure patterns
- **Notification Service** - Sends final account closure notice

---

### BillingCycleUpdated

Published when the billing frequency is changed for an account.

**Event Fields:**
- `MessageId` (Guid) - Unique event identifier
- `OccurredUtc` (DateTimeOffset) - When the billing cycle was updated
- `AccountId` (string) - Account identifier
- `OldBillingCycle` (enum) - Previous billing frequency
- `NewBillingCycle` (enum) - New billing frequency
- `ChangeReason` (string) - Business reason for the change
- `IdempotencyKey` (string) - Deduplication key with timestamp

**Subscribers:**
- **Premium Billing System** - Adjusts billing schedule
- **Policy Management System** - Records billing cycle change
- **Notification Service** - Confirms billing cycle change to policy holder

---

## Business Examples

### Example 1: Creating a New Insurance Policy Account

**Given:** A new life insurance policy has been issued to John Doe  
**When:** The system creates a billing account with:
- Customer ID: "CUST-12345"
- Policy Number: "POL-67890"
- Policy Holder: "John Doe"
- Premium Owed: $500.00
- Billing Cycle: Monthly
- Effective Date: February 1, 2026

**Then:** 
- Account is created in **Pending** status
- Account ID is system-generated (GUID)
- `BillingAccountCreated` event published
- Account is ready for activation

---

### Example 2: Policy Premium Adjustment

**Given:** An active billing account with current premium of $500.00  
**When:** Policy terms change and premium is adjusted to $600.00 with reason "Coverage increase"  
**Then:**
- Premium is updated from $500.00 to $600.00
- `PremiumOwedUpdated` event published with old and new amounts
- Outstanding balance recalculated: $600.00 - TotalPaid
- Premium Billing System adjusts recurring charges

---

### Example 3: Account Lifecycle - New Policy Flow

**Given:** A newly created billing account in Pending status  
**When:** 
1. Account is created (Pending status)
2. Policy is issued and account is activated (Active status)
3. Customer makes payments (Active status, balance decreasing)
4. Policy is terminated and account is closed (Closed status)

**Then:**
- `BillingAccountCreated` → `AccountActivated` → `AccountClosed` events published in sequence
- Each status transition is tracked and logged
- Closed account cannot be modified further

---

### Example 4: Preventing Invalid Premium Update

**Given:** A billing account with current premium of $500.00  
**When:** An attempt is made to update premium to -$100.00  
**Then:**
- Update is rejected with error code: `NEGATIVE_PREMIUM`
- Error message: "Premium owed cannot be negative"
- No changes made to account
- No event published

---

### Example 5: Account Suspension and Reactivation

**Given:** An active billing account for a policy holder who hasn't paid  
**When:**
1. Account is suspended with reason "Non-payment of premium"
2. Customer pays outstanding balance
3. Account is reactivated (Suspended → Active)

**Then:**
- `AccountSuspended` event published (payment processing blocked)
- After payment, status manually updated to Active
- Payment acceptance re-enabled
- Collections procedures halted

---

### Example 6: Idempotent Account Creation

**Given:** A billing account creation request with Account ID "12345"  
**When:**
1. First request creates account successfully
2. Duplicate request received with same Account ID

**Then:**
- First request: Account created, `BillingAccountCreated` published
- Second request: Returns success (idempotent), no duplicate account, no duplicate event
- Logged as: "Account {AccountId} already exists - idempotent duplicate detected"

---

### Example 7: Closing Account with Outstanding Balance

**Given:** An active billing account with outstanding balance of $300.00  
**When:** Account is closed with reason "Policy cancellation"  
**Then:**
- Account status changed to Closed
- Warning logged: "Closing account {AccountId} with outstanding balance {Balance}"
- `AccountClosed` event published with `FinalOutstandingBalance: 300.00`
- Collections System receives event to pursue outstanding balance
- No further modifications allowed on account

---

### Example 8: Changing Billing Cycle

**Given:** An active account with Monthly billing cycle  
**When:** Customer requests change to Quarterly billing with reason "Reduce payment frequency"  
**Then:**
- Billing cycle updated from Monthly to Quarterly
- `BillingCycleUpdated` event published with old and new cycles
- Premium Billing System adjusts next billing date to quarterly schedule
- Customer notified of billing cycle change

---

## Dependencies

### Internal Dependencies
- **BillingAccountRepository** - Data persistence for billing accounts
- **IMessageSession** (NServiceBus) - Event publishing to message bus

### External Systems
- **Premium Billing System** - Manages recurring billing schedules
- **Payment Processing System** - Handles payment acceptance
- **Policy Management System** - Links billing to policy records
- **Collections System** - Manages outstanding balances
- **Analytics System** - Tracks billing metrics
- **Notification Service** - Communicates with policy holders

---

## Inputs and Outputs

### CreateBillingAccountAsync

**Input (CreateBillingAccountDto):**
- `AccountId` (string) - Caller-provided GUID
- `CustomerId` (string) - Customer identifier
- `PolicyNumber` (string) - Insurance policy number
- `PolicyHolderName` (string) - Policy holder name
- `CurrentPremiumOwed` (decimal) - Initial premium amount
- `BillingCycle` (enum) - Billing frequency
- `EffectiveDate` (DateTimeOffset) - Policy effective date

**Output (BillingAccountResult):**
- `IsSuccess` (bool) - Operation outcome
- `AccountId` (string) - Created account ID
- `ErrorMessage` (string) - Error description if failed
- `ErrorCode` (string) - Programmatic error code
- `IsRetryable` (bool) - Whether operation can be retried

---

### UpdatePremiumOwedAsync

**Input (UpdatePremiumOwedDto):**
- `AccountId` (string) - Account to update
- `NewPremiumOwed` (decimal) - New premium amount
- `ChangeReason` (string) - Business justification

**Output (BillingAccountResult):** Same as above

---

### ActivateAccountAsync

**Input:**
- `accountId` (string) - Account to activate

**Output (BillingAccountResult):** Same as above

---

### SuspendAccountAsync

**Input:**
- `accountId` (string) - Account to suspend
- `suspensionReason` (string) - Business justification

**Output (BillingAccountResult):** Same as above

---

### CloseAccountAsync

**Input:**
- `accountId` (string) - Account to close
- `closureReason` (string) - Business justification

**Output (BillingAccountResult):** Same as above

---

### UpdateBillingCycleAsync

**Input (UpdateBillingCycleDto):**
- `AccountId` (string) - Account to update
- `NewBillingCycle` (enum) - New billing frequency
- `ChangeReason` (string) - Business justification

**Output (BillingAccountResult):** Same as above

---

## Related Documentation

- [Billing Payment Management](billing-payment.md) - Recording payments to billing accounts
- [Architecture Constitution](../../../../.specify/memory/constitution.md) - System-wide architectural rules
- [Manager Pattern](../../copilot-instructions/manager-and-facade.md) - Manager architecture guidelines
