# Sales Management - Business Requirements

## Overview
The Sales domain is responsible for accepting customer orders and initiating the order processing workflow. It serves as the entry point for the entire order management system, validating order requests and publishing events that trigger billing and fulfillment processes.

This domain is responsible for:
- Accepting customer order placement requests
- Validating order data
- Publishing OrderPlaced events to downstream systems

## Domain Terminology (Ubiquitous Language)

Define the business terms used within this bounded context:

| Term | Definition |
|------|------------|
| Order | A customer request to purchase items, identified by a unique OrderID |
| Order Placement | The action of a customer submitting an order for processing |
| OrderPlaced | Past-tense event indicating an order has been successfully accepted |
| PlaceOrder | The command to create a new order in the system |

## Business Capabilities

### Placing Order (PlacingOrder)
**Purpose**: Intuitive - Accept customer order and notify downstream systems

**Trigger**: Customer initiates order placement via UI (e.g., "Press 'P' to place an order")

**Process**:
1. Receive `PlaceOrder` command with OrderID
2. Execute business logic: Validate order data and create order record
3. Publish `OrderPlaced` event with OrderID

**Outputs**:
- **Event**: `OrderPlaced` (Published: Yes)
- **Status**: Success/Failure

**Business Rules**:
- Minimal validation (required fields)
- OrderID must be unique
- Order data must be present

---

## Use Cases

### UC-1: Place New Order
**Actor**: Customer  
**Goal**: Successfully place an order for processing  
**Preconditions**:
- Customer has items to order
- OrderID is generated (GUID)

**Main Flow**:
1. Customer triggers order placement (via UI or API)
2. System receives PlaceOrder command with OrderID
3. System validates order data
4. System creates order record
5. System publishes OrderPlaced event
6. Billing and Shipping domains receive notification

**Postconditions**:
- `OrderPlaced` event published to event bus
- Order state is "Placed"
- Downstream systems begin processing

**Alternative Flows**:
- **Validation Error**: Return 400 Bad Request with validation messages
- **Duplicate OrderID**: Return 409 Conflict

---

## Validation Rules
**Minimal validation approach**:
- Required fields: OrderID (GUID)
- Format validation: OrderID must be valid GUID
- Business rules: OrderID must be unique

## Integration Requirements

### Events Published (Outgoing)
- **`OrderPlaced`**: Published when customer successfully places order
  - **Consumers**: Billing (for payment processing), Shipping (for inventory reservation)
  - **Data**: OrderID (GUID)

### Events Subscribed (Incoming)
None - Sales domain is the entry point and does not subscribe to external events.

---
*Document generated from DDD specification: Sales_Systems_single_context_final.md*
