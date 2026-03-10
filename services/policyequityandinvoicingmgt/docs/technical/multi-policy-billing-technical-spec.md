# PolicyEquityAndInvoicing Domain - Technical Specification (Multi-Policy)

**Domain**: PolicyEquityAndInvoicing  
**Version**: 2.0.0  
**Date**: February 5, 2026  
**Updates**: Added multi-policy PolicyEquityAndInvoicing data model and APIs

---

## API Surface Area

| HTTP Method | Endpoint | Description |
|-------------|----------|-------------|
| POST | `/api/policyequityandinvoicingmgt/record-payment` | Initiate payment for a specific policy |
| GET | `/api/policyequityandinvoicingmgt/accounts/{PolicyEquityAndInvoicingAccountId}` | Retrieve PolicyEquityAndInvoicing account with all policies |
| GET | `/api/policyequityandinvoicingmgt/accounts/{PolicyEquityAndInvoicingAccountId}/payments` | Retrieve payment history for PolicyEquityAndInvoicing account |

---

## Architecture Overview

The PolicyEquityAndInvoicing domain manages PolicyEquityAndInvoicing accounts with multi-policy support using:
- **API Layer**: HTTP endpoints for payment operations
- **Domain Layer**: PolicyEquityAndInvoicing account entities, payment logic, balance calculations
- **Infrastructure Layer**: Cosmos DB persistence, event publishing
- **Endpoint.In**: Message handlers for `PolicyIssued`, `FundsSettled`, `PolicyCancelled`

**Aggregate Root**: `PolicyEquityAndInvoicingAccount` (identified by `PolicyEquityAndInvoicingAccountId`)  
**Partition Key**: `/PolicyEquityAndInvoicingAccountId`

---

## Data Model

### PolicyEquityAndInvoicingAccount Document

**Cosmos DB Container**: `PolicyEquityAndInvoicing`  
**Partition Key**: `/PolicyEquityAndInvoicingAccountId`

```csharp
public class PolicyEquityAndInvoicingAccount
{
    [JsonPropertyName("id")]
    public string Id { get; set; }  // Same as PolicyEquityAndInvoicingAccountId
    
    [JsonPropertyName("PolicyEquityAndInvoicingAccountId")]
    public string PolicyEquityAndInvoicingAccountId { get; set; }  // Partition key (GUID)
    
    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = "PolicyEquityAndInvoicingAccount";
    
    // Customer Reference
    [JsonPropertyName("customerId")]
    public string CustomerId { get; set; }
    
    // Account Status
    [JsonPropertyName("status")]
    public string Status { get; set; }  // Active, PaidInFull, PastDue, Suspended
    
    // Multi-Policy Tracking
    [JsonPropertyName("policies")]
    public List<PolicyEquityAndInvoicingInfo> Policies { get; set; } = new();
    
    // Aggregate Balance
    [JsonPropertyName("totalBalance")]
    public decimal TotalBalance { get; set; }  // Sum of all policy balances
    
    // Audit
    [JsonPropertyName("createdUtc")]
    public DateTimeOffset CreatedUtc { get; set; }
    
    [JsonPropertyName("updatedUtc")]
    public DateTimeOffset UpdatedUtc { get; set; }
    
    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }
}

public class PolicyEquityAndInvoicingInfo
{
    [JsonPropertyName("policyId")]
    public string PolicyId { get; set; }
    
    [JsonPropertyName("policyNumber")]
    public string PolicyNumber { get; set; }
    
    [JsonPropertyName("premium")]
    public decimal Premium { get; set; }  // Total policy premium
    
    [JsonPropertyName("balance")]
    public decimal Balance { get; set; }  // Current amount owed for this policy
    
    [JsonPropertyName("effectiveDate")]
    public DateTimeOffset EffectiveDate { get; set; }
    
    [JsonPropertyName("expirationDate")]
    public DateTimeOffset ExpirationDate { get; set; }
    
    [JsonPropertyName("status")]
    public string Status { get; set; }  // Active, PaidInFull, Cancelled
    
    [JsonPropertyName("addedUtc")]
    public DateTimeOffset AddedUtc { get; set; }
}
```

---

### Payment Document

**Cosmos DB Container**: `PolicyEquityAndInvoicing`  
**Partition Key**: `/PolicyEquityAndInvoicingAccountId`  
**Document Type**: `Payment`

```csharp
public class Payment
{
    [JsonPropertyName("id")]
    public string Id { get; set; }  // PaymentId (GUID)
    
    [JsonPropertyName("PolicyEquityAndInvoicingAccountId")]
    public string PolicyEquityAndInvoicingAccountId { get; set; }  // Partition key
    
    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = "Payment";
    
    // Payment Details
    [JsonPropertyName("policyId")]
    public string PolicyId { get; set; }  // Which policy this payment is for
    
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }
    
    [JsonPropertyName("paymentMethodId")]
    public string PaymentMethodId { get; set; }
    
    [JsonPropertyName("fundTransferId")]
    public string? FundTransferId { get; set; }  // From FundsTransferMgt
    
    // Status Tracking
    [JsonPropertyName("status")]
    public string Status { get; set; }  // Pending, Settled, Failed, Refunded
    
    [JsonPropertyName("failureReason")]
    public string? FailureReason { get; set; }
    
    // Audit
    [JsonPropertyName("initiatedUtc")]
    public DateTimeOffset InitiatedUtc { get; set; }
    
    [JsonPropertyName("settledUtc")]
    public DateTimeOffset? SettledUtc { get; set; }
    
    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }
}
```

---

## Message Handlers

### PolicyIssuedHandler

**Purpose**: Handle `PolicyIssued` event from Policy domain

**Handler Location**: `services/PolicyEquityAndInvoicing/src/Endpoint.In/Handlers/PolicyIssuedHandler.cs`

**Process**:
1. Receive `PolicyIssued` event via RabbitMQ transport
2. Check if PolicyEquityAndInvoicing account exists for customerId
3. **If no account exists**: Create new PolicyEquityAndInvoicing account with first policy
4. **If account exists**: Add policy to existing account
5. Update TotalBalance (add premium)
6. Save PolicyEquityAndInvoicing account
7. Publish `PolicyEquityAndInvoicingAccountCreated` or `PolicyAdded` event

```csharp
public class PolicyIssuedHandler : IHandleMessages<PolicyIssued>
{
    private readonly IPolicyEquityAndInvoicingRepository _repository;
    
    public async Task Handle(PolicyIssued message, IMessageHandlerContext context)
    {
        // Check for existing PolicyEquityAndInvoicing account
        var account = await _repository.GetByCustomerIdAsync(message.CustomerId);
        
        if (account == null)
        {
            // Create new PolicyEquityAndInvoicing account
            account = new PolicyEquityAndInvoicingAccount
            {
                PolicyEquityAndInvoicingAccountId = Guid.NewGuid().ToString(),
                CustomerId = message.CustomerId,
                Status = "Active",
                Policies = new List<PolicyEquityAndInvoicingInfo>
                {
                    new PolicyEquityAndInvoicingInfo
                    {
                        PolicyId = message.PolicyId,
                        PolicyNumber = message.PolicyNumber,
                        Premium = message.Premium,
                        Balance = message.Premium,  // Initially full premium
                        EffectiveDate = message.EffectiveDate,
                        ExpirationDate = message.ExpirationDate,
                        Status = "Active",
                        AddedUtc = DateTimeOffset.UtcNow
                    }
                },
                TotalBalance = message.Premium,
                CreatedUtc = DateTimeOffset.UtcNow,
                UpdatedUtc = DateTimeOffset.UtcNow
            };
            
            await _repository.CreateAsync(account);
            
            await context.Publish(new PolicyEquityAndInvoicingAccountCreated(
                MessageId: Guid.NewGuid(),
                OccurredUtc: DateTimeOffset.UtcNow,
                PolicyEquityAndInvoicingAccountId: account.PolicyEquityAndInvoicingAccountId,
                CustomerId: message.CustomerId,
                PolicyId: message.PolicyId,
                Premium: message.Premium,
                IdempotencyKey: message.IdempotencyKey
            ));
        }
        else
        {
            // Add policy to existing account (multi-policy scenario)
            var existingPolicy = account.Policies.FirstOrDefault(p => p.PolicyId == message.PolicyId);
            if (existingPolicy != null)
            {
                _logger.LogInformation(
                    "Policy {PolicyId} already in PolicyEquityAndInvoicing account {PolicyEquityAndInvoicingAccountId}, skipping",
                    message.PolicyId, account.PolicyEquityAndInvoicingAccountId);
                return;  // Idempotency
            }
            
            account.Policies.Add(new PolicyEquityAndInvoicingInfo
            {
                PolicyId = message.PolicyId,
                PolicyNumber = message.PolicyNumber,
                Premium = message.Premium,
                Balance = message.Premium,
                EffectiveDate = message.EffectiveDate,
                ExpirationDate = message.ExpirationDate,
                Status = "Active",
                AddedUtc = DateTimeOffset.UtcNow
            });
            
            account.TotalBalance += message.Premium;
            account.UpdatedUtc = DateTimeOffset.UtcNow;
            
            await _repository.UpdateAsync(account);
            
            await context.Publish(new PolicyAdded(
                MessageId: Guid.NewGuid(),
                OccurredUtc: DateTimeOffset.UtcNow,
                PolicyEquityAndInvoicingAccountId: account.PolicyEquityAndInvoicingAccountId,
                PolicyId: message.PolicyId,
                Premium: message.Premium,
                UpdatedTotalBalance: account.TotalBalance,
                IdempotencyKey: message.IdempotencyKey
            ));
        }
        
        _logger.LogInformation(
            "Policy {PolicyNumber} added to PolicyEquityAndInvoicing account {PolicyEquityAndInvoicingAccountId}, TotalBalance: {TotalBalance}",
            message.PolicyNumber, account.PolicyEquityAndInvoicingAccountId, account.TotalBalance);
    }
}
```

**Idempotency**: Check if policy already exists in account before adding

**Events Published**: 
- `PolicyEquityAndInvoicingAccountCreated` (first policy)
- `PolicyAdded` (second+ policy)

---

### FundsSettledHandler

**Purpose**: Handle `FundsSettled` event from FundsTransferMgt domain

**Process**:
1. Receive `FundsSettled` event
2. Find payment document by fundTransferId
3. Update payment status to "Settled"
4. Find policy in PolicyEquityAndInvoicing account
5. Reduce policy balance by payment amount
6. Recalculate TotalBalance
7. Update account status if fully paid
8. Save PolicyEquityAndInvoicing account
9. Publish `PaymentRecorded` event

```csharp
public class FundsSettledHandler : IHandleMessages<FundsSettled>
{
    public async Task Handle(FundsSettled message, IMessageHandlerContext context)
    {
        // Find payment document
        var payment = await _repository.GetPaymentByFundTransferIdAsync(message.FundTransferId);
        if (payment == null)
        {
            _logger.LogWarning("Payment not found for FundTransferId {FundTransferId}", message.FundTransferId);
            return;
        }
        
        if (payment.Status == "Settled")
        {
            _logger.LogInformation("Payment {PaymentId} already settled, skipping", payment.Id);
            return;  // Idempotency
        }
        
        // Update payment status
        payment.Status = "Settled";
        payment.SettledUtc = DateTimeOffset.UtcNow;
        await _repository.UpdatePaymentAsync(payment);
        
        // Update PolicyEquityAndInvoicing account balance
        var account = await _repository.GetByIdAsync(payment.PolicyEquityAndInvoicingAccountId);
        var policy = account.Policies.FirstOrDefault(p => p.PolicyId == payment.PolicyId);
        
        if (policy == null)
        {
            _logger.LogError("Policy {PolicyId} not found in PolicyEquityAndInvoicing account {PolicyEquityAndInvoicingAccountId}",
                payment.PolicyId, payment.PolicyEquityAndInvoicingAccountId);
            return;
        }
        
        // Reduce policy balance
        policy.Balance -= payment.Amount;
        if (policy.Balance <= 0)
        {
            policy.Balance = 0;
            policy.Status = "PaidInFull";
        }
        
        // Recalculate total balance
        account.TotalBalance = account.Policies.Sum(p => p.Balance);
        
        // Update account status
        if (account.TotalBalance == 0)
            account.Status = "PaidInFull";
        else if (account.Policies.Any(p => p.Status == "Active"))
            account.Status = "Active";
        
        account.UpdatedUtc = DateTimeOffset.UtcNow;
        await _repository.UpdateAsync(account);
        
        // Publish event
        await context.Publish(new PaymentRecorded(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            PolicyEquityAndInvoicingAccountId: account.PolicyEquityAndInvoicingAccountId,
            PolicyId: payment.PolicyId,
            PaymentAmount: payment.Amount,
            RemainingPolicyBalance: policy.Balance,
            TotalAccountBalance: account.TotalBalance,
            IdempotencyKey: message.IdempotencyKey
        ));
        
        _logger.LogInformation(
            "Payment {PaymentId} settled, Policy {PolicyId} balance: {Balance}, Total balance: {TotalBalance}",
            payment.Id, payment.PolicyId, policy.Balance, account.TotalBalance);
    }
}
```

**Events Published**: `PaymentRecorded`

---

## API Endpoints

### POST /api/policyequityandinvoicingmgt/record-payment

**Purpose**: Initiate payment for a specific policy

**Request**:
```json
{
  "PolicyEquityAndInvoicingAccountId": "guid",
  "policyId": "guid",
  "amount": 1200.00,
  "paymentMethodId": "guid"
}
```

**Validation**:
- PolicyEquityAndInvoicing account must exist
- Policy must exist in account
- Amount must be > 0 and ≤ policy balance
- Payment method must be active

**Process**:
1. Validate payment amount against policy balance
2. Create Payment document with status "Pending"
3. Save payment
4. Publish `InitiateFundTransfer` command to FundsTransferMgt
5. Return 202 Accepted

**Response**: `202 Accepted`
```json
{
  "paymentId": "guid",
  "status": "Pending",
  "message": "Payment initiated, awaiting settlement"
}
```

**Events/Commands Published**: `InitiateFundTransfer` (to FundsTransferMgt)

---

### GET /api/policyequityandinvoicingmgt/accounts/{PolicyEquityAndInvoicingAccountId}

**Purpose**: Retrieve PolicyEquityAndInvoicing account with all policies

**Response**: `200 OK`
```json
{
  "PolicyEquityAndInvoicingAccountId": "guid",
  "customerId": "guid",
  "status": "Active",
  "totalBalance": 2000.00,
  "policies": [
    {
      "policyId": "guid",
      "policyNumber": "KWG-2026-000001",
      "premium": 1200.00,
      "balance": 1200.00,
      "status": "Active",
      "effectiveDate": "2026-03-01T00:00:00Z",
      "expirationDate": "2027-03-01T00:00:00Z"
    },
    {
      "policyId": "guid",
      "policyNumber": "KWG-2026-000002",
      "premium": 800.00,
      "balance": 800.00,
      "status": "Active",
      "effectiveDate": "2026-04-01T00:00:00Z",
      "expirationDate": "2027-04-01T00:00:00Z"
    }
  ]
}
```

---

### GET /api/policyequityandinvoicingmgt/accounts/{PolicyEquityAndInvoicingAccountId}/payments

**Purpose**: Retrieve payment history for PolicyEquityAndInvoicing account

**Query Parameters**:
- `policyId`: Filter by specific policy (optional)
- `status`: Filter by payment status (optional)

**Response**: `200 OK`
```json
{
  "payments": [
    {
      "paymentId": "guid",
      "policyId": "guid",
      "amount": 1200.00,
      "status": "Settled",
      "initiatedUtc": "2026-02-05T15:00:00Z",
      "settledUtc": "2026-02-05T15:05:00Z"
    }
  ]
}
```

---

## Domain Services

### BillingManager

**Interface**: `IBillingManager`

```csharp
public interface IBillingManager
{
    Task<PolicyEquityAndInvoicingAccount> CreatePolicyEquityAndInvoicingAccountAsync(PolicyIssued policyIssued);
    Task<PolicyEquityAndInvoicingAccount> AddPolicyToPolicyEquityAndInvoicingAccountAsync(string PolicyEquityAndInvoicingAccountId, PolicyIssued policyIssued);
    Task<Payment> RecordPaymentAsync(RecordPaymentRequest request);
    Task ProcessPaymentSettlementAsync(string paymentId, string fundTransferId);
    Task ProcessRefundAsync(string policyId, decimal refundAmount);
}
```

---

### BalanceCalculator

**Interface**: `IBalanceCalculator`

```csharp
public interface IBalanceCalculator
{
    decimal CalculateTotalBalance(PolicyEquityAndInvoicingAccount account);
    decimal CalculatePolicyBalance(PolicyEquityAndInvoicingInfo policy, IEnumerable<Payment> payments);
    bool ValidateBalanceConsistency(PolicyEquityAndInvoicingAccount account);
}
```

**Implementation**:
```csharp
public decimal CalculateTotalBalance(PolicyEquityAndInvoicingAccount account)
{
    return account.Policies.Sum(p => p.Balance);
}

public bool ValidateBalanceConsistency(PolicyEquityAndInvoicingAccount account)
{
    var calculatedTotal = account.Policies.Sum(p => p.Balance);
    return Math.Abs(account.TotalBalance - calculatedTotal) < 0.01m;  // Allow for rounding
}
```

---

## Repository Pattern

### IPolicyEquityAndInvoicingRepository

```csharp
public interface IPolicyEquityAndInvoicingRepository
{
    Task<PolicyEquityAndInvoicingAccount?> GetByIdAsync(string PolicyEquityAndInvoicingAccountId);
    Task<PolicyEquityAndInvoicingAccount?> GetByCustomerIdAsync(string customerId);
    Task<PolicyEquityAndInvoicingAccount> CreateAsync(PolicyEquityAndInvoicingAccount account);
    Task<PolicyEquityAndInvoicingAccount> UpdateAsync(PolicyEquityAndInvoicingAccount account);
    
    Task<Payment?> GetPaymentByIdAsync(string paymentId);
    Task<Payment?> GetPaymentByFundTransferIdAsync(string fundTransferId);
    Task<IEnumerable<Payment>> GetPaymentsByAccountAsync(string PolicyEquityAndInvoicingAccountId);
    Task<Payment> CreatePaymentAsync(Payment payment);
    Task<Payment> UpdatePaymentAsync(Payment payment);
}
```

**Implementation Notes**:
- `GetByCustomerIdAsync`: Cross-partition query (index on customerId or use secondary index)
- Co-locate payments in same partition as PolicyEquityAndInvoicing account

---

## Events Published

### PolicyEquityAndInvoicingAccountCreated

**Contract Location**: `services/PolicyEquityAndInvoicing/src/Domain/Contracts/Events/PolicyEquityAndInvoicingAccountCreated.cs`

```csharp
public record PolicyEquityAndInvoicingAccountCreated(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string PolicyEquityAndInvoicingAccountId,
    string CustomerId,
    string PolicyId,
    decimal Premium,
    string IdempotencyKey
);
```

---

### PolicyAdded

```csharp
public record PolicyAdded(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string PolicyEquityAndInvoicingAccountId,
    string PolicyId,
    decimal Premium,
    decimal UpdatedTotalBalance,
    string IdempotencyKey
);
```

---

### PaymentRecorded

```csharp
public record PaymentRecorded(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string PolicyEquityAndInvoicingAccountId,
    string PolicyId,
    decimal PaymentAmount,
    decimal RemainingPolicyBalance,
    decimal TotalAccountBalance,
    string IdempotencyKey
);
```

---

## Testing Strategy

### Unit Tests

- TotalBalance calculation (sum of policy balances)
- Payment allocation to specific policy
- Balance consistency validation
- Multi-policy scenarios (2, 3, 5 policies)
- Payment amount validation (cannot exceed balance)

### Integration Tests (Playwright)

- Create PolicyEquityAndInvoicing account on first policy issuance
- Add second policy to existing account
- Make targeted payment to specific policy
- Verify balance updates correctly
- Verify TotalBalance equals sum of policy balances

**Test Scenarios**:
- Single policy account (legacy)
- Two-policy account with targeted payments
- Three-policy account with mixed statuses (one paid, two active)
- Cancellation with refund in multi-policy account

---

## Performance Considerations

### Partition Strategy

- **Partition Key**: `/PolicyEquityAndInvoicingAccountId`
- **Hot Partition Risk**: Low (accounts evenly distributed by GUID)
- **Queries**: Point reads by PolicyEquityAndInvoicingAccountId, cross-partition by customerId

### Balance Consistency

- **Atomic Updates**: Use ETags for optimistic concurrency
- **Validation**: Always verify TotalBalance = sum(policy.Balance) before save
- **Retry Logic**: Retry on ETag conflicts

---

## Monitoring & Observability

### Metrics

- Payment success rate
- Multi-policy adoption rate (% of accounts with 2+ policies)
- Average TotalBalance per account
- Balance consistency errors (TotalBalance ≠ sum)
- Payment settlement time

### Logging

**Required Fields**:
- PolicyEquityAndInvoicingAccountId
- PolicyId (for targeted operations)
- Operation name
- Correlation ID

---

## Error Handling

### Validation Errors (400 Bad Request)

```json
{
  "error": "PaymentExceedsBalance",
  "message": "Payment amount ($1500) exceeds policy balance ($1200)",
  "policyBalance": 1200.00,
  "requestedAmount": 1500.00
}
```

---

## Future Enhancements

1. **Proportional Payment Allocation**: Split payment across multiple policies
2. **Auto-Pay**: Scheduled automatic payments
3. **Installment Plans**: Monthly/quarterly payment schedules
4. **Payment Reminders**: Email/SMS before due dates
5. **Dunning Workflows**: Automated collection processes
6. **Consolidated Invoices**: Single invoice for all policies
