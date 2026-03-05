# Premium Domain - Technical Specification

**Domain**: Premium  
**Version**: 1.0.0  
**Date**: February 5, 2026  
**Status**: Design complete, implementation deferred post-MVP

---

## API Surface Area (Future)

| HTTP Method | Endpoint | Description |
|-------------|----------|-------------|
| GET | `/api/premium/policies/{policyId}/earning` | Retrieve earned vs. unearned premium for policy |
| GET | `/api/premium/reports/daily-earning` | Generate daily earning report for all active policies |

---

## Architecture Overview

The Premium domain will manage earned vs. unearned premium calculations using:
- **API Layer**: HTTP endpoints for premium reports and queries
- **Domain Layer**: Earning calculation engine, premium entities
- **Infrastructure Layer**: Cosmos DB persistence, event publishing
- **Endpoint.In**: Message handlers for `PolicyIssued`, `PolicyCancelled`

**Aggregate Root**: `PremiumEarning` (identified by `PolicyId`)  
**Partition Key**: `/policyId`

---

## Data Model (Future Implementation)

### PremiumEarning Document

**Cosmos DB Container**: `premium`  
**Partition Key**: `/policyId`

```csharp
public class PremiumEarning
{
    [JsonPropertyName("id")]
    public string Id { get; set; }  // Same as PolicyId
    
    [JsonPropertyName("policyId")]
    public string PolicyId { get; set; }  // Partition key
    
    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = "PremiumEarning";
    
    // Policy Reference
    [JsonPropertyName("policyNumber")]
    public string PolicyNumber { get; set; }
    
    [JsonPropertyName("customerId")]
    public string CustomerId { get; set; }
    
    // Premium Amounts
    [JsonPropertyName("totalPremium")]
    public decimal TotalPremium { get; set; }
    
    [JsonPropertyName("earnedToDate")]
    public decimal EarnedToDate { get; set; }
    
    [JsonPropertyName("unearnedBalance")]
    public decimal UnearnedBalance { get; set; }
    
    // Policy Dates
    [JsonPropertyName("effectiveDate")]
    public DateTimeOffset EffectiveDate { get; set; }
    
    [JsonPropertyName("expirationDate")]
    public DateTimeOffset ExpirationDate { get; set; }
    
    [JsonPropertyName("cancellationDate")]
    public DateTimeOffset? CancellationDate { get; set; }
    
    // Status
    [JsonPropertyName("status")]
    public string Status { get; set; }  // Active, Finalized, Cancelled
    
    // Calculation Metadata
    [JsonPropertyName("lastCalculatedUtc")]
    public DateTimeOffset LastCalculatedUtc { get; set; }
    
    [JsonPropertyName("earningMethod")]
    public string EarningMethod { get; set; } = "ProRata";  // ProRata, 365ths
    
    // Audit
    [JsonPropertyName("createdUtc")]
    public DateTimeOffset CreatedUtc { get; set; }
    
    [JsonPropertyName("updatedUtc")]
    public DateTimeOffset UpdatedUtc { get; set; }
    
    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }
}
```

---

### EarningHistory Document (Optional)

**Purpose**: Track daily earning calculations for audit trail

```csharp
public class EarningHistory
{
    [JsonPropertyName("id")]
    public string Id { get; set; }  // Guid
    
    [JsonPropertyName("policyId")]
    public string PolicyId { get; set; }  // Partition key
    
    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = "EarningHistory";
    
    [JsonPropertyName("calculationDate")]
    public DateTimeOffset CalculationDate { get; set; }
    
    [JsonPropertyName("earnedAmount")]
    public decimal EarnedAmount { get; set; }  // Earned on this date
    
    [JsonPropertyName("cumulativeEarned")]
    public decimal CumulativeEarned { get; set; }
    
    [JsonPropertyName("unearnedBalance")]
    public decimal UnearnedBalance { get; set; }
}
```

---

## Message Handlers (Future)

### PolicyIssuedHandler

**Purpose**: Handle `PolicyIssued` event and initialize earning calculation

**Process**:
1. Receive `PolicyIssued` event via RabbitMQ transport
2. Create PremiumEarning document
3. Set status to "Active"
4. Initialize earned = $0, unearned = total premium
5. Save document

```csharp
public class PolicyIssuedHandler : IHandleMessages<PolicyIssued>
{
    public async Task Handle(PolicyIssued message, IMessageHandlerContext context)
    {
        var premiumEarning = new PremiumEarning
        {
            PolicyId = message.PolicyId,
            PolicyNumber = message.PolicyNumber,
            CustomerId = message.CustomerId,
            TotalPremium = message.Premium,
            EarnedToDate = 0m,
            UnearnedBalance = message.Premium,
            EffectiveDate = message.EffectiveDate,
            ExpirationDate = message.ExpirationDate,
            Status = "Active",
            LastCalculatedUtc = DateTimeOffset.UtcNow,
            EarningMethod = "ProRata",
            CreatedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow
        };
        
        await _repository.CreateAsync(premiumEarning);
        
        _logger.LogInformation(
            "Premium earning initialized for Policy {PolicyNumber}, Total Premium: {TotalPremium}",
            message.PolicyNumber, message.Premium);
    }
}
```

---

### PolicyCancelledHandler

**Purpose**: Finalize earning calculation on cancellation

**Process**:
1. Receive `PolicyCancelled` event
2. Calculate final earned premium through cancellation date
3. Calculate unearned premium (refund amount)
4. Update status to "Finalized"
5. Publish `PremiumAdjusted` event with refund amount

```csharp
public class PolicyCancelledHandler : IHandleMessages<PolicyCancelled>
{
    private readonly IEarningCalculator _calculator;
    
    public async Task Handle(PolicyCancelled message, IMessageHandlerContext context)
    {
        var premiumEarning = await _repository.GetByPolicyIdAsync(message.PolicyId);
        if (premiumEarning == null)
        {
            _logger.LogWarning("PremiumEarning not found for Policy {PolicyId}", message.PolicyId);
            return;
        }
        
        // Calculate final earned premium through cancellation date
        var finalEarned = _calculator.CalculateEarnedPremium(
            premiumEarning.TotalPremium,
            premiumEarning.EffectiveDate,
            message.CancellationDate,
            premiumEarning.ExpirationDate
        );
        
        var unearnedPremium = premiumEarning.TotalPremium - finalEarned;
        
        premiumEarning.EarnedToDate = finalEarned;
        premiumEarning.UnearnedBalance = unearnedPremium;
        premiumEarning.CancellationDate = message.CancellationDate;
        premiumEarning.Status = "Finalized";
        premiumEarning.LastCalculatedUtc = DateTimeOffset.UtcNow;
        premiumEarning.UpdatedUtc = DateTimeOffset.UtcNow;
        
        await _repository.UpdateAsync(premiumEarning);
        
        // Publish adjustment event
        await context.Publish(new PremiumAdjusted(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            PolicyId: message.PolicyId,
            FinalEarnedPremium: finalEarned,
            UnearnedPremium: unearnedPremium,
            AdjustmentReason: "Cancellation",
            IdempotencyKey: message.IdempotencyKey
        ));
        
        _logger.LogInformation(
            "Premium earning finalized for Policy {PolicyId}, Earned: {Earned}, Unearned: {Unearned}",
            message.PolicyId, finalEarned, unearnedPremium);
    }
}
```

---

## Domain Services (Future)

### EarningCalculator

**Interface**: `IEarningCalculator`

```csharp
public interface IEarningCalculator
{
    decimal CalculateEarnedPremium(
        decimal totalPremium,
        DateTimeOffset effectiveDate,
        DateTimeOffset asOfDate,
        DateTimeOffset expirationDate
    );
    
    decimal CalculateUnearnedPremium(
        decimal totalPremium,
        decimal earnedPremium
    );
    
    decimal CalculateDailyEarningRate(
        decimal totalPremium,
        DateTimeOffset effectiveDate,
        DateTimeOffset expirationDate
    );
}
```

**Implementation** (Pro-Rata Method):
```csharp
public class ProRataEarningCalculator : IEarningCalculator
{
    public decimal CalculateEarnedPremium(
        decimal totalPremium,
        DateTimeOffset effectiveDate,
        DateTimeOffset asOfDate,
        DateTimeOffset expirationDate)
    {
        // Ensure asOfDate is within policy term
        if (asOfDate < effectiveDate)
            return 0m;
        
        if (asOfDate >= expirationDate)
            return totalPremium;
        
        // Calculate days elapsed and total days
        var daysElapsed = (asOfDate - effectiveDate).TotalDays;
        var totalDays = (expirationDate - effectiveDate).TotalDays;
        
        if (totalDays == 0)
            return totalPremium;
        
        // Calculate earned amount
        var earnedPercentage = daysElapsed / totalDays;
        var earnedAmount = totalPremium * (decimal)earnedPercentage;
        
        return Math.Round(earnedAmount, 2);
    }
    
    public decimal CalculateUnearnedPremium(decimal totalPremium, decimal earnedPremium)
    {
        return totalPremium - earnedPremium;
    }
    
    public decimal CalculateDailyEarningRate(
        decimal totalPremium,
        DateTimeOffset effectiveDate,
        DateTimeOffset expirationDate)
    {
        var totalDays = (expirationDate - effectiveDate).TotalDays;
        if (totalDays == 0)
            return totalPremium;
        
        return Math.Round(totalPremium / (decimal)totalDays, 4);
    }
}
```

---

## Background Jobs (Future)

### Daily Earning Job

**Purpose**: Calculate earned premium for all active policies

**Schedule**: Runs daily at 1:00 AM UTC

**Process**:
```csharp
public class DailyEarningJob
{
    public async Task ExecuteAsync()
    {
        var activePolicies = await _repository.GetActivePoliciesAsync();
        var currentDate = DateTimeOffset.UtcNow.Date;
        
        foreach (var batch in activePolicies.Chunk(1000))
        {
            await Parallel.ForEachAsync(batch, async (policy, ct) =>
            {
                var earnedToDate = _calculator.CalculateEarnedPremium(
                    policy.TotalPremium,
                    policy.EffectiveDate,
                    currentDate,
                    policy.ExpirationDate
                );
                
                policy.EarnedToDate = earnedToDate;
                policy.UnearnedBalance = policy.TotalPremium - earnedToDate;
                policy.LastCalculatedUtc = DateTimeOffset.UtcNow;
                policy.UpdatedUtc = DateTimeOffset.UtcNow;
                
                await _repository.UpdateAsync(policy);
                
                // Optional: Create earning history record
                await _repository.CreateEarningHistoryAsync(new EarningHistory
                {
                    PolicyId = policy.PolicyId,
                    CalculationDate = currentDate,
                    EarnedAmount = earnedToDate - policy.EarnedToDate,  // Daily increment
                    CumulativeEarned = earnedToDate,
                    UnearnedBalance = policy.UnearnedBalance
                });
            });
        }
        
        _logger.LogInformation(
            "Daily earning calculation completed for {Count} policies",
            activePolicies.Count());
    }
}
```

---

## API Endpoints (Future)

### GET /api/premium/policies/{policyId}/earning

**Purpose**: Retrieve current earned vs. unearned premium for policy

**Response**: `200 OK`
```json
{
  "policyId": "guid",
  "policyNumber": "KWG-2026-000001",
  "totalPremium": 1200.00,
  "earnedToDate": 300.00,
  "unearnedBalance": 900.00,
  "lastCalculatedUtc": "2026-05-01T01:00:00Z",
  "earningPercentage": 25.0,
  "status": "Active"
}
```

---

### GET /api/premium/reports/daily-earning

**Purpose**: Generate daily earning report for all active policies

**Query Parameters**:
- `date`: Report date (defaults to today)

**Response**: `200 OK`
```json
{
  "reportDate": "2026-05-01",
  "totalActivePolicies": 1500,
  "totalPremiumInForce": 1800000.00,
  "totalEarnedToDate": 450000.00,
  "totalUnearnedBalance": 1350000.00,
  "dailyEarnedAmount": 5000.00
}
```

---

## Repository Pattern (Future)

### IPremiumRepository

```csharp
public interface IPremiumRepository
{
    Task<PremiumEarning?> GetByPolicyIdAsync(string policyId);
    Task<IEnumerable<PremiumEarning>> GetActivePoliciesAsync();
    Task<PremiumEarning> CreateAsync(PremiumEarning premiumEarning);
    Task<PremiumEarning> UpdateAsync(PremiumEarning premiumEarning);
    Task CreateEarningHistoryAsync(EarningHistory history);
    Task<IEnumerable<EarningHistory>> GetEarningHistoryAsync(string policyId);
}
```

---

## Events Published (Future)

### PremiumEarned

```csharp
public record PremiumEarned(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string PolicyId,
    decimal EarnedAmount,  // Daily increment
    decimal CumulativeEarned,
    decimal RemainingUnearned,
    string IdempotencyKey
);
```

---

### PremiumAdjusted

```csharp
public record PremiumAdjusted(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string PolicyId,
    decimal FinalEarnedPremium,
    decimal UnearnedPremium,
    string AdjustmentReason,  // Cancellation, Endorsement
    string IdempotencyKey
);
```

---

## Testing Strategy (Future)

### Unit Tests

- Earned premium calculation (pro-rata)
- Boundary conditions (effective date, expiration date)
- Leap year handling
- Daily earning rate calculation
- Cancellation scenarios

**Test Cases**:
- Policy effective today: earned = $0
- Policy at expiration: earned = total premium
- Mid-term cancellation: earned proportional to days elapsed
- 6-month policy earning (180-183 days)
- 12-month policy earning (365-366 days)

### Integration Tests

- Daily earning job execution
- Policy cancellation with earning adjustment
- Cross-domain integration (PolicyCancelled → PremiumAdjusted → Billing refund)

---

## Performance Considerations (Future)

### Batch Processing

- Process policies in batches of 1,000
- Parallel processing for daily earning job
- Target: Complete earning calculation in < 5 minutes for 10,000 policies

### Caching

- Cache earning calculation parameters
- No caching of earned amounts (always calculate fresh)

---

## Monitoring & Observability (Future)

### Metrics

- Daily earning job execution time
- Number of policies processed
- Earning calculation errors
- Reconciliation with Billing domain

### Logging

**Required Fields**:
- PolicyId
- Calculation date
- Earned amount
- Correlation ID

---

## Future Enhancements

1. **365ths Method**: Alternative earning calculation for annual policies
2. **Endorsement Support**: Mid-term premium adjustments
3. **Installment Plan Integration**: Earning vs. payment timing reconciliation
4. **Loss Ratio Reporting**: Earned premium vs. claims incurred
5. **Reconciliation Workflows**: Auto-reconcile with Billing domain
6. **Advanced Analytics**: Profitability analysis, territory-based earning

---

## Implementation Timeline

**Phase 1** (Post-MVP): Basic earning calculation and daily job  
**Phase 2**: Cancellation handling and refund integration  
**Phase 3**: Reporting and reconciliation  
**Phase 4**: Advanced features (endorsements, analytics)
