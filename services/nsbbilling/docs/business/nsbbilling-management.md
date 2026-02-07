# Billing Management - Business Requirements

## Overview
The Billing domain is responsible for processing payments for placed orders. When an order is placed in the Sales system, this domain receives the OrderPlaced event and processes the billing transaction (charging credit card). Upon successful billing, it publishes an OrderBilled event to trigger downstream fulfillment processes.

This domain is responsible for:
- Processing billing for placed orders
- Charging credit cards or processing payments
- Publishing OrderBilled confirmation events

## Domain Terminology (Ubiquitous Language)

Define the business terms used within this bounded context:

| Term | Definition |
|------|------------|
| Billing | The payment processing record for an order |
| BillOrder | The command to process billing/payment for an order |
| OrderBilled | Past-tense event indicating an order has been successfully billed |
| Charging | The action of processing payment via credit card or payment method |

## Business Capabilities

### Billing Order (BillingOrder)
**Purpose**: Intuitive - Process payment for an order

**Trigger**: Receive `OrderPlaced` event from Sales domain

**Process**:
1. Receive `OrderPlaced` event with OrderID
2. Log: "Received OrderPlaced, OrderId = {OrderID} - Charging credit card..."
3. Execute business logic: Charge credit card / process payment
4. Create billing record
5. Publish `OrderBilled` event with OrderID

**Outputs**:
- **Event**: `OrderBilled` (Published: Yes)
- **Status**: Success/Failure

**Business Rules**:
- Minimal validation (required fields)
- OrderID must be valid
- Billing record created for each order

---

## Use Cases

### UC-1: Bill Placed Order
**Actor**: System (automated via message handler)  
**Goal**: Successfully process billing for a placed order  
**Preconditions**:
- OrderPlaced event received from Sales domain
- OrderID is valid

**Main Flow**:
1. System receives OrderPlaced event via OrderPlacedHandler
2. System logs receipt: "Received OrderPlaced, OrderId = {OrderID} - Charging credit card..."
3. System calls BillingManager.BillOrderAsync with OrderID
4. System validates order data
5. System processes payment (charges credit card)
6. System creates billing record in database
7. System publishes OrderBilled event
8. Shipping domain receives notification to proceed with fulfillment

**Postconditions**:
- `OrderBilled` event published to event bus
- Billing record created with status "Charged"
- Shipping system notified to proceed

**Alternative Flows**:
- **Payment Failure**: Log error, retry or publish failure event
- **Duplicate OrderID**: Skip processing (idempotency)

---

## Validation Rules
**Minimal validation approach**:
- Required fields: OrderID (GUID)
- Format validation: OrderID must be valid GUID
- Business rules: Process each order exactly once (idempotency)

## Integration Requirements

### Events Published (Outgoing)
- **`OrderBilled`**: Published when order successfully billed
  - **Consumers**: Shipping (for order fulfillment)
  - **Data**: OrderID (GUID)

### Events Subscribed (Incoming)
- **`OrderPlaced`**: From Sales domain
  - **Handler**: `OrderPlacedHandler`
  - **Action**: Triggers `BillOrder` command
  - **Response**: "Received OrderPlaced, OrderId = {OrderID} - Charging credit card...Publishing OrderBilled"

---
*Document generated from DDD specification: Billing_Systems_single_context_final.md*
