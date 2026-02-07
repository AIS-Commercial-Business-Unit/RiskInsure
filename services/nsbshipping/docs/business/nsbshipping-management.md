# Shipping Management - Business Requirements

## Overview
The Shipping domain is responsible for two critical workflows in the order processing system:
1. **Inventory Reservation**: When orders are placed, inventory must be reserved
2. **Order Fulfillment**: When orders are billed, they must be packaged and shipped

This domain subscribes to events from both Sales (OrderPlaced) and Billing (OrderBilled) domains to coordinate fulfillment activities.

This domain is responsible for:
- Reserving inventory for placed orders
- Packaging orders for shipment
- Shipping orders after payment confirmation
- Publishing shipment status events

## Domain Terminology (Ubiquitous Language)

Define the business terms used within this bounded context:

| Term | Definition |
|------|------------|
| Inventory Reservation | The process of allocating inventory to a specific order |
| ReserveInventory | The command to reserve inventory for an order |
| InventoryReserved | Past-tense event indicating inventory has been allocated |
| Order Packaging | The process of preparing an order for shipment |
| ShipOrder | The command to ship a billed order |
| OrderShipped | Past-tense event indicating an order has been shipped |

## Business Capabilities

### Reserve Inventory (ReserveInventory)
**Purpose**: Intuitive - Reserve inventory when order is placed

**Trigger**: Receive `OrderPlaced` event from Sales domain

**Process**:
1. Receive `OrderPlaced` event with OrderID
2. Log: "Received OrderPlaced, OrderId = {OrderID} - Order Packaging Start..."
3. Execute business logic: Allocate inventory for order
4. Create inventory reservation record
5. Publish `InventoryReserved` event

**Outputs**:
- **Event**: `InventoryReserved` (Published: Yes)
- **Status**: Success/Failure

**Business Rules**:
- Minimal validation (required fields)
- Reserve inventory as soon as order is placed
- Each order has one inventory reservation

---

### Ship Order (ShipOrder)
**Purpose**: Intuitive - Ship order after billing is complete

**Trigger**: Receive `OrderBilled` event from Billing domain

**Process**:
1. Receive `OrderBilled` event with OrderID
2. Log: "Received OrderBilled, OrderId = {OrderID} - Verify Package and Ship..."
3. Execute business logic: Verify package, generate shipping label, ship order
4. Create shipment record
5. Publish `OrderShipped` event

**Outputs**:
- **Event**: `OrderShipped` (Published: Yes)
- **Status**: Success/Failure

**Business Rules**:
- Minimal validation (required fields)
- Only ship orders that have been billed
- Each order has one shipment record

---

## Use Cases

### UC-1: Reserve Inventory for Order
**Actor**: System (automated via message handler)  
**Goal**: Successfully reserve inventory for a placed order  
**Preconditions**:
- OrderPlaced event received from Sales domain
- OrderID is valid

**Main Flow**:
1. System receives OrderPlaced event via OrderPlacedHandler
2. System logs: "Received OrderPlaced, OrderId = {OrderID} - Order Packaging Start..."
3. System calls InventoryManager (or ShippingManager).ReserveInventoryAsync
4. System validates order data
5. System reserves inventory
6. System creates inventory reservation record
7. System publishes InventoryReserved event
8. External systems receive inventory allocation notification

**Postconditions**:
- `InventoryReserved` event published to event bus
- Inventory reservation record created
- Inventory allocated to order

**Alternative Flows**:
- **Insufficient Inventory**: Log error, publish failure event
- **Duplicate Reservation**: Skip processing (idempotency)

---

### UC-2: Ship Billed Order
**Actor**: System (automated via message handler)  
**Goal**: Successfully ship an order after billing  
**Preconditions**:
- OrderBilled event received from Billing domain
- OrderID is valid
- Inventory already reserved (from UC-1)

**Main Flow**:
1. System receives OrderBilled event via OrderBilledHandler
2. System logs: "Received OrderBilled, OrderId = {OrderID} - Verify Package and Ship..."
3. System calls ShippingManager.ShipOrderAsync
4. System validates order data
5. System verifies package contents
6. System generates shipping label
7. System marks order as shipped
8. System creates shipment record
9. System publishes OrderShipped event
10. External systems receive shipment notification

**Postconditions**:
- `OrderShipped` event published to event bus
- Shipment record created with tracking info
- Order marked as fulfilled

**Alternative Flows**:
- **Package Verification Failed**: Log error, hold shipment
- **Duplicate Shipment**: Skip processing (idempotency)

---

## Validation Rules
**Minimal validation approach**:
- Required fields: OrderID (GUID)
- Format validation: OrderID must be valid GUID
- Business rules: Process each order exactly once for each workflow (idempotency)

## Integration Requirements

### Events Published (Outgoing)
- **`InventoryReserved`**: Published when inventory successfully reserved
  - **Consumers**: External inventory tracking systems
  - **Data**: (OrderID correlation)

- **`OrderShipped`**: Published when order successfully shipped
  - **Consumers**: External order tracking systems, customer notification systems
  - **Data**: (OrderID correlation)

### Events Subscribed (Incoming)
- **`OrderPlaced`**: From Sales domain
  - **Handler**: `OrderPlacedHandler`
  - **Action**: Triggers `ReserveInventory` command
  - **Response**: "Received OrderPlaced, OrderId = {OrderID} - Order Packaging Start..."

- **`OrderBilled`**: From Billing domain
  - **Handler**: `OrderBilledHandler`
  - **Action**: Triggers `ShipOrder` command
  - **Response**: "Received OrderBilled, OrderId = {OrderID} - Verify Package and Ship..."

---
*Document generated from DDD specification: Shipping_Systems_single_context_final.md*
