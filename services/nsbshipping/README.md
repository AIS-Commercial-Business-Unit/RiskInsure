# Shipping Domain

**Status**: 📋 Planning

## Purpose
Manages inventory reservation and order shipping operations. This domain is **message-driven** and coordinates two parallel workflows: inventory reservation (triggered by OrderPlaced) and order shipping (triggered by OrderBilled).

## Documentation
- [Overview](docs/overview.md) - Domain overview and bounded context definition
- [Business Requirements](docs/business/nsbshipping-management.md) - Business rules and requirements
- [Technical Specification](docs/technical/nsbshipping-technical-spec.md) - Technical design and message contracts

## Domain Properties
- **Entry Point**: Message-driven (no public API)
- **Primary Entities**: Inventory, Shipment
- **Correlation ID**: OrderID

## Integration
- **Events Published**: 
  - `InventoryReserved` (inventory allocation confirmed)
  - `OrderShipped` (order fulfillment complete)
- **Events Subscribed**: 
  - `Sales.OrderPlaced` (triggers inventory reservation)
  - `Billing.OrderBilled` (triggers order shipment)

## Quick Links
- Integration Tests: [test/Integration.Tests/](test/Integration.Tests/)
- Unit Tests: [test/Unit.Tests/](test/Unit.Tests/)

---
*Generated from DDD specification: `services/.rawservice/Shipping_Systems_single_context_final.md`*
