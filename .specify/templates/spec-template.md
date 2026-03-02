# Feature Specification: [FEATURE NAME]

**Feature Branch**: `[###-feature-name]`  
**Created**: [DATE]  
**Status**: Draft  
**Input**: User description: "$ARGUMENTS"

> **Template Note**: This is the **full specification template**. For faster authoring when domain docs already exist, consider using `spec-template-quick.md` instead (10–20 min vs 30–60 min). See [templates/README.md](./) for guidance.

## RiskInsure Context *(mandatory)*

**Bounded Context(s)**: [Which service(s) - e.g., Billing, Policy, Customer, FileIntegration]  
**Domain Documentation**: [Link to services/<domain>/docs/domain-specific-standards.md if exists]  
**Constitution Reference**: [.specify/memory/constitution.md](../../.specify/memory/constitution.md)

**Applies Domain Rules**:
- [List specific rules/invariants from domain docs that apply, e.g., "FileRun state machine rules", "Invoice cancellation rules"]
- [If none, state "General platform patterns only"]

**Executable Host Profiles (mandatory)**:
- **Api host**: [Yes/No] → If Yes: use `Serilog.AspNetCore` and `mcr.microsoft.com/dotnet/aspnet:10.0`
- **Endpoint.In host**: [Yes/No] → If Yes: use `Serilog.Extensions.Hosting` + `Serilog.Settings.Configuration` and `mcr.microsoft.com/dotnet/runtime:10.0`

## User Scenarios & Testing *(mandatory)*

<!--
  IMPORTANT: User stories should be PRIORITIZED as user journeys ordered by importance.
  Each user story/journey must be INDEPENDENTLY TESTABLE - meaning if you implement just ONE of them,
  you should still have a viable MVP (Minimum Viable Product) that delivers value.
  
  Assign priorities (P1, P2, P3, etc.) to each story, where P1 is the most critical.
  Think of each story as a standalone slice of functionality that can be:
  - Developed independently
  - Tested independently
  - Deployed independently
  - Demonstrated to users independently
-->

### User Story 1 - [Brief Title] (Priority: P1)

[Describe this user journey in plain language]

**Why this priority**: [Explain the value and why it has this priority level]

**Independent Test**: [Describe how this can be tested independently - e.g., "Can be fully tested by [specific action] and delivers [specific value]"]

**Acceptance Scenarios**:

1. **Given** [initial state], **When** [action], **Then** [expected outcome]
2. **Given** [initial state], **When** [action], **Then** [expected outcome]

---

### User Story 2 - [Brief Title] (Priority: P2)

[Describe this user journey in plain language]

**Why this priority**: [Explain the value and why it has this priority level]

**Independent Test**: [Describe how this can be tested independently]

**Acceptance Scenarios**:

1. **Given** [initial state], **When** [action], **Then** [expected outcome]

---

### User Story 3 - [Brief Title] (Priority: P3)

[Describe this user journey in plain language]

**Why this priority**: [Explain the value and why it has this priority level]

**Independent Test**: [Describe how this can be tested independently]

**Acceptance Scenarios**:

1. **Given** [initial state], **When** [action], **Then** [expected outcome]

---

[Add more user stories as needed, each with an assigned priority]

### Edge Cases

<!--
  ACTION REQUIRED: The content in this section represents placeholders.
  Fill them out with the right edge cases.
-->

- What happens when [boundary condition]?
- How does system handle [error scenario]?

## Requirements *(mandatory)*

<!--
  ACTION REQUIRED: The content in this section represents placeholders.
  Fill them out with the right functional requirements.
-->

### Functional Requirements

- **FR-001**: System MUST [specific capability, e.g., "allow users to create accounts"]
- **FR-002**: System MUST [specific capability, e.g., "validate email addresses"]  
- **FR-003**: Users MUST be able to [key interaction, e.g., "reset their password"]
- **FR-004**: System MUST [data requirement, e.g., "persist user preferences"]
- **FR-005**: System MUST [behavior, e.g., "log all security events"]

*Example of marking unclear requirements:*

- **FR-006**: System MUST authenticate users via [NEEDS CLARIFICATION: auth method not specified - email/password, SSO, OAuth?]
- **FR-007**: System MUST retain user data for [NEEDS CLARIFICATION: retention period not specified]

### Key Entities *(include if feature involves data)*

- **[Entity 1]**: [What it represents, key attributes without implementation]
- **[Entity 2]**: [What it represents, relationships to other entities]

## Message Contracts *(if event-driven integration)*

**Commands** (imperative, directed):
- `[CommandName]` - [Purpose, required fields: MessageId, OccurredUtc, IdempotencyKey, + domain fields]

**Events** (past-tense, broadcast):
- `[EventName]` - [What occurred, required fields: MessageId, OccurredUtc, IdempotencyKey, + domain fields]

**Contract Location**: 
- Internal (this service only): `services/<domain>/src/Domain/Contracts/`
- Public (cross-service): `platform/RiskInsure.PublicContracts/Events/` or `platform/RiskInsure.PublicContracts/Commands/`

## Data Strategy *(if applicable)*

**Partition Key**: [e.g., `/fileRunId`, `/orderId`, `/customerId` - identifies processing unit for single-partition queries]  
**Document Types**: [e.g., FileRun, PaymentInstruction, ValidationError - types stored in same container]  
**State Transitions**: [If aggregate state changes, describe: e.g., "FileRun: Pending → Processing → Completed/Failed"]  
**Idempotency Strategy**: [How duplicate messages are handled - e.g., "Check existing document by ID before creating"]

## Success Criteria *(mandatory)*

<!--
  ACTION REQUIRED: Define measurable success criteria.
  These must be technology-agnostic and measurable.
-->

### Measurable Outcomes

- **SC-001**: [Measurable metric, e.g., "Users can complete account creation in under 2 minutes"]
- **SC-002**: [Measurable metric, e.g., "System handles 1000 concurrent users without degradation"]
- **SC-003**: [User satisfaction metric, e.g., "90% of users successfully complete primary task on first attempt"]
- **SC-004**: [Business metric, e.g., "Reduce support tickets related to [X] by 50%"]
