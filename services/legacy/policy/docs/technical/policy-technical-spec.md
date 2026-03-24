# Policy Domain - Technical Specification

**Domain**: Policy  
**Version**: 1.0.0  
**Date**: February 5, 2026

---

## API Surface Area

| HTTP Method | Endpoint | Description |
|-------------|----------|-------------|
| POST | `/api/policies/{policyId}/issue` | Issue policy after payment method added |
| GET | `/api/policies/{policyId}` | Retrieve policy details |
| GET | `/api/customers/{customerId}/policies` | Retrieve all policies for a customer |
| POST | `/api/policies/{policyId}/cancel` | Cancel policy before expiration |
| POST | `/api/policies/{policyId}/reinstate` | Reinstate lapsed policy |

---

## Architecture Overview

The Policy domain manages policy lifecycle using:
- **API Layer**: HTTP endpoints for policy operations
- **Domain Layer**: Policy entities, state machine, business logic
- **Infrastructure Layer**: Cosmos DB persistence, event publishing
- **Endpoint.In**: Message handlers for `QuoteAccepted` event

**Aggregate Root**: `Policy` (identified by `PolicyId`)  
**Partition Key**: `/policyId`

---

## Data Model

### Policy Document

**Cosmos DB Container**: `policy`  
**Partition Key**: `/policyId`

```csharp
public class Policy
{
    [JsonPropertyName("id")]
    public string Id { get; set; }  // Same as PolicyId
    
    [JsonPropertyName("policyId")]
    public string PolicyId { get; set; }  // Partition key (GUID)
    
    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = "Policy";
    
    // Policy Identity
    [JsonPropertyName("policyNumber")]
    public string PolicyNumber { get; set; }  // KWG-2026-000001
    
    [JsonPropertyName("quoteId")]
    public string QuoteId { get; set; }  // Source quote
    
    [JsonPropertyName("customerId")]
    public string CustomerId { get; set; }
    
    // Policy Status & Dates
    [JsonPropertyName("status")]
    public string Status { get; set; }  // Bound, Issued, Active, Cancelled, Expired, Lapsed, Reinstated
    
    [JsonPropertyName("effectiveDate")]
    public DateTimeOffset EffectiveDate { get; set; }
    
    [JsonPropertyName("expirationDate")]
    public DateTimeOffset ExpirationDate { get; set; }
    
    [JsonPropertyName("boundDate")]
    public DateTimeOffset BoundDate { get; set; }
    
    [JsonPropertyName("issuedDate")]
    public DateTimeOffset? IssuedDate { get; set; }
    
    [JsonPropertyName("cancelledDate")]
    public DateTimeOffset? CancelledDate { get; set; }
    
    // Coverage Details (from accepted quote)
    [JsonPropertyName("structureCoverageLimit")]
    public decimal StructureCoverageLimit { get; set; }
    
    [JsonPropertyName("structureDeductible")]
    public decimal StructureDeductible { get; set; }
    
    [JsonPropertyName("contentsCoverageLimit")]
    public decimal ContentsCoverageLimit { get; set; }
    
    [JsonPropertyName("contentsDeductible")]
    public decimal ContentsDeductible { get; set; }
    
    [JsonPropertyName("termMonths")]
    public int TermMonths { get; set; }  // 6 or 12
    
    // Premium Information
    [JsonPropertyName("premium")]
    public decimal Premium { get; set; }  // Total premium for term
    
    // Cancellation Information
    [JsonPropertyName("cancellationReason")]
    public string? CancellationReason { get; set; }
    
    [JsonPropertyName("unearnedPremium")]
    public decimal? UnearnedPremium { get; set; }  // Refund amount
    
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

## Message Handlers

### QuoteAcceptedHandler

**Purpose**: Handle `QuoteAccepted` event from Rating & Underwriting domain

**Handler Location**: `services/policy/src/Endpoint.In/Handlers/QuoteAcceptedHandler.cs`

**Process**:
1. Receive `QuoteAccepted` event via RabbitMQ transport
2. Check if policy already exists for quoteId (idempotency)
3. Generate unique policyId and policy number
4. Create policy document with coverage terms from event
5. Set status to "Bound"
6. Save policy to Cosmos DB
7. Publish `PolicyBound` event

```csharp
public class QuoteAcceptedHandler : IHandleMessages<QuoteAccepted>
{
    private readonly IPolicyRepository _repository;
    private readonly IPolicyNumberGenerator _policyNumberGenerator;
    
    public async Task Handle(QuoteAccepted message, IMessageHandlerContext context)
    {
        // Idempotency check
        var existing = await _repository.GetByQuoteIdAsync(message.QuoteId);
        if (existing != null)
        {
            _logger.LogInformation(
                "Policy already exists for QuoteId {QuoteId}, skipping",
                message.QuoteId);
            return;
        }
        
        // Generate policy number
        var policyNumber = await _policyNumberGenerator.GenerateAsync();
        
        // Create policy
        var policy = new Policy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyNumber = policyNumber,
            QuoteId = message.QuoteId,
            CustomerId = message.CustomerId,
            Status = "Bound",
            EffectiveDate = message.EffectiveDate,
            ExpirationDate = message.EffectiveDate.AddMonths(message.TermMonths),
            BoundDate = DateTimeOffset.UtcNow,
            StructureCoverageLimit = message.StructureCoverageLimit,
            StructureDeductible = message.StructureDeductible,
            ContentsCoverageLimit = message.ContentsCoverageLimit,
            ContentsDeductible = message.ContentsDeductible,
            TermMonths = message.TermMonths,
            Premium = message.Premium,
            CreatedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow
        };
        
        await _repository.CreateAsync(policy);
        
        // Publish PolicyBound event
        await context.Publish(new PolicyBound(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            PolicyId: policy.PolicyId,
            PolicyNumber: policy.PolicyNumber,
            QuoteId: policy.QuoteId,
            CustomerId: policy.CustomerId,
            Premium: policy.Premium,
            EffectiveDate: policy.EffectiveDate,
            ExpirationDate: policy.ExpirationDate,
            IdempotencyKey: message.IdempotencyKey
        ));
        
        _logger.LogInformation(
            "Policy {PolicyNumber} bound for Quote {QuoteId}",
            policyNumber, message.QuoteId);
    }
}
```

**Idempotency**: Check for existing policy by quoteId before creating

**Events Published**: `PolicyBound`

---

## API Endpoints

### POST /api/policies/{policyId}/issue

**Purpose**: Issue policy after payment method added

**Request**: (Empty or confirmation)

**Validation**:
- Policy must exist
- Policy status must be "Bound"
- Effective date must not be in past

**Process**:
1. Retrieve policy by policyId
2. Verify status is "Bound"
3. Update status to "Issued"
4. Set issuedDate timestamp
5. Save policy
6. Publish `PolicyIssued` event (triggers billing account creation)
7. Generate policy documents (PDF)

**Response**: `200 OK`
```json
{
  "policyId": "guid",
  "policyNumber": "KWG-2026-000001",
  "status": "Issued",
  "issuedDate": "2026-02-05T15:00:00Z"
}
```

**Events Published**: `PolicyIssued` (critical - triggers billing)

**Error Responses**:
- `404 Not Found`: Policy does not exist
- `409 Conflict`: Policy not in "Bound" status

---

### GET /api/policies/{policyId}

**Purpose**: Retrieve policy details

**Response**: `200 OK`
```json
{
  "policyId": "guid",
  "policyNumber": "KWG-2026-000001",
  "customerId": "guid",
  "status": "Issued",
  "effectiveDate": "2026-03-01T00:00:00Z",
  "expirationDate": "2027-03-01T00:00:00Z",
  "structureCoverageLimit": 200000,
  "structureDeductible": 1000,
  "contentsCoverageLimit": 50000,
  "contentsDeductible": 500,
  "termMonths": 12,
  "premium": 1350.00,
  "boundDate": "2026-02-05T14:30:00Z",
  "issuedDate": "2026-02-05T15:00:00Z"
}
```

---

### GET /api/customers/{customerId}/policies

**Purpose**: Retrieve all policies for a customer

**Query Parameters**:
- `status`: Filter by status (Active, Expired, Cancelled)
- `includeExpired`: Include expired policies (default: false)

**Response**: `200 OK`
```json
{
  "policies": [
    {
      "policyId": "guid",
      "policyNumber": "KWG-2026-000001",
      "status": "Active",
      "effectiveDate": "2026-03-01T00:00:00Z",
      "expirationDate": "2027-03-01T00:00:00Z",
      "premium": 1350.00
    }
  ]
}
```

**Note**: Cross-partition query (by customerId)

---

### POST /api/policies/{policyId}/cancel

**Purpose**: Cancel policy before expiration

**Request**:
```json
{
  "cancellationDate": "2026-06-01T00:00:00Z",
  "reason": "Customer request"
}
```

**Validation**:
- Policy must be Active or Issued
- Cancellation date ≥ current date + 1 day
- Cancellation date ≤ expiration date

**Process**:
1. Calculate unearned premium (pro-rata refund)
2. Update policy status to "Cancelled"
3. Set cancellation date and reason
4. Save policy
5. Publish `PolicyCancelled` event (triggers refund in Billing)

**Unearned Premium Calculation**:
```csharp
public decimal CalculateUnearnedPremium(Policy policy, DateTimeOffset cancellationDate)
{
    var totalDays = (policy.ExpirationDate - policy.EffectiveDate).TotalDays;
    var daysRemaining = (policy.ExpirationDate - cancellationDate).TotalDays;
    
    if (daysRemaining <= 0)
        return 0m;
    
    var unearnedPercentage = daysRemaining / totalDays;
    return Math.Round(policy.Premium * (decimal)unearnedPercentage, 2);
}
```

**Response**: `200 OK`
```json
{
  "policyId": "guid",
  "status": "Cancelled",
  "cancellationDate": "2026-06-01T00:00:00Z",
  "unearnedPremium": 675.00
}
```

**Events Published**: `PolicyCancelled`

---

### POST /api/policies/{policyId}/reinstate

**Purpose**: Reinstate lapsed policy

**Request**:
```json
{
  "paymentAmount": 1350.00,
  "paymentConfirmationId": "guid"
}
```

**Validation**:
- Policy status must be "Lapsed"
- Lapsed < 30 days ago
- Payment amount equals past due + current premium
- No claims during lapse period

**Response**: `200 OK`

**Events Published**: `PolicyReinstated`

---

## Domain Services

### PolicyManager

**Interface**: `IPolicyManager`

```csharp
public interface IPolicyManager
{
    Task<Policy> CreateFromQuoteAsync(QuoteAccepted quote);
    Task<Policy> IssuePolicyAsync(string policyId);
    Task<Policy> CancelPolicyAsync(string policyId, DateTimeOffset cancellationDate, string reason);
    Task<Policy> ReinstatePolicyAsync(string policyId);
    Task<Policy> GetPolicyAsync(string policyId);
    Task ExpirePoliciesAsync();  // Background job
}
```

---

### PolicyNumberGenerator

**Interface**: `IPolicyNumberGenerator`

```csharp
public interface IPolicyNumberGenerator
{
    Task<string> GenerateAsync();
}
```

**Implementation**:
```csharp
public class PolicyNumberGenerator : IPolicyNumberGenerator
{
    // Format: KWG-{YEAR}-{SEQUENCE}
    // Sequence increments per year, stored in Cosmos DB
    
    public async Task<string> GenerateAsync()
    {
        var year = DateTime.UtcNow.Year;
        var sequence = await GetNextSequenceAsync(year);
        return $"KWG-{year}-{sequence:D6}";  // KWG-2026-000001
    }
    
    private async Task<int> GetNextSequenceAsync(int year)
    {
        // Use Cosmos DB counter document with optimistic concurrency
        // Retry on ETag conflict
    }
}
```

---

## Repository Pattern

### IPolicyRepository

```csharp
public interface IPolicyRepository
{
    Task<Policy?> GetByIdAsync(string policyId);
    Task<Policy?> GetByQuoteIdAsync(string quoteId);
    Task<Policy?> GetByPolicyNumberAsync(string policyNumber);
    Task<IEnumerable<Policy>> GetByCustomerIdAsync(string customerId);
    Task<Policy> CreateAsync(Policy policy);
    Task<Policy> UpdateAsync(Policy policy);
    Task<IEnumerable<Policy>> GetExpirablePoliciesAsync(DateTimeOffset currentDate);
}
```

**Implementation Notes**:
- `GetByQuoteIdAsync`: Requires secondary index or cross-partition query
- `GetExpirablePoliciesAsync`: Finds policies where `expirationDate < currentDate` and `status = 'Active'`

---

## Events Published

### PolicyBound

**Contract Location**: `services/policy/src/Domain/Contracts/Events/PolicyBound.cs`

```csharp
namespace RiskInsure.Policy.Domain.Contracts.Events;

public record PolicyBound(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string PolicyId,
    string PolicyNumber,
    string QuoteId,
    string CustomerId,
    decimal Premium,
    DateTimeOffset EffectiveDate,
    DateTimeOffset ExpirationDate,
    string IdempotencyKey
);
```

---

### PolicyIssued

**Contract Location**: `platform/RiskInsure.PublicContracts/Events/PolicyIssued.cs`

```csharp
namespace RiskInsure.PublicContracts.Events;

public record PolicyIssued(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string PolicyId,
    string PolicyNumber,
    string CustomerId,
    decimal StructureCoverageLimit,
    decimal StructureDeductible,
    decimal ContentsCoverageLimit,
    decimal ContentsDeductible,
    int TermMonths,
    DateTimeOffset EffectiveDate,
    DateTimeOffset ExpirationDate,
    decimal Premium,
    string IdempotencyKey
);
```

**Subscribers**: 
- **Billing domain** (creates billing account)
- **Premium domain** (starts earning calculations - future)

---

### PolicyCancelled

```csharp
public record PolicyCancelled(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string PolicyId,
    string CustomerId,
    DateTimeOffset CancellationDate,
    string CancellationReason,
    decimal UnearnedPremium,
    string IdempotencyKey
);
```

**Subscribers**: **Billing domain** (process refund)

---

## Background Jobs

### Policy Expiration Job

**Purpose**: Expire policies on natural expiration date

**Schedule**: Runs daily at 3:00 AM UTC

**Process**:
1. Query policies where `expirationDate < currentDate` and `status = 'Active'`
2. Update status to "Expired"
3. Publish `PolicyExpired` event
4. Send expiration notification to customer

---

## Testing Strategy

### Unit Tests

- Policy number generation (unique, sequential)
- Status transitions (valid/invalid)
- Unearned premium calculation
- Policy creation from quote
- Cancellation logic
- Reinstatement eligibility

### Integration Tests (Playwright)

- Quote acceptance → Policy binding (cross-domain)
- Policy issuance workflow
- Policy cancellation and refund
- Policy expiration
- Customer policy retrieval

**Test Scenarios**:
- Happy path: Quote → Policy → Issue → Active
- Cancellation: Active → Cancelled with refund
- Expiration: Active → Expired (no refund)
- Idempotency: Duplicate QuoteAccepted events

---

## Performance Considerations

### Partition Strategy

- **Partition Key**: `/policyId`
- **Hot Partition Risk**: Low (policies evenly distributed by GUID)
- **Query Patterns**: Point reads by policyId, cross-partition queries by customerId

### Caching

- **Policy Number Sequence**: Cache current sequence number in memory
- **Policy Documents**: No caching (always read from Cosmos for accuracy)

---

## Monitoring & Observability

### Metrics

- Policy creation rate (per hour)
- Bind-to-issue conversion rate
- Policy lapse rate
- Cancellation rate
- Average time to issue
- Policy document generation time

### Logging

**Required Fields**:
- PolicyId
- PolicyNumber
- CustomerId
- Operation name
- Correlation ID

**Key Events to Log**:
- Policy bound (from quote)
- Policy issued
- Policy cancelled
- Policy expired
- Policy reinstated

---

## Error Handling

### Validation Errors (400 Bad Request)

```json
{
  "error": "ValidationFailed",
  "errors": {
    "CancellationDate": ["Cancellation date must be at least 1 day in the future"]
  }
}
```

### Conflict Errors (409 Conflict)

```json
{
  "error": "InvalidPolicyStatus",
  "message": "Policy must be in 'Bound' status to issue",
  "currentStatus": "Issued"
}
```

---

## Future Enhancements

1. **Policy Endorsements**: Mid-term coverage changes with premium adjustments
2. **Renewal Workflows**: Auto-generate renewal policies
3. **Multi-Line Policies**: Bundle multiple coverages
4. **Policy Versioning**: Track all policy changes with versions
5. **Document Management**: Store and retrieve policy PDFs
6. **Claims Verification**: Verify coverage on claim submission
