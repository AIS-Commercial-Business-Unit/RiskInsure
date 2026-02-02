# RiskInsure.PublicContracts

Public message contracts shared between bounded contexts.

## Purpose

This project contains **public message contracts** that are shared across service boundaries:
- Events published from one service and consumed by others
- Shared commands that cross domain boundaries
- Common DTOs used in cross-service communication

## Internal vs. Public Contracts

- **Public Contracts** (this project): Used for inter-service communication
  - Example: `InvoiceCreated` event from Billing consumed by Payments
  - Placed in this project and referenced by multiple services

- **Internal Contracts** (`ServiceName.Domain/Contracts`): Used within a single service
  - Example: `ProcessPaymentInstruction` command used only by FileIntegration
  - Placed in the service's Domain layer

## Structure

\\\
Contracts/
├── Commands/        # Imperative actions (e.g., ProcessPayment, CreateInvoice)
├── Events/          # Past-tense facts (e.g., PaymentProcessed, InvoiceCreated)
└── POCOs/          # Plain data objects shared across services
\\\

## Naming Conventions

- **Commands**: `Verb` + `Noun` (e.g., `ProcessPayment`, `CreateInvoice`)
- **Events**: `Noun` + `VerbPastTense` (e.g., `PaymentProcessed`, `InvoiceCreated`)
- Use C# records for immutability
- All contracts target `net10.0`

## Standard Fields

All messages MUST include:
- `MessageId` (Guid): Unique identifier
- `OccurredUtc` (DateTimeOffset): When the event occurred
- `IdempotencyKey` (string): Deduplication key
- Correlation fields for distributed tracing

## Example Contract

\\\csharp
namespace RiskInsure.PublicContracts.Contracts.Events;

public record InvoiceCreated(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    Guid InvoiceId,
    string CustomerId,
    decimal Amount,
    string IdempotencyKey
);
\\\

## Versioning

This project will become a NuGet package for sharing across repositories.
Use semantic versioning for breaking changes.
