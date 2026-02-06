# Fund Transfers – Technical Design Overview

## Purpose

This document describes the technical components, APIs, data models, and integration mechanisms for the **Fund Transfers** subdomain within the `FundTransferMgt` bounded context. The purpose is to enable secure authorization, execution, and tracking of fund movements between customers and the insurance company.

---

## Services Overview

* **FundTransferService**
  * Executes and tracks funds movement (debits, credits)
  * Manages transfer lifecycle and status transitions
  * Coordinates retry policies for failed transfers

* **AuthorizationGateway**
  * Interfaces with external financial institutions or payment processors
  * Handles authorization requests and responses
  * Manages settlement confirmation

* **TransferEventPublisher**
  * Emits domain events related to fund transfers and refunds
  * Notifies downstream systems of transfer status changes

---

## Core APIs

### Fund Transfers

#### `POST /fund-transfers`

Initiates a funds transfer from a selected payment method.

**Inputs**:
* Transfer ID (GUID - client-generated for idempotency)
* Customer ID
* Payment Method ID
* Transfer amount (decimal)
* Purpose/Reason (e.g., `PREMIUM_PAYMENT`, `INITIAL_DEPOSIT`, `OUTSTANDING_BALANCE`)
* Metadata (optional): Additional context for the transfer

**Validation**:
* Payment method exists and belongs to customer
* Payment method status is `VALIDATED`
* Transfer amount is positive and within allowed limits
* Customer has authorization for the transfer type

**Processing**:
1. Validate input and payment method ownership
2. Create transfer record with status `PENDING`
3. Call AuthorizationGateway to authorize transfer
4. Update status based on authorization result:
   * Success: Status → `IN_PROGRESS`
   * Failure: Status → `FAILED`
5. Publish appropriate event

**Response**:
* `202 Accepted` (asynchronous processing)
* Transfer ID
* Initial status (`PENDING`)
* Location header with status endpoint

**Events Published**:
* `FundsTransferInitiated` (on successful initiation)
* `TransferAuthorizationFailed` (on authorization failure)
* `FundsSettled` (when settlement completes)

**Error Responses**:
* `400 Bad Request`: Invalid input (negative amount, missing fields)
* `404 Not Found`: Payment method does not exist
* `403 Forbidden`: Payment method does not belong to customer
* `422 Unprocessable Entity`: Payment method not in VALIDATED status

---

#### `GET /fund-transfers/{id}`

Returns transfer status, including settlement information.

**Response**:
* `200 OK`
* Transfer details:
  ```json
  {
    "id": "guid",
    "customerId": "guid",
    "paymentMethodId": "guid",
    "amount": 150.00,
    "direction": "INBOUND",
    "purpose": "PREMIUM_PAYMENT",
    "status": "COMPLETED",
    "initiatedAt": "2026-02-05T10:00:00Z",
    "authorizedAt": "2026-02-05T10:00:15Z",
    "settledAt": "2026-02-06T08:30:00Z",
    "gatewayTransactionId": "ext-txn-12345",
    "failureReason": null,
    "errorCode": null
  }
  ```

**Error Responses**:
* `404 Not Found`: Transfer does not exist

---

#### `GET /fund-transfers?customerId={customerId}`

Returns all fund transfers for a customer.

**Query Parameters**:
* `customerId` (required): Customer identifier
* `status` (optional): Filter by status
* `startDate` (optional): Filter transfers after this date
* `endDate` (optional): Filter transfers before this date
* `direction` (optional): Filter by `INBOUND` or `OUTBOUND`

**Response**:
* `200 OK`
* Array of transfer records (most recent first)

---

### Refunds

#### `POST /refunds`

Initiates a refund to a previously used payment method.

**Inputs**:
* Refund ID (GUID - client-generated for idempotency)
* Original Transfer ID (reference to original transaction)
* Refund Amount (decimal, must not exceed original amount)
* Refund Reason (e.g., `CLAIM_SETTLEMENT`, `POLICY_CANCELLATION`, `OVERPAYMENT`)
* Metadata (optional)

**Validation**:
* Original transfer exists and is `COMPLETED`
* Refund amount does not exceed original transfer amount
* Original payment method still exists and is refundable
* Refund is within allowed time window

**Processing**:
1. Validate original transfer eligibility
2. Create refund record with status `PENDING`
3. Call AuthorizationGateway to initiate refund
4. Update status based on result
5. Publish appropriate event

**Response**:
* `202 Accepted`
* Refund ID
* Initial status (`PENDING`)

**Events Published**:
* `RefundInitiated`
* `FundsRefunded` (when refund completes)
* `RefundFailed` (on failure)

**Error Responses**:
* `400 Bad Request`: Refund amount exceeds original
* `404 Not Found`: Original transfer does not exist
* `422 Unprocessable Entity`: Original transfer not eligible for refund

---

#### `GET /refunds/{id}`

Returns refund status.

**Response**:
* `200 OK`
* Refund details similar to fund transfer structure

---

### Internal/Integration APIs

#### `POST /internal/fund-transfers/schedule`

Schedules a transfer to be executed at a future date/time.

**Inputs**:
* All standard transfer fields
* `scheduledFor` (DateTimeOffset): When to execute the transfer

**Response**:
* `202 Accepted`
* Transfer ID
* Scheduled execution time

**Events Published**:
* `TransferScheduled`

---

#### `POST /internal/webhooks/payment-status-updated`

Receives asynchronous updates from payment processors or gateways.

**Inputs**:
* Gateway transaction ID
* New status
* Settlement details
* Timestamp

**Processing**:
1. Find transfer by gateway transaction ID
2. Update transfer status
3. Publish appropriate event

---

## Data Model

### FundTransfer

**Document Structure** (Cosmos DB):
```csharp
public class FundTransfer
{
    [JsonPropertyName("id")]
    public string Id { get; set; }  // GUID
    
    [JsonPropertyName("customerId")]
    public string CustomerId { get; set; }  // Partition key
    
    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = "FundTransfer";
    
    [JsonPropertyName("paymentMethodId")]
    public string PaymentMethodId { get; set; }
    
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }
    
    [JsonPropertyName("direction")]
    public string Direction { get; set; }  // INBOUND (debit), OUTBOUND (refund)
    
    [JsonPropertyName("purpose")]
    public string Purpose { get; set; }  // PREMIUM_PAYMENT, CLAIM_REFUND, etc.
    
    [JsonPropertyName("status")]
    public string Status { get; set; }  // PENDING, IN_PROGRESS, COMPLETED, FAILED, REVERSED
    
    [JsonPropertyName("initiatedAt")]
    public DateTimeOffset InitiatedAt { get; set; }
    
    [JsonPropertyName("authorizedAt")]
    public DateTimeOffset? AuthorizedAt { get; set; }
    
    [JsonPropertyName("settledAt")]
    public DateTimeOffset? SettledAt { get; set; }
    
    [JsonPropertyName("gatewayTransactionId")]
    public string? GatewayTransactionId { get; set; }
    
    [JsonPropertyName("failureReason")]
    public string? FailureReason { get; set; }
    
    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }
    
    [JsonPropertyName("retryCount")]
    public int RetryCount { get; set; }
    
    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
    
    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }
}
```

### Refund

**Document Structure** (Cosmos DB):
```csharp
public class Refund
{
    [JsonPropertyName("id")]
    public string Id { get; set; }  // GUID
    
    [JsonPropertyName("customerId")]
    public string CustomerId { get; set; }  // Partition key
    
    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = "Refund";
    
    [JsonPropertyName("originalTransferId")]
    public string OriginalTransferId { get; set; }
    
    [JsonPropertyName("paymentMethodId")]
    public string PaymentMethodId { get; set; }
    
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }
    
    [JsonPropertyName("reason")]
    public string Reason { get; set; }
    
    [JsonPropertyName("status")]
    public string Status { get; set; }  // PENDING, IN_PROGRESS, COMPLETED, FAILED
    
    [JsonPropertyName("initiatedAt")]
    public DateTimeOffset InitiatedAt { get; set; }
    
    [JsonPropertyName("completedAt")]
    public DateTimeOffset? CompletedAt { get; set; }
    
    [JsonPropertyName("gatewayRefundId")]
    public string? GatewayRefundId { get; set; }
    
    [JsonPropertyName("failureReason")]
    public string? FailureReason { get; set; }
    
    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
    
    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }
}
```

**Partition Strategy**:
* Container: `fundstransfermgt`
* Partition Key: `/customerId`
* Co-located with payment method documents for efficient querying

---

## Events Published

| Event Name | Trigger | Payload Summary | Contract Location |
|------------|---------|-----------------|-------------------|
| `FundsSettled` | Payment successfully authorized and settled | CustomerId, TransactionId, Amount, PaymentMethodId, SettledUtc | `platform/RiskInsure.PublicContracts/Events/FundsSettled.cs` |
| `FundsRefunded` | Refund processed and returned to customer | CustomerId, RefundId, OriginalTransactionId, Amount, RefundedUtc, Reason | `platform/RiskInsure.PublicContracts/Events/FundsRefunded.cs` |
| `TransferAuthorizationFailed` | Payment authorization failed | CustomerId, TransactionId, PaymentMethodId, FailureReason, ErrorCode, FailedUtc | `services/fundstransfermgt/src/Domain/Contracts/Events/TransferAuthorizationFailed.cs` |
| `TransferScheduled` | Payment scheduled for future execution | CustomerId, TransactionId, ScheduledFor | `services/fundstransfermgt/src/Domain/Contracts/Events/TransferScheduled.cs` |
| `FundsTransferReversed` | Transfer reversed due to dispute/error | CustomerId, TransactionId, ReversalReason, ReversedUtc | `services/fundstransfermgt/src/Domain/Contracts/Events/FundsTransferReversed.cs` |
| `TransferSettlementCompleted` | Settlement batch processed | BatchId, TransferIds, SettledUtc | `services/fundstransfermgt/src/Domain/Contracts/Events/TransferSettlementCompleted.cs` |

---

## Integration Points

### Payment Gateway
* **Purpose**: Authorize and process fund transfers
* **Integration**: HTTPS API calls to payment processor
* **Flow**: 
  1. Transfer request → Gateway authorization API
  2. Authorization response → Update transfer status
  3. Settlement webhook → Update to COMPLETED

### Payment Method Service
* **Purpose**: Validate payment method eligibility
* **Integration**: Internal API call or repository access
* **Flow**: Transfer request → Validate payment method → Proceed if valid

### Billing Service
* **Purpose**: Receive transfer requests for premium payments
* **Integration**: Listens for billing events, sends transfer requests
* **Flow**: Premium due → Transfer request → FundsSettled event → Billing updates

### Claims Service
* **Purpose**: Request refund transfers
* **Integration**: Sends refund requests for claim settlements
* **Flow**: Claim approved → Refund request → FundsRefunded event → Claim updated

---

## Retry and Error Handling

### Retry Policy
* **Failed Authorizations**: Up to 3 retry attempts
* **Backoff Strategy**: Exponential backoff (1s, 2s, 4s)
* **Permanent Failures**: After max retries, mark as `FAILED`
* **Retry Tracking**: Increment `retryCount` field

### Error Codes
* `INSUFFICIENT_FUNDS`: Payment method has insufficient balance
* `PAYMENT_METHOD_INVALID`: Payment method revoked or expired
* `GATEWAY_ERROR`: Payment gateway unavailable or error
* `AUTHORIZATION_DECLINED`: Financial institution declined
* `NETWORK_ERROR`: Network connectivity issue

### Settlement Monitoring
* Monitor for settlement confirmation within expected window
* Alert on delayed settlements
* Handle settlement failures with appropriate status updates

---

## Repository Pattern

### TransactionRepository

**Interface** (`Domain/Repositories/ITransactionRepository.cs`):
```csharp
public interface ITransactionRepository
{
    Task<FundTransfer?> GetTransferByIdAsync(string id);
    Task<IEnumerable<FundTransfer>> GetTransfersByCustomerIdAsync(string customerId);
    Task<FundTransfer> CreateTransferAsync(FundTransfer transfer);
    Task<FundTransfer> UpdateTransferAsync(FundTransfer transfer);
    
    Task<Refund?> GetRefundByIdAsync(string id);
    Task<IEnumerable<Refund>> GetRefundsByCustomerIdAsync(string customerId);
    Task<Refund> CreateRefundAsync(Refund refund);
    Task<Refund> UpdateRefundAsync(Refund refund);
}
```

**Implementation** (`Domain/Repositories/TransactionRepository.cs`):
* Uses Cosmos DB SDK directly
* Queries by partition key (`/customerId`) for efficiency
* Implements optimistic concurrency using ETags
* Maps between domain models and Cosmos DB documents

---

## Observability

### Logging
* All transfer lifecycle events logged with correlation IDs
* Authorization requests/responses logged
* Settlement confirmations logged
* Failure reasons logged with error codes

### Metrics
* Authorization success rate
* Average authorization time
* Settlement completion time
* Failed transfer rate
* Refund processing time

### Alerting
* Alert on authorization failure rate > threshold
* Alert on delayed settlements
* Alert on refund failures
* Alert on gateway errors

---

## Security

### Authorization
* Transfers require customer authentication
* Validate payment method ownership before transfer
* API keys for internal/integration endpoints
* Gateway credentials managed via Azure Key Vault

### Audit Trail
* All transfer operations logged with customer ID, timestamps
* Immutable audit log for compliance
* Correlation IDs for distributed tracing

### PCI Compliance
* Token references used (no raw card data in transfer records)
* Secure communication with payment gateway (TLS 1.2+)
* Access controls on transfer data

---

## Testing Strategy

### Unit Tests
* Transfer validation logic
* Status transition logic
* Retry policy behavior

### Integration Tests
* API endpoint testing via Playwright
* Repository operations with Cosmos DB Emulator
* Event publishing verification
* Authorization gateway mocking

### End-to-End Tests
* Complete transfer workflow (initiation → authorization → settlement)
* Refund workflow testing
* Failed authorization handling
* Settlement delay scenarios

---

## Future Enhancements

* Scheduled recurring transfers
* Transfer batching for efficiency
* Real-time payment support (RTP/FedNow)
* International transfers
* Multi-currency support
* Fraud detection integration
