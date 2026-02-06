# Rating & Underwriting - Technical Specification

**Domain**: Rating & Underwriting  
**Version**: 1.0.0  
**Date**: February 5, 2026

---

## API Surface Area

| HTTP Method | Endpoint | Description |
|-------------|----------|-------------|
| POST | `/api/quotes/start` | Initiate new insurance quote |
| POST | `/api/quotes/{quoteId}/submit-underwriting` | Submit underwriting information for risk evaluation |
| POST | `/api/quotes/{quoteId}/calculate` | Calculate premium (auto-called after underwriting approval) |
| POST | `/api/quotes/{quoteId}/accept` | Customer accepts quote and coverage terms |
| GET | `/api/quotes/{quoteId}` | Retrieve quote details |
| GET | `/api/customers/{customerId}/quotes` | Retrieve all quotes for a customer |

---

## Architecture Overview

The Rating & Underwriting domain manages quote lifecycle, premium calculation, and risk evaluation using:
- **API Layer**: HTTP endpoints for quote operations
- **Domain Layer**: Rating engine, underwriting rules, quote entities
- **Infrastructure Layer**: Cosmos DB persistence, event publishing
- **Endpoint.In**: Currently no message subscriptions (future: rate changes, territory updates)

**Aggregate Root**: `Quote` (identified by `QuoteId`)  
**Partition Key**: `/quoteId`

---

## Data Model

### Quote Document

**Cosmos DB Container**: `ratingunderwriting`  
**Partition Key**: `/quoteId`

```csharp
public class Quote
{
    [JsonPropertyName("id")]
    public string Id { get; set; }  // Same as QuoteId
    
    [JsonPropertyName("quoteId")]
    public string QuoteId { get; set; }  // Partition key (GUID)
    
    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = "Quote";
    
    // Customer Reference
    [JsonPropertyName("customerId")]
    public string CustomerId { get; set; }
    
    // Quote Metadata
    [JsonPropertyName("status")]
    public string Status { get; set; }  // Draft, UnderwritingPending, Quoted, Accepted, Declined, Expired
    
    [JsonPropertyName("createdUtc")]
    public DateTimeOffset CreatedUtc { get; set; }
    
    [JsonPropertyName("expirationUtc")]
    public DateTimeOffset ExpirationUtc { get; set; }  // CreatedUtc + 30 days
    
    [JsonPropertyName("acceptedUtc")]
    public DateTimeOffset? AcceptedUtc { get; set; }
    
    // Coverage Selections
    [JsonPropertyName("structureCoverageLimit")]
    public decimal StructureCoverageLimit { get; set; }  // $50k - $500k
    
    [JsonPropertyName("structureDeductible")]
    public decimal StructureDeductible { get; set; }  // $500, $1000, $2500, $5000
    
    [JsonPropertyName("contentsCoverageLimit")]
    public decimal ContentsCoverageLimit { get; set; }  // $10k - $150k
    
    [JsonPropertyName("contentsDeductible")]
    public decimal ContentsDeductible { get; set; }  // $250, $500, $1000, $2500
    
    [JsonPropertyName("termMonths")]
    public int TermMonths { get; set; }  // 6 or 12
    
    [JsonPropertyName("effectiveDate")]
    public DateTimeOffset EffectiveDate { get; set; }  // Policy start date
    
    // Underwriting Information
    [JsonPropertyName("priorClaimsCount")]
    public int? PriorClaimsCount { get; set; }  // Claims in past 3 years
    
    [JsonPropertyName("kwegiboAge")]
    public int? KwegiboAge { get; set; }  // Age of insured Kwegibo
    
    [JsonPropertyName("creditTier")]
    public string? CreditTier { get; set; }  // Excellent, Good, Fair, Poor
    
    [JsonPropertyName("underwritingClass")]
    public string? UnderwritingClass { get; set; }  // A, B, or null if declined
    
    [JsonPropertyName("declineReason")]
    public string? DeclineReason { get; set; }
    
    // Rating Information
    [JsonPropertyName("premium")]
    public decimal? Premium { get; set; }  // Calculated premium
    
    [JsonPropertyName("baseRate")]
    public decimal? BaseRate { get; set; }  // $500
    
    [JsonPropertyName("coverageFactor")]
    public decimal? CoverageFactor { get; set; }
    
    [JsonPropertyName("termFactor")]
    public decimal? TermFactor { get; set; }
    
    [JsonPropertyName("ageFactor")]
    public decimal? AgeFactor { get; set; }
    
    [JsonPropertyName("territoryFactor")]
    public decimal? TerritoryFactor { get; set; }
    
    [JsonPropertyName("zipCode")]
    public string? ZipCode { get; set; }  // From customer, used for territory rating
    
    // Audit
    [JsonPropertyName("updatedUtc")]
    public DateTimeOffset UpdatedUtc { get; set; }
    
    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }
}
```

---

## API Endpoints

### POST /api/quotes/start

**Purpose**: Initiate new insurance quote

**Request**:
```json
{
  "quoteId": "guid",
  "customerId": "guid",
  "structureCoverageLimit": 200000,
  "structureDeductible": 1000,
  "contentsCoverageLimit": 50000,
  "contentsDeductible": 500,
  "termMonths": 12,
  "effectiveDate": "2026-03-01T00:00:00Z"
}
```

**Validation**:
- Structure coverage: $50,000 - $500,000
- Structure deductible: $500, $1,000, $2,500, $5,000
- Contents coverage: $10,000 - $150,000
- Contents deductible: $250, $500, $1,000, $2,500
- Term: 6 or 12 months
- Effective date: Future date, ≤ 60 days out

**Response**: `201 Created`
```json
{
  "quoteId": "guid",
  "status": "Draft",
  "expirationUtc": "2026-03-07T00:00:00Z"
}
```

**Events Published**: `QuoteStarted`

---

### POST /api/quotes/{quoteId}/submit-underwriting

**Purpose**: Submit underwriting information for risk evaluation

**Request**:
```json
{
  "priorClaimsCount": 0,
  "kwegiboAge": 10,
  "creditTier": "Excellent"
}
```

**Underwriting Logic**:
```csharp
// Class A (Preferred)
if (priorClaimsCount == 0 && kwegiboAge <= 15 && creditTier == "Excellent")
    return "A";

// Class B (Standard)
if (priorClaimsCount <= 1 && kwegiboAge <= 30 && (creditTier == "Good" || creditTier == "Excellent"))
    return "B";

// Declined
if (priorClaimsCount >= 3 || kwegiboAge > 30 || creditTier == "Poor")
    return Decline("Excessive risk factors");

return Decline("Does not meet underwriting criteria");
```

**Response**: `200 OK`
```json
{
  "quoteId": "guid",
  "status": "Quoted",
  "underwritingClass": "A",
  "premium": 1350.00
}
```

**Events Published**: 
- `UnderwritingSubmitted`
- `QuoteApproved` or `QuoteDeclined`
- `QuoteCalculated` (if approved)

**Error Responses**:
- `400 Bad Request`: Invalid underwriting data
- `409 Conflict`: Quote not in correct status
- `422 Unprocessable Entity`: Risk declined

---

### POST /api/quotes/{quoteId}/calculate

**Purpose**: Calculate premium (called automatically after underwriting approval)

**Rating Formula Implementation**:
```csharp
public decimal CalculatePremium(Quote quote, string zipCode)
{
    const decimal BASE_RATE = 500m;
    
    // Coverage Factor
    decimal structureFactor = quote.StructureCoverageLimit / 100000m;
    decimal contentsFactor = quote.ContentsCoverageLimit / 50000m;
    decimal coverageFactor = structureFactor + contentsFactor;
    
    // Term Factor
    decimal termFactor = quote.TermMonths == 6 ? 0.55m : 1.00m;
    
    // Age Factor
    decimal ageFactor = quote.KwegiboAge switch
    {
        <= 5 => 0.80m,
        <= 15 => 1.00m,
        <= 30 => 1.20m,
        _ => 1.50m
    };
    
    // Territory Factor
    decimal territoryFactor = GetTerritoryFactor(zipCode);
    
    // Calculate premium
    decimal premium = BASE_RATE * coverageFactor * termFactor * ageFactor * territoryFactor;
    
    return Math.Round(premium, 2);
}

private decimal GetTerritoryFactor(string zipCode)
{
    return zipCode switch
    {
        "90210" or "10001" => 0.90m,  // Zone 1
        "60601" or "33101" => 1.00m,  // Zone 2
        "70112" or "94102" => 1.20m,  // Zone 3
        _ => 1.10m  // Zone 4 (default)
    };
}
```

**Response**: `200 OK`
```json
{
  "quoteId": "guid",
  "premium": 1350.00,
  "ratingBreakdown": {
    "baseRate": 500.00,
    "coverageFactor": 3.0,
    "termFactor": 1.0,
    "ageFactor": 1.0,
    "territoryFactor": 0.9
  }
}
```

**Events Published**: `QuoteCalculated`

---

### POST /api/quotes/{quoteId}/accept

**Purpose**: Customer accepts quote and coverage terms

**Request**: (Empty body or confirmation)

**Validation**:
- Quote status must be "Quoted"
- Quote must not be expired
- Premium must be calculated

**Process**:
1. Update quote status to "Accepted"
2. Set acceptedUtc timestamp
3. Save quote document
4. Publish `QuoteAccepted` event to Service Bus
5. Policy domain receives event and creates policy

**Response**: `200 OK`
```json
{
  "quoteId": "guid",
  "status": "Accepted",
  "acceptedUtc": "2026-02-05T14:30:00Z",
  "premium": 1350.00,
  "message": "Quote accepted. Policy creation initiated."
}
```

**Events Published**: `QuoteAccepted` (critical - triggers policy creation)

**Error Responses**:
- `400 Bad Request`: Quote expired
- `409 Conflict`: Quote not in "Quoted" status

---

### GET /api/quotes/{quoteId}

**Purpose**: Retrieve quote details

**Response**: `200 OK`
```json
{
  "quoteId": "guid",
  "customerId": "guid",
  "status": "Quoted",
  "structureCoverageLimit": 200000,
  "structureDeductible": 1000,
  "contentsCoverageLimit": 50000,
  "contentsDeductible": 500,
  "termMonths": 12,
  "effectiveDate": "2026-03-01T00:00:00Z",
  "premium": 1350.00,
  "underwritingClass": "A",
  "expirationUtc": "2026-03-07T00:00:00Z",
  "createdUtc": "2026-02-05T10:00:00Z"
}
```

---

### GET /api/customers/{customerId}/quotes

**Purpose**: Retrieve all quotes for a customer

**Query Parameters**:
- `status`: Filter by quote status (optional)
- `includeExpired`: Include expired quotes (default: false)

**Response**: `200 OK`
```json
{
  "quotes": [
    {
      "quoteId": "guid",
      "status": "Quoted",
      "premium": 1350.00,
      "expirationUtc": "2026-03-07T00:00:00Z",
      "createdUtc": "2026-02-05T10:00:00Z"
    }
  ]
}
```

**Note**: Cross-partition query (by customerId), use sparingly

---

## Domain Services

### RatingEngine

**Interface**: `IRatingEngine`

```csharp
public interface IRatingEngine
{
    decimal CalculatePremium(Quote quote, string zipCode);
    RatingBreakdown GetRatingBreakdown(Quote quote, string zipCode);
    decimal GetTerritoryFactor(string zipCode);
    decimal GetAgeFactor(int kwegiboAge);
    decimal GetTermFactor(int termMonths);
    decimal GetCoverageFactor(decimal structureLimit, decimal contentsLimit);
}
```

**Responsibilities**:
- Implement rating formula
- Apply rating factors
- Return premium breakdown for transparency

---

### UnderwritingEngine

**Interface**: `IUnderwritingEngine`

```csharp
public interface IUnderwritingEngine
{
    UnderwritingResult Evaluate(UnderwritingSubmission submission);
    bool IsRiskAcceptable(int priorClaimsCount, int kwegiboAge, string creditTier);
    string DetermineUnderwritingClass(UnderwritingSubmission submission);
}

public record UnderwritingResult(
    bool IsApproved,
    string? UnderwritingClass,
    string? DeclineReason
);

public record UnderwritingSubmission(
    int PriorClaimsCount,
    int KwegiboAge,
    string CreditTier
);
```

**Underwriting Rules**:
- Class A: 0 claims, age ≤ 15, Excellent credit
- Class B: 0-1 claims, age ≤ 30, Good+ credit
- Decline: 3+ claims, age > 30, Poor credit

---

### QuoteManager

**Interface**: `IQuoteManager`

```csharp
public interface IQuoteManager
{
    Task<Quote> StartQuoteAsync(StartQuoteRequest request);
    Task<Quote> SubmitUnderwritingAsync(string quoteId, UnderwritingSubmission submission);
    Task<Quote> AcceptQuoteAsync(string quoteId);
    Task<Quote> GetQuoteAsync(string quoteId);
    Task ExpireOldQuotesAsync();  // Background job
}
```

---

## Repository Pattern

### IQuoteRepository

```csharp
public interface IQuoteRepository
{
    Task<Quote?> GetByIdAsync(string quoteId);
    Task<IEnumerable<Quote>> GetByCustomerIdAsync(string customerId);
    Task<Quote> CreateAsync(Quote quote);
    Task<Quote> UpdateAsync(Quote quote);
    Task<IEnumerable<Quote>> GetExpirableQuotesAsync(DateTimeOffset currentDate);
}
```

**Implementation Notes**:
- `GetByCustomerIdAsync`: Cross-partition query (use with pagination)
- `GetExpirableQuotesAsync`: Finds quotes where `expirationUtc < currentDate` and `status != 'Accepted'`

---

## Events Published

### QuoteStarted

**Contract Location**: `services/ratingandunderwriting/src/Domain/Contracts/Events/QuoteStarted.cs`

```csharp
namespace RiskInsure.RatingAndUnderwriting.Domain.Contracts.Events;

public record QuoteStarted(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string QuoteId,
    string CustomerId,
    decimal StructureCoverageLimit,
    decimal StructureDeductible,
    decimal ContentsCoverageLimit,
    decimal ContentsDeductible,
    int TermMonths,
    DateTimeOffset EffectiveDate,
    string IdempotencyKey
);
```

---

### QuoteAccepted

**Contract Location**: `platform/RiskInsure.PublicContracts/Events/QuoteAccepted.cs`

```csharp
namespace RiskInsure.PublicContracts.Events;

public record QuoteAccepted(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string QuoteId,
    string CustomerId,
    decimal StructureCoverageLimit,
    decimal StructureDeductible,
    decimal ContentsCoverageLimit,
    decimal ContentsDeductible,
    int TermMonths,
    DateTimeOffset EffectiveDate,
    decimal Premium,
    string IdempotencyKey
);
```

**Subscribers**: **Policy domain** (creates policy from accepted quote)

---

### QuoteCalculated

```csharp
public record QuoteCalculated(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string QuoteId,
    decimal Premium,
    decimal BaseRate,
    decimal CoverageFactor,
    decimal TermFactor,
    decimal AgeFactor,
    decimal TerritoryFactor,
    string IdempotencyKey
);
```

---

### QuoteDeclined

```csharp
public record QuoteDeclined(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    string QuoteId,
    string DeclineReason,
    string IdempotencyKey
);
```

---

## Background Jobs

### Quote Expiration Job

**Purpose**: Expire quotes after 30 days

**Schedule**: Runs daily at 2:00 AM UTC

**Process**:
1. Query quotes where `expirationUtc < currentDate` and `status = 'Quoted'`
2. Update status to "Expired"
3. Publish `QuoteExpired` event
4. Send notification email to customer

**Implementation**: Azure Function with Timer Trigger or Container App scheduled job

---

## Testing Strategy

### Unit Tests

- Rating formula calculations (verify each factor)
- Underwriting logic (Class A, B, Decline scenarios)
- Quote expiration logic
- Status transitions
- Validation rules

**Test Cases**:
- Minimum coverage limits
- Maximum coverage limits
- Invalid deductible selections
- Boundary conditions (age 15 vs 16, age 30 vs 31)
- Territory factor mappings

### Integration Tests (Playwright)

- Start quote → Submit underwriting → Calculate → Accept (end-to-end)
- Quote declination flow
- Multiple quotes per customer
- Quote expiration
- Cross-domain integration (Quote acceptance triggers policy creation)

---

## Performance Considerations

### Partition Strategy

- **Partition Key**: `/quoteId`
- **Hot Partition Risk**: Low (quotes evenly distributed by GUID)
- **Query Patterns**: Mostly point reads by quoteId

### Caching

- **Territory Factors**: Cache zip code → territory factor mapping
- **Rating Rules**: Cache rating formula parameters
- **No caching for quote data** (always read from Cosmos for accuracy)

---

## Monitoring & Observability

### Metrics

- Quote creation rate (per hour)
- Underwriting approval rate (% approved vs declined)
- Average premium amount
- Quote acceptance rate (quote-to-bind ratio)
- Quote expiration rate
- Rating calculation time (performance)

### Logging

**Required Fields**:
- QuoteId
- CustomerId
- Operation name
- Correlation ID

**Key Events to Log**:
- Quote started
- Underwriting submitted (with outcome)
- Premium calculated
- Quote accepted
- Quote expired
- Quote declined (with reason)

---

## Error Handling

### Validation Errors (400 Bad Request)

```json
{
  "error": "ValidationFailed",
  "errors": {
    "StructureCoverageLimit": ["Coverage limit must be between $50,000 and $500,000"],
    "StructureDeductible": ["Deductible must be $500, $1,000, $2,500, or $5,000"]
  }
}
```

### Business Logic Errors (422 Unprocessable Entity)

```json
{
  "error": "UnderwritingDeclined",
  "message": "Risk does not meet underwriting criteria",
  "declineReason": "Kwegibo age exceeds maximum allowable (30 years)"
}
```

---

## Future Enhancements

1. **Multi-Coverage Options**: Add Coverage C, D (liability, additional living expenses)
2. **Endorsements**: Mid-term coverage changes with pro-rata premium adjustments
3. **Renewal Quotes**: Auto-generate renewal quotes 30 days before policy expiration
4. **Quote Versioning**: Track quote modifications and versions
5. **Advanced Underwriting**: AI/ML risk scoring, external data integrations
6. **Dynamic Territory Rating**: Real-time risk maps, weather patterns
