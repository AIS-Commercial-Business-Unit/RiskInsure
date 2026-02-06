# Payment Methods – Technical Design Overview

## Purpose

This document describes the technical components, APIs, data models, and integration mechanisms for the **Payment Methods** subdomain within the `FundTransferMgt` bounded context. The purpose is to enable secure storage, validation, and lifecycle management of customer payment instruments.

---

## Services Overview

* **PaymentMethodService**
  * Manages storage and lifecycle of ACH accounts, credit/debit cards, and wallet tokens
  * Validates payment instrument details
  * Coordinates with payment vaults/tokenization services

* **PaymentMethodValidator**
  * Performs validation logic (Luhn checksum, routing number format, etc.)
  * Coordinates micro-deposit verification for ACH accounts

* **PaymentMethodEventPublisher**
  * Emits domain events related to payment method lifecycle

---

## Core APIs

### Payment Method Management

#### `POST /payment-methods/credit-card`

Adds a new credit card payment method for a customer.

**Inputs**:
* Payment Method ID (GUID - client-generated for idempotency)
* Customer ID
* Cardholder name
* Card number (tokenized or passed to vault)
* Expiration date (MM/YY)
* CVV (not stored, used for initial validation only)
* Billing address

**Validation**:
* PCI compliance checks
* Luhn checksum validation
* Supported card type check (Visa, Mastercard, Discover, Amex)
* Expiration date must be in future
* Required fields present and properly formatted

**Processing**:
1. Validate input format
2. Perform Luhn checksum
3. Tokenize card number via payment vault
4. Store tokenized reference with metadata
5. Set status to `VALIDATED`

**Response**:
* `201 Created`
* Payment Method ID
* Status (`VALIDATED`)
* Masked card number (last 4 digits)
* Card type
* Expiration date

**Events Published**:
* `PaymentMethodAdded`

**Error Responses**:
* `400 Bad Request`: Invalid card number, unsupported card type, expired card
* `409 Conflict`: Duplicate payment method already exists
* `422 Unprocessable Entity`: Validation failures

---

#### `POST /payment-methods/ach`

Adds a new ACH account for payment.

**Inputs**:
* Payment Method ID (GUID - client-generated for idempotency)
* Customer ID
* Account holder name
* Routing number (9-digit ABA number)
* Account number (encrypted before storage)
* Account type (`CHECKING` or `SAVINGS`)

**Validation**:
* Routing number format check (9 digits, valid ABA)
* Account type must be CHECKING or SAVINGS
* Required fields present

**Processing**:
1. Validate routing number format
2. Encrypt account number
3. Store encrypted reference with metadata
4. Set status to `PENDING` (awaiting micro-deposit verification)
5. Optionally initiate micro-deposit verification

**Response**:
* `201 Created`
* Payment Method ID
* Status (`PENDING` or `VALIDATED`)
* Masked account number (last 4 digits)
* Account type

**Events Published**:
* `PaymentMethodAdded`

**Error Responses**:
* `400 Bad Request`: Invalid routing number, invalid account type
* `409 Conflict`: Duplicate payment method already exists

---

#### `GET /payment-methods?customerId={customerId}`

Retrieves all active payment methods for a customer.

**Query Parameters**:
* `customerId` (required): Customer identifier
* `status` (optional): Filter by status (`VALIDATED`, `PENDING`, `EXPIRED`, etc.)

**Response**:
* `200 OK`
* Array of payment methods (masked):
  ```json
  [
    {
      "id": "guid",
      "customerId": "guid",
      "type": "CREDIT_CARD",
      "status": "VALIDATED",
      "maskedNumber": "****1234",
      "cardType": "VISA",
      "expirationDate": "12/26",
      "isDefault": true,
      "createdAt": "2026-01-15T10:00:00Z"
    },
    {
      "id": "guid",
      "customerId": "guid",
      "type": "ACH",
      "status": "VALIDATED",
      "maskedAccountNumber": "****5678",
      "accountType": "CHECKING",
      "isDefault": false,
      "createdAt": "2026-02-01T14:30:00Z"
    }
  ]
  ```

**Error Responses**:
* `404 Not Found`: Customer has no payment methods

---

#### `GET /payment-methods/{id}`

Retrieves a specific payment method by ID.

**Response**:
* `200 OK`
* Payment method details (masked)

**Error Responses**:
* `404 Not Found`: Payment method does not exist

---

#### `PUT /payment-methods/{id}`

Updates a payment method (e.g., new expiration date for credit card).

**Inputs**:
* Fields to update (e.g., expiration date, billing address, default flag)

**Response**:
* `200 OK`
* Updated payment method details

**Events Published**:
* `PaymentMethodUpdated`

**Error Responses**:
* `404 Not Found`: Payment method does not exist
* `400 Bad Request`: Invalid update fields

---

#### `DELETE /payment-methods/{id}`

Removes or invalidates a stored payment method (soft delete).

**Processing**:
1. Validate payment method exists
2. Check if payment method is used in pending transfers
3. Set status to `REVOKED`
4. Update timestamp

**Response**:
* `204 No Content`

**Events Published**:
* `PaymentMethodRemoved`

**Error Responses**:
* `404 Not Found`: Payment method does not exist
* `409 Conflict`: Payment method in use by pending transfer

---

## Data Model

### PaymentMethod

**Document Structure** (Cosmos DB):
```csharp
public class PaymentMethod
{
    [JsonPropertyName("id")]
    public string Id { get; set; }  // GUID
    
    [JsonPropertyName("customerId")]
    public string CustomerId { get; set; }  // Partition key
    
    [JsonPropertyName("type")]
    public string Type { get; set; }  // CREDIT_CARD, ACH, WALLET
    
    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = "PaymentMethod";
    
    [JsonPropertyName("status")]
    public string Status { get; set; }  // VALIDATED, PENDING, REVOKED, EXPIRED, INACTIVE
    
    // Card-specific fields
    [JsonPropertyName("cardType")]
    public string? CardType { get; set; }  // VISA, MASTERCARD, DISCOVER, AMEX
    
    [JsonPropertyName("maskedCardNumber")]
    public string? MaskedCardNumber { get; set; }  // Last 4 digits
    
    [JsonPropertyName("cardholderName")]
    public string? CardholderName { get; set; }
    
    [JsonPropertyName("expirationMonth")]
    public int? ExpirationMonth { get; set; }
    
    [JsonPropertyName("expirationYear")]
    public int? ExpirationYear { get; set; }
    
    [JsonPropertyName("tokenReference")]
    public string? TokenReference { get; set; }  // Vault token
    
    // ACH-specific fields
    [JsonPropertyName("accountHolderName")]
    public string? AccountHolderName { get; set; }
    
    [JsonPropertyName("maskedAccountNumber")]
    public string? MaskedAccountNumber { get; set; }  // Last 4 digits
    
    [JsonPropertyName("routingNumber")]
    public string? RoutingNumber { get; set; }
    
    [JsonPropertyName("accountType")]
    public string? AccountType { get; set; }  // CHECKING, SAVINGS
    
    [JsonPropertyName("encryptedAccountReference")]
    public string? EncryptedAccountReference { get; set; }
    
    // Common fields
    [JsonPropertyName("billingAddress")]
    public Address? BillingAddress { get; set; }
    
    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; set; }
    
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }
    
    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }
    
    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }  // For optimistic concurrency
}
```

**Partition Strategy**:
* Container: `fundstransfermgt`
* Partition Key: `/customerId`
* Co-located with fund transfer documents for efficient querying

---

## Events Published

| Event Name | Trigger | Payload Summary | Contract Location |
|------------|---------|-----------------|-------------------|
| `PaymentMethodAdded` | Customer adds payment method and validation succeeds | CustomerId, PaymentMethodId, MethodType, AddedUtc | `services/fundstransfermgt/src/Domain/Contracts/Events/PaymentMethodAdded.cs` |
| `PaymentMethodUpdated` | Payment method details modified | CustomerId, PaymentMethodId, UpdatedFields, UpdatedUtc | `services/fundstransfermgt/src/Domain/Contracts/Events/PaymentMethodUpdated.cs` |
| `PaymentMethodRemoved` | Customer removes payment method or system invalidates it | CustomerId, PaymentMethodId, RemovalReason, RemovedUtc | `services/fundstransfermgt/src/Domain/Contracts/Events/PaymentMethodRemoved.cs` |
| `PaymentMethodExpired` | Credit card expiration date passed | CustomerId, PaymentMethodId, ExpiredUtc | `services/fundstransfermgt/src/Domain/Contracts/Events/PaymentMethodExpired.cs` |
| `PaymentMethodValidated` | ACH micro-deposit verification completed | CustomerId, PaymentMethodId, ValidatedUtc | `services/fundstransfermgt/src/Domain/Contracts/Events/PaymentMethodValidated.cs` |

---

## Security and Compliance

### PCI DSS Compliance
* **Tokenization**: Card numbers MUST be tokenized via PCI-compliant vault
* **No Plain Text Storage**: Raw card numbers NEVER stored in application database
* **CVV Handling**: CVV used for validation only, never stored
* **Access Controls**: Payment method access requires customer authentication
* **Audit Logging**: All payment method operations logged with correlation IDs

### Encryption
* ACH account numbers encrypted at rest using AES-256
* Encryption keys managed via Azure Key Vault
* Decryption only for authorized payment gateway operations

### Validation
* Client-side validation for immediate feedback
* Server-side validation for security (never trust client)
* Luhn checksum for credit cards
* ABA routing number validation for ACH

---

## Integration Points

### Payment Vault/Tokenization Service
* **Purpose**: Tokenize credit card numbers
* **Integration**: HTTPS API calls to tokenization provider
* **Flow**: Card number → Tokenization API → Token reference stored

### Micro-Deposit Verification Service
* **Purpose**: Verify ACH account ownership
* **Integration**: Initiate micro-deposits, verify amounts entered by customer
* **Flow**: ACH account → Micro-deposits sent → Customer verifies → Status updated to VALIDATED

### Payment Gateway
* **Purpose**: Authorize and process transactions
* **Integration**: Provide token references for fund transfers
* **Flow**: Fund transfer request → Gateway uses token → Authorization result

---

## Repository Pattern

### PaymentMethodRepository

**Interface** (`Domain/Repositories/IPaymentMethodRepository.cs`):
```csharp
public interface IPaymentMethodRepository
{
    Task<PaymentMethod?> GetByIdAsync(string id);
    Task<IEnumerable<PaymentMethod>> GetByCustomerIdAsync(string customerId);
    Task<PaymentMethod> CreateAsync(PaymentMethod paymentMethod);
    Task<PaymentMethod> UpdateAsync(PaymentMethod paymentMethod);
    Task DeleteAsync(string id);  // Soft delete (set status to REVOKED)
}
```

**Implementation** (`Domain/Repositories/PaymentMethodRepository.cs`):
* Uses Cosmos DB SDK directly
* Queries by partition key (`/customerId`) for efficiency
* Implements optimistic concurrency using ETags
* Maps between domain models and Cosmos DB documents

---

## Error Handling

### Validation Errors
* `400 Bad Request`: Invalid input format, failed validation rules
* `422 Unprocessable Entity`: Business rule violations

### Conflict Errors
* `409 Conflict`: Duplicate payment method, payment method in use

### Not Found Errors
* `404 Not Found`: Payment method does not exist

### Authorization Errors
* `401 Unauthorized`: Missing or invalid authentication
* `403 Forbidden`: Customer attempting to access another customer's payment method

---

## Testing Strategy

### Unit Tests
* Validation logic (Luhn checksum, routing number format)
* Payment method creation and updates
* Status transitions

### Integration Tests
* API endpoint testing via Playwright
* Repository operations with Cosmos DB Emulator
* Event publishing verification

### Security Tests
* Authentication and authorization checks
* Cross-customer access prevention
* Token/encryption handling

---

## Future Enhancements

* Support for additional payment methods (Apple Pay, Google Pay)
* Automated expiration monitoring and customer notifications
* Payment method usage analytics
* Fraud detection integration
