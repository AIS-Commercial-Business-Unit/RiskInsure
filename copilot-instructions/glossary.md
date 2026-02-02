# Glossary

## Overview

This glossary defines domain-specific terms used in this bounded context. Maintaining a shared vocabulary (ubiquitous language) ensures clear communication between developers, domain experts, and stakeholders.

---

## Domain Terms

### Event
**Definition:** A scheduled occurrence with a specific start and end date that can be ticketed or attended.

**Usage Context:** Core aggregate root in the EventManagement domain. An Event has a lifecycle from creation through various statuses to eventual completion or cancellation.

**Related Terms:** EventStatus, EventCreatedEvent, EventStatusChangedEvent

**Examples:**
- "Concert at Madison Square Garden on June 15, 2024"
- "Tech Conference from March 1-3, 2024"
- "Charity Fundraiser on December 31, 2023"

---

### EventStatus
**Definition:** The current state of an Event in its lifecycle.

**Possible Values:**
- `Active` - Event is currently available and bookable
- `Cancelled` - Event has been cancelled and is no longer available
- `Expired` - Event date has passed
- (Add additional statuses as implemented)

**Usage Context:** Used to control business rules and behavior. Status transitions trigger domain events.

**Related Terms:** Event, EventStatusChangedEvent

---

## Technical Terms

### Bounded Context
**Definition:** A logical boundary within which a particular domain model is defined and applicable. Each bounded context has its own ubiquitous language and is implemented as a separate service/project.

**Usage Context:** EventManagement is one bounded context in the larger AcmeTickets system. It owns all concepts related to event lifecycle management.

**Related Terms:** Domain-Driven Design, Microservices, Event-Driven Architecture

---

### Command
**Definition:** An imperative message that requests an action to be performed. Commands are named in the imperative form (e.g., `CreateEventCommand`).

**Usage Context:** Commands represent intent to change state. They are sent to a specific handler and may succeed or fail.

**Related Terms:** Message, Event, Handler, CQRS

---

### Event (Integration Event)
**Definition:** A message that notifies other parts of the system that something significant has happened. Events are named in the past tense (e.g., `EventCreatedEvent`).

**Usage Context:** Events represent facts that have already occurred. They are published to all interested subscribers and cannot fail.

**Related Terms:** Command, Message, Publisher, Subscriber, Event-Driven Architecture

**Note:** This is different from the domain term "Event" (a scheduled occurrence). Context determines which meaning applies.

---

### Domain Event
**Definition:** An event that represents something significant that happened within the domain model, raised by entities.

**Usage Context:** Domain events are raised by entities when important state changes occur. They are published as integration events after persistence succeeds.

**Related Terms:** Event, Entity, Aggregate, DomainEvent base class

---

### Handler
**Definition:** A class that processes a specific message (command or event) and contains the business logic for that operation.

**Usage Context:** Each message type has one or more handlers. Handlers implement `IHandleMessages<TMessage>` from NServiceBus.

**Related Terms:** Command, Event, Message, NServiceBus

---

### Saga
**Definition:** A long-running business process that coordinates multiple messages and maintains state across multiple operations.

**Usage Context:** Used for complex workflows that span multiple steps, potentially across multiple bounded contexts. Handles failures with compensation.

**Related Terms:** Process Manager, Workflow, Orchestration, Compensation

---

### Repository
**Definition:** An abstraction that provides collection-like access to domain entities, hiding persistence details.

**Usage Context:** Each aggregate root has a repository interface in the Domain layer and implementation in Infrastructure.

**Related Terms:** Entity, Aggregate, Persistence, Data Pattern

---

### Aggregate
**Definition:** A cluster of domain objects treated as a single unit for data changes. Each aggregate has a root entity and a consistency boundary.

**Usage Context:** Event is an aggregate root. All changes to the aggregate go through the root. Aggregates ensure invariants are maintained.

**Related Terms:** Entity, Domain-Driven Design, Consistency Boundary

---

## Architecture Terms

### CQRS (Command Query Responsibility Segregation)
**Definition:** Pattern that separates read operations (queries) from write operations (commands), potentially using different models for each.

**Usage Context:** Commands use the domain model. Queries may use simplified read models or DTOs directly from the database.

**Related Terms:** Command, Query, Read Model

---

### Eventually Consistent
**Definition:** A consistency model where updates to the system will propagate to all parts eventually, but not immediately.

**Usage Context:** Cross-domain operations are eventually consistent. Messages are delivered asynchronously, and subscribers process them independently.

**Related Terms:** Distributed Systems, Messaging, Asynchronous

---

### Idempotent
**Definition:** An operation that produces the same result no matter how many times it is executed with the same input.

**Usage Context:** Message handlers should be idempotent to handle duplicate message delivery safely.

**Related Terms:** Handler, Message, Retry

---

## Template Instructions

**To expand this glossary:**

1. **Identify domain terms** from conversations with domain experts
2. **Add ubiquitous language** terms used in this bounded context
3. **Define technical patterns** specific to this codebase
4. **Provide examples** to clarify abstract concepts
5. **Link related terms** to show relationships
6. **Keep definitions concise** but clear
7. **Update as the domain evolves**

**Format for new entries:**

```markdown
### Term Name
**Definition:** Clear, concise definition of the term.

**Usage Context:** How and where this term is used in the system.

**Related Terms:** Other glossary terms related to this one

**Examples:** (Optional) Concrete examples to illustrate the term
```

---

## Related Files
- See [domain-overview.md](domain-overview.md) for domain concepts
- See [naming-conventions.md](naming-conventions.md) for naming standards
- See [messaging-patterns.md](messaging-patterns.md) for message terminology
- See [data-patterns.md](data-patterns.md) for data terminology
