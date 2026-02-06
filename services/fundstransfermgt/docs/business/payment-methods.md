# Payment Methods – Business Context

### **Who This Is For**

This documentation is intended for **business owners**, **product managers**, and **domain experts** responsible for:

* Customer payment experiences
* Payment method validation and compliance
* Secure payment instrument management

---

## 📘 Payment Methods Subdomain – Business Capabilities Overview

### **Purpose**

The Payment Methods subdomain is responsible for managing the **collection, storage, and lifecycle** of customer payment instruments. It ensures secure, PCI-compliant storage and validation of payment methods that customers can use for fund transfers, premium payments, and other financial transactions.

---

### **Key Business Capabilities**

1. **Payment Instrument Storage**

   * Collects and stores customer payment instruments such as:
     * ACH accounts (checking/savings)
     * Credit/Debit cards
     * Apple Pay or digital wallet tokens
   * Maintains secure, tokenized references to payment data
   * Supports secure, PCI-compliant capture via **UI** and **public-facing APIs**

2. **Payment Method Validation**

   * Validates payment instrument details:
     * Luhn checksum for credit cards
     * Routing number format for ACH accounts
     * Expiration date validation
   * Supports micro-deposit verification for ACH accounts
   * Validates against supported card types and networks

3. **Payment Method Lifecycle Management**

   * Tracks payment method status (`VALIDATED`, `REVOKED`, `EXPIRED`, `INACTIVE`)
   * Supports customer self-service actions:
     * Adding new payment methods
     * Editing payment method details
     * Removing/invalidating payment methods
     * Setting default payment preferences
   * Manages expiration and automatic status updates

4. **Payment Method Retrieval**

   * Provides secure, masked views of stored payment methods
   * Filters payment methods by customer
   * Returns only active, valid payment methods for transaction use

---

### **Key Business Events**

| **Event Name**           | **Description**                                                    |
| ------------------------ | ------------------------------------------------------------------ |
| `PaymentMethodAdded`     | A new payment instrument was added and validated for future use    |
| `PaymentMethodUpdated`   | Payment method details were modified (e.g., new expiration date)   |
| `PaymentMethodRemoved`   | A stored payment instrument was deleted or invalidated             |
| `PaymentMethodExpired`   | A payment method reached its expiration date                       |
| `PaymentMethodValidated` | A payment method completed validation (e.g., micro-deposit)        |

---

### **Business Rules**

1. **Validation Requirements**
   * Credit cards MUST pass Luhn checksum validation
   * ACH routing numbers MUST be valid 9-digit ABA numbers
   * Expiration dates MUST be in the future
   * Card types MUST be in supported list (Visa, Mastercard, Discover, Amex)

2. **Status Transitions**
   * New payment methods start in `PENDING` or `VALIDATED` status
   * Expired payment methods automatically transition to `EXPIRED`
   * Removed payment methods transition to `REVOKED` (soft delete)
   * Failed validation attempts may result in `INACTIVE` status

3. **Security Requirements**
   * Card numbers MUST be tokenized/vaulted, never stored in plain text
   * ACH account numbers MUST be encrypted at rest
   * All payment method access requires customer authentication
   * PCI DSS compliance required for card data handling

4. **Customer Experience**
   * Customers can store multiple payment methods
   * Customers can designate one method as default
   * Payment method masking for display (e.g., `****1234`)
   * Real-time validation feedback during entry

---

### **Integration Points**

* **Fund Transfer Management**: Provides validated payment methods for transfer execution
* **Billing**: Supplies default payment methods for recurring premium payments
* **Customer Portal**: Enables self-service payment method management
* **Payment Gateway/Vault**: Tokenizes and stores sensitive payment data

---

### **Success Metrics**

* **Payment Method Addition Success Rate**: % of payment methods successfully validated on first attempt
* **Payment Method Active Rate**: % of stored payment methods in active status
* **Validation Error Rate**: % of payment method additions that fail validation
* **Customer Self-Service Rate**: % of payment method changes done via self-service vs. support
