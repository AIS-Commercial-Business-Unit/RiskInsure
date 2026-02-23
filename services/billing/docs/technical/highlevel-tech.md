# Billing Bounded Context – Technical Design Overview

## Purpose

This document describes the technical components, APIs, data models, and integration mechanisms for the `Billing` bounded context. The purpose is to enable secure management of insurance policy billing accounts and payment processing, tracking premium obligations, payment receipts, and account lifecycle transitions.

---

## Services Overview

* **BillingAccountService**
  * Manages lifecycle of billing accounts (create, activate, suspend, close)
  * Controls premium amounts and billing cycle preferences
  * Enforces account status transitions and business rules

* **BillingPaymentService**
  * Records customer payments against active billing accounts
  * Maintains account balance accuracy through payment application
  * Enforces payment validation rules (minimum amounts, balance limits)

* **EventPublisher**
  * Publishes domain events for account lifecycle changes
  * Emits payment events for downstream processing and notifications
  * Supports both synchronous and asynchronous processing patterns

---

## Core APIs

### Billing Account Management

#### `POST /api/billing/accounts`

Creates a new billing account for an insurance policy.

* **Inputs**:
  * Account ID (system-generated GUID or provided)
  * Customer ID
  * Policy Number (must be unique per customer)
  * Policy Holder Name
  * Current Premium Owed (must be ≥ $0.00)
  * Billing Cycle (Monthly, Quarterly, SemiAnnual, Annual)
  * Effective Date (policy start date)

* **Validation**:
  * Premium amount must be non-negative
  * Policy number uniqueness per customer
  * Effective date cannot be more than 90 days in past
  * Idempotent: duplicate creation with same Account ID succeeds silently

* **Response**:
  * 201 Created with Account ID
  * Account starts in **Pending** status

* **Events**:
  * `BillingAccountCreated`

**Example Request**:
```json
{
  "accountId": "ACC-12345",
  "customerId": "CUST-67890",
  "policyNumber": "POL-2026-001",
  "policyHolderName": "John Smith",
  "currentPremiumOwed": 1200.00,
  "billingCycle": "Monthly",
  "effectiveDate": "2026-02-01T00:00:00Z"
}
```

---

#### `PUT /api/billing/accounts/{accountId}/premium`

Updates the premium amount owed on an existing account.

* **Inputs**:
  * Account ID (path parameter)
  * New Premium Owed (must be ≥ $0.00)
  * Change Reason (business justification)

* **Validation**:
  * Account must exist
  * Account cannot be in Closed status
  * Premium must be non-negative

* **Response**:
  * 200 OK with updated premium
  * 404 Not Found if account doesn't exist
  * 400 Bad Request if account is closed or premium is invalid

* **Events**:
  * `PremiumOwedUpdated`

**Example Request**:
```json
{
  "newPremiumOwed": 1500.00,
  "changeReason": "Policy coverage increased - added comprehensive coverage"
}
```

---

#### `POST /api/billing/accounts/{accountId}/activate`

Activates a pending account to enable payment processing.

* **Inputs**:
  * Account ID (path parameter)

* **Validation**:
  * Account must exist
  * Idempotent: activating already-active account succeeds

* **Response**:
  * 200 OK
  * 404 Not Found if account doesn't exist

* **Events**:
  * `AccountActivated` (only if state changed from Pending)

---

#### `POST /api/billing/accounts/{accountId}/suspend`

Suspends an account to prevent payment processing.

* **Inputs**:
  * Account ID (path parameter)
  * Suspension Reason (e.g., "Non-payment", "Policy lapse")

* **Validation**:
  * Account must exist
  * Cannot suspend already-closed account
  * Idempotent: suspending already-suspended account succeeds

* **Response**:
  * 200 OK
  * 404 Not Found if account doesn't exist
  * 400 Bad Request if account is closed

* **Events**:
  * `AccountSuspended` (only if state changed)

**Example Request**:
```json
{
  "suspensionReason": "Non-payment - 60 days past due"
}
```

---

#### `POST /api/billing/accounts/{accountId}/close`

Permanently closes a billing account.

* **Inputs**:
  * Account ID (path parameter)
  * Closure Reason (e.g., "Policy terminated", "Policy cancelled")

* **Validation**:
  * Account must exist
  * Idempotent: closing already-closed account succeeds
  * Warning logged if outstanding balance > $0

* **Response**:
  * 200 OK
  * 404 Not Found if account doesn't exist

* **Events**:
  * `AccountClosed` (only if state changed)

**Example Request**:
```json
{
  "closureReason": "Policy terminated - vehicle sold"
}
```

---

#### `PUT /api/billing/accounts/{accountId}/billing-cycle`

Updates the billing frequency preference for an account.

* **Inputs**:
  * Account ID (path parameter)
  * New Billing Cycle (Monthly, Quarterly, SemiAnnual, Annual)
  * Change Reason

* **Validation**:
  * Account must exist
  * Account cannot be in Closed status

* **Response**:
  * 200 OK
  * 404 Not Found if account doesn't exist
  * 400 Bad Request if account is closed

* **Events**:
  * `BillingCycleUpdated`

**Example Request**:
```json
{
  "newBillingCycle": "Quarterly",
  "changeReason": "Customer requested quarterly billing"
}
```

---

#### `GET /api/billing/accounts`

Retrieves all billing accounts in the system.

* **Response**:
  * 200 OK with array of accounts (empty array if none exist)
  * Each account includes: AccountId, CustomerId, PolicyNumber, Status, Premium, Balance details

---

#### `GET /api/billing/accounts/{accountId}`

Retrieves a specific billing account by ID.

* **Response**:
  * 200 OK with account details
  * 404 Not Found if account doesn't exist

---

### Payment Processing

#### `POST /api/billing/payments` (Synchronous)

Records a payment to a billing account with immediate validation and response.

* **Inputs**:
  * Account ID
  * Amount (must be ≥ $1.00, cannot exceed outstanding balance)
  * Reference Number (unique per account - check number, ACH trace, wire ID)

* **Validation**:
  * Account must exist and be Active
  * Payment amount must be positive and ≥ $1.00 (minimum threshold)
  * Payment cannot exceed outstanding balance (no overpayment)
  * Reference number must be unique per account (duplicate detection)

* **Behavior**:
  * Reduces outstanding balance by payment amount
  * Increases total paid by payment amount
  * Idempotent: duplicate reference number returns existing account state

* **Response**:
  * 200 OK with updated account balances
  * 400 Bad Request if validation fails
  * 404 Not Found if account doesn't exist

* **Events**:
  * `PaymentReceived` (only for new payments, not duplicates)

**Example Request**:
```json
{
  "accountId": "ACC-12345",
  "amount": 250.00,
  "referenceNumber": "ACH-98765"
}
```

**Example Response**:
```json
{
  "message": "Payment successfully recorded",
  "accountId": "ACC-12345",
  "amount": 250.00,
  "referenceNumber": "ACH-98765",
  "totalPaid": 1250.00,
  "outstandingBalance": 750.00,
  "wasDuplicate": false
}
```

---

#### `POST /api/billing/payments/async` (Asynchronous)

Submits a payment command for background processing via NServiceBus.

* **Inputs**:
  * Same as synchronous endpoint

* **Validation**:
  * Basic structural validation only
  * Full business rule validation occurs in message handler

* **Behavior**:
  * Publishes `RecordPayment` command to NServiceBus
  * Returns immediately without waiting for processing

* **Response**:
  * 202 Accepted - command queued for processing
  * 400 Bad Request if structural validation fails

* **Events**:
  * `PaymentReceived` (published by handler after processing)

**Use Case**: Large batch payment imports, webhook processing from external payment gateways

---

#### `GET /api/billing/payments/health`

Health check endpoint for monitoring.

* **Response**:
  * 200 OK with service status

---

## Data Model Summary

### BillingAccount

Primary aggregate managing insurance policy billing lifecycle.

* `id`: string (GUID) - Unique account identifier
* `customerId`: string - Customer/policyholder identifier
* `policyNumber`: string - Insurance policy number (unique per customer)
* `policyHolderName`: string - Name of policy holder
* `status`: enum - `Pending`, `Active`, `Suspended`, `Closed`
* `currentPremiumOwed`: decimal - Current premium obligation
* `outstandingBalance`: decimal - Amount owed (premium - payments)
* `totalPaid`: decimal - Cumulative payments received
* `billingCycle`: enum - `Monthly`, `Quarterly`, `SemiAnnual`, `Annual`
* `effectiveDate`: DateTimeOffset - Policy effective date
* `createdUtc`: DateTimeOffset - Account creation timestamp
* `updatedUtc`: DateTimeOffset - Last modification timestamp

### BillingPayment

Individual payment record within an account.

* `referenceNumber`: string - Unique payment identifier (check #, ACH trace, etc.)
* `amount`: decimal - Payment amount
* `recordedUtc`: DateTimeOffset - When payment was recorded
* `idempotencyKey`: string - Format: `{AccountId}:{ReferenceNumber}`

---

## Events Published

| Event Name | Purpose | Trigger | Payload Summary | Contract Location |
|------------|---------|---------|-----------------|-------------------|
| `BillingAccountCreated` | New billing account created for policy | Account creation API called successfully | AccountId, CustomerId, PolicyNumber, Premium, BillingCycle, EffectiveDate | `services/billing/src/Domain/Contracts/Events/BillingAccountCreated.cs` |
| `PremiumOwedUpdated` | Premium amount adjusted | Premium update API called | AccountId, OldPremium, NewPremium, ChangeReason | `services/billing/src/Domain/Contracts/Events/PremiumOwedUpdated.cs` |
| `AccountActivated` | Account activated for payments | Activation API called | AccountId, PolicyNumber | `services/billing/src/Domain/Contracts/Events/AccountActivated.cs` |
| `AccountSuspended` | Account suspended | Suspension API called | AccountId, PolicyNumber, SuspensionReason | `services/billing/src/Domain/Contracts/Events/AccountSuspended.cs` |
| `AccountClosed` | Account permanently closed | Close API called | AccountId, PolicyNumber, ClosureReason, FinalBalance | `services/billing/src/Domain/Contracts/Events/AccountClosed.cs` |
| `BillingCycleUpdated` | Billing frequency changed | Billing cycle update API called | AccountId, OldCycle, NewCycle, ChangeReason | `services/billing/src/Domain/Contracts/Events/BillingCycleUpdated.cs` |
| `PaymentReceived` | Payment received and applied to account | Payment recorded successfully | AccountId, Amount, ReferenceNumber, TotalPaid, OutstandingBalance | `platform/RiskInsure.PublicContracts/Events/PaymentReceived.cs` |

## Events Subscribed To

| Event Name | Publishing Domain | Handler Name | Action | Contract Location |
|------------|-------------------|--------------|--------|-------------------|
| `FundsSettled` | FundTransferMgt | `FundsSettledHandler` | Applies settled funds as payment to billing account, updates balance | `platform/RiskInsure.PublicContracts/Events/FundsSettled.cs` |
| `FundsRefunded` | FundTransferMgt | `FundsRefundedHandler` | Reverses payment application, adjusts account balance for refunded amount | `platform/RiskInsure.PublicContracts/Events/FundsRefunded.cs` |

---

## Processing Patterns

### Synchronous Operations (Direct Manager Call)

Used for operations requiring immediate feedback:

* **Account Lifecycle**: Create, Activate, Suspend, Close
* **Premium Updates**: Update premium amount, billing cycle
* **Payment Recording**: `POST /api/billing/payments`

**Flow**:
```
API Controller → Manager (Domain) → Repository → Database → Events → Return Result
```

### Asynchronous Operations (Command Publishing)

Used for batch processing or webhook integration:

* **Payment Recording**: `POST /api/billing/payments/async`

**Flow**:
```
API Controller → Publish Command → Return 202 Accepted
(Later) Handler → Manager (Domain) → Repository → Database → Events
```

---

## Security and Compliance Considerations

* All API endpoints require authentication (development mode currently without auth, production will use JWT)
* Payment reference numbers are immutable once recorded (audit trail)
* All operations include idempotency keys for safe retry
* Account status transitions are logged for compliance
* Outstanding balance warnings on account closure for collection tracking
* Structured logging includes AccountId, CustomerId for audit correlation

---

## Business Rules Reference

### Account Creation
* Premium must be ≥ $0.00
* Policy number must be unique per customer
* Effective date cannot be >90 days in past
* All accounts start in `Pending` status

### Payment Processing
* Only `Active` accounts can receive payments
* Minimum payment: $1.00 (prevents processing costs exceeding payment value)
* Maximum payment: Outstanding balance (no overpayment allowed)
* Reference numbers must be unique per account (duplicate detection)
* Duplicate payments are idempotent (return existing state without error)

### Account Status Transitions
* `Pending` → `Active`: Via activate endpoint
* `Active` → `Suspended`: Via suspend endpoint
* `Suspended` → `Active`: Via activate endpoint
* Any status → `Closed`: Via close endpoint (permanent, irreversible)
* `Closed` accounts cannot be modified

---

## Integration Points

### Downstream Consumers

**Premium Billing System**:
* Subscribes to: `BillingAccountCreated`, `PremiumOwedUpdated`, `BillingCycleUpdated`
* Purpose: Set up and adjust recurring billing schedules

**Payment Processing System**:
* Subscribes to: `AccountActivated`, `AccountSuspended`, `AccountClosed`
* Purpose: Enable/disable payment acceptance

**Policy Management System**:
* Subscribes to: All account lifecycle events
* Purpose: Synchronize policy and billing account states

**Accounting System**:
* Subscribes to: `PaymentReceived`
* Purpose: Update financial records and reconciliation

**Customer Service Portal**:
* Subscribes to: `PaymentReceived`
* Purpose: Display real-time payment status

**Notification Service**:
* Subscribes to: All events
* Purpose: Send customer communications (email/SMS)

**Collections System**:
* Subscribes to: `AccountSuspended`, `PaymentReceived`
* Purpose: Manage delinquency and payment plans

**Analytics/Reporting**:
* Subscribes to: All events
* Purpose: Track account metrics, payment trends, premium changes

---

## NServiceBus Configuration

### Development Mode
* Connection strings from `appsettings.Development.json`
* Retries disabled for faster iteration
* Installers enabled (auto-creates queues)
* Uses Cosmos DB emulator and RabbitMQ connection string

### Production Mode
* Managed Identity (`DefaultAzureCredential`)
* Retry policies configured
* Requires `ConnectionStrings:RabbitMQ` (or `RabbitMQ:ConnectionString`) and `CosmosDb:Endpoint`
* Saga persistence in Cosmos DB (`Billing-Sagas` container)

### Message Conventions
* Commands: Namespace ends with `.Commands` (e.g., `RecordPayment`)
* Events: Namespace ends with `.Events` (e.g., `PaymentReceived`)
* All messages include: `MessageId`, `OccurredUtc`, `IdempotencyKey`

---

## Error Handling

### Error Codes

| Code                       | Meaning                                    | Retryable | HTTP Status |
| -------------------------- | ------------------------------------------ | --------- | ----------- |
| `ACCOUNT_NOT_FOUND`        | Account ID does not exist                  | No        | 404         |
| `DUPLICATE_POLICY_NUMBER`  | Policy number already exists for customer  | No        | 400         |
| `NEGATIVE_PREMIUM`         | Premium amount is negative                 | No        | 400         |
| `INVALID_EFFECTIVE_DATE`   | Effective date >90 days in past            | No        | 400         |
| `ACCOUNT_CLOSED`           | Operation not allowed on closed account    | No        | 400         |
| `INVALID_ACCOUNT_STATUS`   | Account status prevents operation          | No        | 400         |
| `INVALID_AMOUNT`           | Payment amount ≤ 0                         | No        | 400         |
| `AMOUNT_BELOW_MINIMUM`     | Payment < $1.00 minimum threshold          | No        | 400         |
| `PAYMENT_EXCEEDS_BALANCE`  | Payment > outstanding balance              | No        | 400         |

### Idempotency Keys

* **Account Creation**: `account-created-{AccountId}`
* **Account Activation**: `account-activated-{AccountId}`
* **Payment Recording**: `{AccountId}:{ReferenceNumber}`
* **Premium Update**: `premium-updated-{AccountId}-{Timestamp}`
* **Billing Cycle Update**: `cycle-updated-{AccountId}-{Timestamp}`

---

## Testing Strategy

### API Integration Tests (Playwright)
* Located in: `test/Integration.Tests`
* Technology: Node.js + Playwright (NOT .NET)
* Tests full HTTP request/response cycles
* Requires API running on `http://localhost:7071`

**Commands**:
```bash
npm test                # Headless
npm run test:ui         # Interactive UI (recommended)
npm run test:headed     # Browser visible
npm run test:debug      # Step-through debugger
```

### Unit Tests (xUnit)
* Located in: `test/Unit.Tests`
* Tests manager business logic
* Mocks repositories and message sessions
* Target: 90%+ coverage for Domain layer

---

## Related Documentation

* **[Business Requirements - Billing Account](../business/billing-account.md)** - Account lifecycle business rules
* **[Business Requirements - Billing Payment](../business/billing-payment.md)** - Payment processing business rules
* **[Constitution](../../../../.specify/memory/constitution.md)** - Architectural principles
* **[Project Structure](../../../../copilot-instructions/project-structure.md)** - Multi-layer architecture template
