# Domain Overview

## Purpose

The EventManagement domain is responsible for managing the complete lifecycle of events within the AcmeTickets system. This includes creating events, tracking their status, handling expiration and closure, and notifying other domains of event-related changes.

## Domain Responsibilities

- **Event Creation**: Initialize new events with name, start date, and end date
- **Event Lifecycle Management**: Track events through Active, Expired, and Closed states
- **State Transitions**: Handle business rules for moving events between states
- **Event Notifications**: Publish domain events when significant changes occur (creation, expiration, closure)
- **Event Queries**: Provide event data to consumers via API

## Core Concepts

### Event
The primary aggregate root representing a scheduled occurrence with:
- Unique identifier
- Name
- Start and end dates
- Current status (Active, Expired, Closed)

### EventStatus
Enumeration defining valid event states:
- **Active**: Event is currently available and operational
- **Expired**: Event has passed its end date but not formally closed
- **Closed**: Event has been manually closed and is no longer available

### Lifecycle Rules
- Events start in Active status upon creation
- Only Active events can be expired or closed
- Status transitions are enforced through domain methods

## Integration Points

### Messages Published
- `EventCreatedEvent`: Published when a new event is created
- `EventExpiredEvent`: Published when an event is marked as expired
- `EventClosedEvent`: Published when an event is closed

### Messages Consumed
- `TicketRequestedEvent`: Example of consuming events from other domains (e.g., Ticketing domain)

## Boundaries

### In Scope
- Event data and lifecycle management
- Publishing notifications about event changes
- Validating event state transitions

### Out of Scope
- Ticket sales and inventory (handled by Ticketing domain)
- Customer information (handled by Customer domain)
- Payment processing (handled by Payment domain)
- UI composition and navigation (handled by Platform domain)
