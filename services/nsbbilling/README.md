# Billing Domain

**Status**: 📋 Planning

## Purpose
Manages billing operations for placed orders. This domain is **message-driven** and processes orders by charging credit cards when OrderPlaced events are received.

## Documentation
- [Overview](docs/overview.md) - Domain overview and bounded context definition
- [Business Requirements](docs/business/nsbbilling-management.md) - Business rules and requirements
- [Technical Specification](docs/technical/nsbbilling-technical-spec.md) - Technical design and message contracts

## Domain Properties
- **Entry Point**: Message-driven (no public API)
- **Primary Entity**: Billing / BillingRecord
- **Correlation ID**: OrderID

## Integration
- **Events Published**: 
  - `OrderBilled` (consumed by Shipping)
- **Events Subscribed**: 
  - `Sales.OrderPlaced` (triggers billing process)

## Quick Links
- Integration Tests: [test/Integration.Tests/](test/Integration.Tests/)
- Unit Tests: [test/Unit.Tests/](test/Unit.Tests/)

---
*Generated from DDD specification: `services/.rawservice/Billing_Systems_single_context_final.md`*
