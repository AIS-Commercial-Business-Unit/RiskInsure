# Sales Domain

**Status**: 📋 Planning

## Purpose
Implements the Sales capability - handles order placement through user interface.

This domain serves as the **entry point** for the order processing system, accepting customer orders via API and publishing OrderPlaced events for downstream processing.

## Documentation
- [Overview](docs/overview.md) - Domain overview and bounded context definition
- [Business Requirements](docs/business/nsbsales-management.md) - Business rules and requirements
- [Technical Specification](docs/technical/nsbsales-technical-spec.md) - Technical design and API contracts

## Domain Properties
- **Entry Point**: HTTP API (user-facing)
- **Primary Entity**: Order
- **Correlation ID**: OrderID

## Integration
- **Events Published**: 
  - `OrderPlaced` (consumed by Billing and Shipping)
- **Events Subscribed**: (none - entry point)

## Quick Links
- API Documentation: [Swagger UI](http://localhost:TBD/scalar/v1) (when running)
- Integration Tests: [test/Integration.Tests/](test/Integration.Tests/)
- Unit Tests: [test/Unit.Tests/](test/Unit.Tests/)

---
*Generated from DDD specification: `services/.rawservice/Sales_Systems_single_context_final.md`*
