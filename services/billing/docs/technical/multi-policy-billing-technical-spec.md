# Billing Domain - Technical Specification (Multi-Policy)

**Domain**: Billing  
**Version**: 2.0.0  
**Date**: February 5, 2026  
**Updates**: Added multi-policy billing data model and APIs

---

## API Surface Area

| HTTP Method | Endpoint | Description |
|-------------|----------|-------------|
| POST | `/api/billing/record-payment` | Initiate payment for a specific policy |
| GET | `/api/billing/accounts/{billingAccountId}` | Retrieve billing account with all policies |
| GET | `/api/billing/accounts/{billingAccountId}/payments` | Retrieve payment history for billing account |

---

## Architecture Overview

The Billing domain manages billing accounts with multi-policy support using:
- **API Layer**: HTTP endpoints for payment operations
- **Domain Layer**: Billing account entities, payment logic, balance calculations
- **Infrastructure Layer**: Cosmos DB persistence, event publishing
- **Endpoint.In**: Message handlers for `PolicyIssued`, `FundsSettled`, `PolicyCancelled`

**Aggregate Root**: `BillingAccount` (identified by `BillingAccountId`)  
**Partition Key**: `/billingAccountId`

---

## Data Model

### BillingAccount Document

**Cosmos DB Container**: `billing`  
**Partition Key**: `/billingAccountId`

```csharp
public class BillingAccount
{
    [JsonPropertyName("id")]
    public string Id { get; set; }  // Same as BillingAccountId
    
    [JsonPropertyName("billingAccountId")]
    public string BillingAccountId { get; set; }  // Partition key (GUID)
    
    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = "BillingAccount";
    
    // Customer Reference
    [JsonPropertyName("customerId")]
    public string CustomerId { get; set; }
    
    // Account Status
    [JsonPropertyName("status")]
    public string Status { get; set; }  // Active, PaidInFull, PastDue, Suspended
    
    // Multi-Policy Tracking
    [JsonPropertyName("policies")]
    public List<PolicyBillingInfo> Policies { get; set; } = new();
    
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

public class PolicyBillingInfo
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

**Cosmos DB Container**: `billing`  
**Partition Key**: `/billingAccountId`  
**Document Type**: `Payment`

```csharp
public class Payment
{
    [JsonPropertyName("id")]
    public string Id { get; set; }  // PaymentId (GUID)
    
    [JsonPropertyName("billingAccountId")]
    public string BillingAccountId { get; set; }  // Partition key
    
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

**Handler Location**: `services/billing/src/Endpoint.In/Handlers/PolicyIssuedHandler.cs`

**Process**:
1. Receive `PolicyIssued` event from Service Bus
2. Check if billing account exists for customerId
3. **If no account exists**: Create new billing account with first policy
4. **If account exists**: Add policy to existing account
5. Update TotalBalance (add premium)
6. Save billing account
7. Publish `BillingAccountCreated` or `PolicyAdded` event

```csharp
public class PolicyIssuedHandler : IHandleMessages<PolicyIssued>
{
    private readonly IBillingRepository _repository;
    
    public async Task Handle(PolicyIssued message, IMessageHandlerContext context)
    {
        // Check for existing billing account
        var account = await _repository.GetByCustomerIdAsync(message.CustomerId);
        
        if (account == null)
        {
            // Create new billing account
            account = new BillingAccount
            {
                BillingAccountId = Guid.NewGuid().ToString(),
                CustomerId = message.CustomerId,
                Status = "Active",
                Policies = new List<PolicyBillingInfo>
                {
                    new PolicyBillingInfo
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
            
            await context.Publish(new BillingAccountCreated(
                MessageId: Guid.NewGuid(),
                OccurredUtc: DateTimeOffset.UtcNow,
                BillingAccountId: account.BillingAccountId,
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
                    "Policy {PolicyId} already in billing account {BillingAccountId}, skipping",
                    message.PolicyId, account.BillingAccountId);
                return;  // Idempotency
            }
            
            account.Policies.Add(new PolicyBillingInfo
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
                BillingAccountId: account.BillingAccountId,
                PolicyId: message.PolicyId,
                Premium: message.Premium,
                UpdatedTotalBalance: account.TotalBalance,
                IdempotencyKey: message.IdempotencyKey
            ));
        }
        
        _logger.LogInformation(
            "Policy {PolicyNumber} added to billing account {BillingAccountId}, TotalBalance: {TotalBalance}",
            message.PolicyNumber, account.BillingAccountId, account.TotalBalance);
    }
}
```

**Idempotency**: Check if policy already exists in account before adding

**Events Published**: 
- `BillingAccountCreated` (first policy)
- `PolicyAdded` (second+ policy)

---

### FundsSettledHandler

**Purpose**: Handle `FundsSettled` event from FundsTransferMgt domain

**Process**:
1. Receive `FundsSettled` event
2. Find payment document by fundTransferId
3. Update payment status to "Settled"
4. Find policy in billing account
5. Reduce policy balance by payment amount
6. Recalculate TotalBalance
7. Update account status if fully paid
8. Save billing account
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
        
        // Update billing account balance
        var account = await _repository.GetByIdAsync(payment.BillingAccountId);
        var policy = account.Policies.FirstOrDefault(p => p.PolicyId == payment.PolicyId);
        
        if (policy == null)
        {
            _logger.LogError("Policy {PolicyId} not found in billing account {BillingAccountId}",
                payment.PolicyId, payment.BillingAccountId);
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
            BillingAccountId: account.BillingAccountId,
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

### POST /api/billing/record-payment

**Purpose**: Initiate payment for a specific policy

**Request**:
```json
{
  "billingAccountId": "guid",
  "policyId": "guid",
  "amount": 1200.00,
  "paymentMethodId": "guid"
}
```

**Validation**:
- Billing account must exist
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

### GET /api/billing/accounts/{billingAccountId}

**Purpose**: Retrieve billing account with all policies

**Response**: `200 OK`
```json
{
  "billingAccountId": "guid",
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

### GET /api/billing/accounts/{billingAccountId}/payments

**Purpose**: Retrieve payment history for billing account

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
    Task<BillingAccount> CreateBillingAccountAsync(PolicyIssued policyIssued);
    Task<BillingAccount> AddPolicyToBillingAccountAsync(string billingAccountId, PolicyIssued policyIssued);
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
    decimal CalculateTotalBalance(BillingAccount account);
    decimal CalculatePolicyBalance(PolicyBillingInfo policy, IEnumerable<Payment> payments);
    bool ValidateBalanceConsistency(BillingAccount account);
}
```

**Implementation**:
```csharp
public decimal CalculateTotalBalance(BillingAccount account)
{
    return account.Policies.Sum(p => p.Balance);
}

public bool ValidateBalanceConsistency(BillingAccount account)
{
    var calculatedTotal = account.Policies.Sum(p => p.Balance);
    return Math.Abs(account.TotalBalance - calculatedTotal) < 0.01m;  // Allow for rounding
}
```

---

## Repository Pattern

### IBillingRepository

```csharp
public interface IBillingRepository
{
    Task<BillingAccount?> GetByIdAsync(string billingAccountId);
    Task<BillingAccount?> GetByCustomerIdAsync(string customerId);
    Task<BillingAccount> CreateAsync(BillingAccount account);
    Task<BillingAccount> UpdateAsync(BillingAccount account);
    
    Task<Payment?> GetPaymentByIdAsync(string paymentId);
    Task<Payment?> GetPaymentByFundTransferIdAsync(string fundTransferId);
    Task<IEnumerable<Payment>> GetPaymentsByAccountAsync(string billingAccountId);
    Task<Payment> CreatePaymentAsync(Payment payment);
    Task<Payment> UpdatePaymentAsync(Payment payment);
}
```

**Implementation Notes**:
- `GetByCustomerIdAsync`: Cross-partition query (index on customerId or use secondary index)
- Co-locate payments in same partition as billing account

---

## Events Published

### BillingAccountCreated

**Contract Location**: `services/billing/src/Domain/Contracts/Events/BillingAccountCreated.cs`

```csharp
public record BillingAccountCreated(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string BillingAccountId,
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
    string BillingAccountId,
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
    string BillingAccountId,
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

- Create billing account on first policy issuance
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

- **Partition Key**: `/billingAccountId`
- **Hot Partition Risk**: Low (accounts evenly distributed by GUID)
- **Queries**: Point reads by billingAccountId, cross-partition by customerId

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
- BillingAccountId
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
