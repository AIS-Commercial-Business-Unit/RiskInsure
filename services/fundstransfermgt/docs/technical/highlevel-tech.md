Here is the **technical documentation** for the `FundTransferMgt` bounded context, mapped directly to the business requirements defined earlier:

---

# FundTransferMgt Bounded Context – Technical Design Overview

## Purpose

This document describes the technical components, APIs, data models, and integration mechanisms for the `FundTransferMgt` bounded context. The purpose is to enable secure management of customer payment methods and to coordinate the authorization and transfer of funds in and out of the organization’s financial systems.

---

## Services Overview

* **PaymentMethodService**

  * Manages storage and lifecycle of ACH accounts, credit/debit cards, and wallet tokens
* **FundTransferService**

  * Executes and tracks funds movement (debits, credits)
* **AuthorizationGateway**

  * Interfaces with external financial institutions or payment processors
* **TransferEventPublisher**

  * Emits domain events related to payments, refunds, and method lifecycle

---

## Core APIs

### Payment Method Management

#### `POST /payment-methods/credit-card`

Adds a new credit card payment method for a customer.

* **Inputs**:

  * Payment Method ID (GUID - client-generated for idempotency)
  * Customer ID
  * Cardholder name
  * Card number (tokenized or passed to vault)
  * Expiration date
  * Billing address
* **Validation**:

  * PCI compliance checks
  * Luhn checksum
  * Supported card type check
* **Response**:

  * Payment Method ID
  * Status (e.g., `VALIDATED`, `REQUIRES_VERIFICATION`)
* **Events**:

  * `PaymentMethodAdded`

---

#### `POST /payment-methods/ach`

Adds a new ACH account for payment.

* **Inputs**:

  * Customer ID
  * Account holder name
  * Routing number
  * Account number (vaulted or encrypted)
  * Account type (checking/savings)
* **Validation**:

  * Routing number format check
  * Optional micro-deposit verification
* **Response**:

  * Payment Method ID
  * Status
* **Events**:

  * `PaymentMethodAdded`

---

#### `GET /payment-methods?customerId=XYZ`

Retrieves all valid payment methods for a customer.

* **Response**:

  * List of active methods (masked)

---

#### `DELETE /payment-methods/{id}`

Removes or invalidates a stored payment method.

* **Response**:

  * Status
* **Events**:

  * `PaymentMethodRemoved`

---

### Fund Transfers

#### `POST /fund-transfers`

Initiates a funds transfer from a selected payment method.

* **Inputs**:

  * Customer ID
  * Payment Method ID
  * Transfer amount
  * Purpose (e.g., `PREMIUM_PAYMENT`, `INITIAL_DEPOSIT`)
* **Validation**:

  * Method ownership
  * Sufficient payment method metadata
  * Transfer window and retry policy
* **Behavior**:

  * Authorizes and initiates funds transfer via `AuthorizationGateway`
* **Response**:

  * Transfer ID
  * Status (`PENDING`, `IN_PROGRESS`, `FAILED`, `COMPLETED`)
* **Events**:

  * `FundsTransferredIn`
  * `TransferAuthorizationFailed`

---

#### `POST /refunds`

Initiates a refund to a previously used payment method.

* **Inputs**:

  * Original transfer ID or transaction context
  * Refund amount
* **Validation**:

  * Refund eligibility
  * Original method still active or transferable
* **Response**:

  * Refund ID
  * Status
* **Events**:

  * `FundsRefunded`

---

#### `GET /fund-transfers/{id}`

Returns transfer status, including settlement information.

---

### Internal/Integration APIs

These may be used by downstream services (e.g., Billing, Claims):

* `POST /internal/fund-transfers/schedule`

  * Schedules a transfer to be executed in future
* `POST /internal/payment-methods/link-to-customer`

  * Used in migrations or special flows
* `POST /internal/webhooks/payment-status-updated`

  * Receives asynchronous updates from processors or gateways

---

## Data Model Summary

### PaymentMethod

* `id`: GUID
* `customerId`: GUID
* `type`: `CREDIT_CARD`, `ACH`, `WALLET`
* `status`: `VALIDATED`, `REVOKED`, `EXPIRED`, `INACTIVE`
* `details`: Encrypted or vaulted reference
* `createdAt`, `updatedAt`

### FundTransfer

* `id`: GUID
* `customerId`
* `paymentMethodId`
* `amount`
* `direction`: `INBOUND` or `OUTBOUND`
* `status`: `PENDING`, `IN_PROGRESS`, `COMPLETED`, `FAILED`, `REVERSED`
* `initiatedAt`, `settledAt`

---

## Events Published

| Event Name | Purpose | Trigger | Payload Summary | Contract Location |
|------------|---------|---------|-----------------|-------------------|
| `FundsSettled` | Payment successfully authorized and settled | Customer-initiated payment completes authorization and settlement | CustomerId, TransactionId, Amount, PaymentMethodId, SettledUtc | `platform/RiskInsure.PublicContracts/Events/FundsSettled.cs` |
| `FundsRefunded` | Refund processed and returned to customer | Refund transaction completes successfully | CustomerId, RefundId, OriginalTransactionId, Amount, RefundedUtc, Reason | `platform/RiskInsure.PublicContracts/Events/FundsRefunded.cs` |
| `PaymentMethodAdded` | New payment instrument validated | Customer adds payment method and validation succeeds | CustomerId, PaymentMethodId, MethodType, AddedUtc | `services/fundstransfermgt/src/Domain/Contracts/Events/PaymentMethodAdded.cs` |
| `PaymentMethodRemoved` | Payment instrument deleted/invalidated | Customer removes payment method or system invalidates it | CustomerId, PaymentMethodId, RemovalReason, RemovedUtc | `services/fundstransfermgt/src/Domain/Contracts/Events/PaymentMethodRemoved.cs` |
| `TransferAuthorizationFailed` | Payment authorization failed | Authorization attempt rejected by financial institution | CustomerId, TransactionId, PaymentMethodId, FailureReason, ErrorCode, FailedUtc | `services/fundstransfermgt/src/Domain/Contracts/Events/TransferAuthorizationFailed.cs` |

## Events Subscribed To

*This service does not currently subscribe to events from other domains.*

---

## Security and Compliance Considerations

* All payment methods are stored in a PCI-compliant vault or encrypted at rest
* Tokenization and never storing raw PAN or account numbers
* Strong authentication required for API access
* All transfers are auditable and signed with correlation IDs

---

Let me know if you'd like me to create an OpenAPI (Swagger) spec or YAML scaffolding for any of these endpoints.
