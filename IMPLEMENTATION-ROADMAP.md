# RiskInsure Event-Driven Architecture Implementation Roadmap

**Document Version**: 1.0  
**Created**: February 19, 2026  
**Status**: Active Implementation  
**Audience**: Development Team, DevOps, Architecture Review

---

## Executive Summary

RiskInsure is an **event-driven .NET 10 microservice platform** using NServiceBus 9.2.6, Azure Service Bus, and Cosmos DB. The infrastructure is built but the **event-driven messaging backbone is incomplete**. This roadmap identifies the critical gaps and provides a phased implementation plan to achieve a fully functional event-driven architecture.

**Current Status**:
- ✅ Infrastructure deployed (5 Container Apps, Cosmos DB, Service Bus)
- ✅ Billing service working end-to-end (API + Endpoint.In handlers)
- ⏳ Policy, Customer, Funds Transfer, Rating services awaiting handler implementation
- 🔴 **CRITICAL**: Service Bus lacks domain-specific topics and subscriptions
- 🔴 **CRITICAL**: Cross-domain event routing not configured
- 🔴 **CRITICAL**: Most services have no message handlers implemented

---

## Current Architecture State

### What's Working ✅

**Infrastructure Layer**:
- Azure Container Apps deployments: Billing ✅, Policy ⏳, Customer ⏳, Funds Transfer ⏳, Rating ⏳, Premium (skipped)
- Shared User-Assigned Managed Identity (UAMI) with all required roles
- Cosmos DB with per-domain containers (billing, policy, customer, fundstransfermgt, ratingunderwriting)
- Cosmos DB saga containers for NServiceBus state management
- Key Vault integration for connection strings
- Billing API with 4 endpoints (account creation, premium update, activate, suspend)

**Code Foundation**:
- NServiceBus configuration infrastructure (`NServiceBusConfigurationExtensions.cs`) in all services
- Billing Endpoint.In with 3 handlers: `FundsRefundedHandler`, `FundsSettledHandler`, `RecordPaymentHandler`
- Policy Endpoint.In with 1 handler: `QuoteAcceptedHandler`
- Domain layer managers and repositories (Billing API proven working with Cosmos DB)

**Public Contracts**:
- `platform/RiskInsure.PublicContracts` project with 5 events defined:
  - `FundsRefunded.cs`
  - `FundsSettled.cs`
  - `PaymentReceived.cs`
  - `PolicyIssued.cs`
  - `QuoteAccepted.cs`

### What's NOT Working 🔴

**Service Bus Configuration**:
- ❌ Only 1 generic topic: `bundle-1` (no domain-specific topics)
- ❌ No subscriptions defined in Terraform
- ❌ No explicit routing between endpoints
- ❌ `AutoSubscribe` disabled (won't auto-register subscriptions)
- **Impact**: Messages published to `bundle-1` but no endpoint listening → messages expire after 1 hour

**Handler Implementations**:
- ❌ Customer service: 0 handlers (needs to subscribe to Policy/Billing events)
- ❌ Funds Transfer service: 0 handlers (needs to subscribe to Billing payment events)
- ❌ Rating & Underwriting service: 1 handler but incomplete
- ❌ No command handlers for inbound API operations (only event handlers)

**Message Routing**:
- ❌ API endpoints don't publish commands to Endpoint.In (except Billing)
- ❌ Endpoint handlers don't have routing configured
- ❌ No cross-domain command routing (e.g., Billing → Policy)
- ❌ No saga orchestration for multi-step workflows

**Domain Integration**:
- ❌ API endpoints don't delegate to domain managers (except Billing)
- ❌ No event publishing from domain operations (except Billing)
- ❌ Missing domain contracts for commands/events (except Billing)

---

## Service Status Matrix

| Service | API ✅ | Endpoint.In ✅ | Handlers | Domain Manager | Repository | Status |
|---------|--------|-----------------|----------|-----------------|------------|--------|
| **Billing** | ✅ Working | ✅ Running | 3 handlers | ✅ Implemented | ✅ Cosmos | 🎯 Reference |
| **Policy** | ⏳ Basic | ⏳ Deployed | 1 handler (incomplete) | ⏳ Partial | ⏳ Partial | 🔄 In Progress |
| **Customer** | ⏳ Basic | ⏳ Deployed | ❌ None | ⏳ Partial | ⏳ Partial | ⏳ Not Started |
| **Funds Transfer** | ⏳ Basic | ⏳ Deployed | ❌ None | ⏳ Partial | ⏳ Partial | ⏳ Not Started |
| **Rating & Underwriting** | ⏳ Basic | ⏳ Deployed | 1 handler (incomplete) | ⏳ Partial | ⏳ Partial | 🔄 In Progress |
| **Premium** | ⏳ Basic | ⏳ Deployed | ❌ None | ❌ Not Started | ❌ Not Started | ⏭️ Skipped |

---

## Critical Gaps & Blockers

### 🔴 BLOCKER #1: Service Bus Topology (Critical)

**Issue**: No domain-specific topics or subscriptions  
**Location**: `platform/infra/shared-services/servicebus.tf`  
**Impact**: **BLOCKS ALL CROSS-DOMAIN MESSAGING**

**Current State**:
```terraform
resource "azurerm_servicebus_topic" "bundle" {
  name = "bundle-1"  # ← Single generic topic for everything
}
# NO subscriptions defined anywhere
```

**Problem**:
- All events from all domains published to `bundle-1`
- No subscriptions = no endpoint listening
- Message expires after 1 hour without being processed
- E2E tests fail with timeouts

**Required Fix**:
Create domain-specific topics for each service:
```terraform
topics:
  - billing-events (for BillingAccountCreated, InvoiceGenerated, etc.)
  - policy-events (for PolicyIssued, PolicyCanceled, etc.)
  - customer-events (for CustomerCreated, CustomerVerified, etc.)
  - fund-events (for FundsSettled, FundsRefunded, etc.)
  - quote-events (for QuoteCreated, QuoteApproved, etc.)

subscriptions:
  - billing-events → policy-subscriber (Policy subscribes to billing events)
  - billing-events → funds-subscriber (Funds Transfer subscribes)
  - policy-events → billing-subscriber (Billing subscribes to policy events)
  - quote-events → policy-subscriber (Policy subscribes to quote events)
  - ... (define all cross-domain subscriptions)
```

**Effort**: 2-3 hours (infrastructure review + testing)

---

### 🔴 BLOCKER #2: Message Handler Implementation (Critical)

**Issue**: Most services missing message handlers  
**Affected Services**: Customer (0 handlers), Funds Transfer (0 handlers), Rating (1 incomplete)  
**Impact**: **BLOCKS ANY INTER-SERVICE COMMUNICATION**

**Current Handler Status**:
```
Billing Endpoint.In/Handlers:
  ✅ FundsRefundedHandler.cs (complete)
  ✅ FundsSettledHandler.cs (complete)
  ✅ RecordPaymentHandler.cs (complete)

Policy Endpoint.In/Handlers:
  ⏳ QuoteAcceptedHandler.cs (incomplete - stub only)

Customer Endpoint.In/Handlers:
  ❌ (folder exists but no handlers)

Funds Transfer Endpoint.In/Handlers:
  ❌ (folder exists but no handlers)

Rating & Underwriting Endpoint.In/Handlers:
  ⏳ (incomplete)
```

**Required Handlers per Service**:

#### Policy Service
- `PolicyCreatedHandler` (internal, from API) - Creates policy entity
- `BillingAccountCreatedHandler` (subscribes to billing-events) - Start policy term
- `PaymentReceivedHandler` (subscribes to fund-events) - Track payment received
- `FundsRefundedHandler` (subscribes to fund-events) - Handle policy cancellation requests

#### Customer Service
- `CustomerCreatedHandler` (internal, from API) - Create profile
- `PolicyIssuedHandler` (subscribes to policy-events) - Track customer policies
- `CustomerVerificationCommandHandler` (internal, from API) - Verify customer

#### Funds Transfer Management Service
- `FundTransferInitiatedHandler` (internal, from API) - Initiate transfer
- `PolicyIssuedHandler` (subscribes to policy-events) - Process premium payments
- `InvoiceGeneratedHandler` (subscribes to billing-events) - Trigger fund transfer

#### Rating & Underwriting Service
- `QuoteInitiatedHandler` (internal, from API) - Create quote
- `QuoteAcceptedHandler` (subscribes to quote-events) - Accept quote
- `PolicyApprovedHandler` (subscribes to policy-events) - Update rating

**Effort**: 8-10 hours (implementation + unit tests)

---

### 🔴 BLOCKER #3: API Command Routing (Critical)

**Issue**: API endpoints don't send commands to Endpoint.In for processing  
**Affected Services**: All except Billing  
**Impact**: **ASYNC OPERATIONS NOT WORKING**

**Current State** (Billing - Working Example):
```csharp
// Api/Program.cs
endpoint.SendOnly();  // API is send-only
routing.RouteToEndpoint(typeof(RecordPayment), "RiskInsure.Billing.Endpoint");

// Api/Controllers/BillingPaymentsController.cs
public async Task<IActionResult> RecordAsync(RecordPaymentRequest request)
{
    var command = new RecordPayment { /* ... */ };
    await _messageSession.Send(command);  // Send to Endpoint.In for processing
    return Accepted();  // 202 Accepted
}
```

**Missing State** (Policy, Customer, etc.):
```csharp
// These services DON'T have routing configured
// API controllers call managers directly (synchronous only)
// Can't process async operations
```

**Required Implementation**:
1. Enable `SendOnly()` mode in each API's NServiceBus config
2. Configure routing: `routing.RouteToEndpoint(typeof(CreatePolicy), "RiskInsure.Policy.Endpoint")`
3. Create command contracts in Domain/Contracts/Commands/
4. Update API controllers to publish commands instead of calling managers directly

**Effort**: 4-5 hours (routing + command contracts)

---

### 🟡 ISSUE #4: Domain Contracts Incomplete

**Issue**: Missing command and event contracts in many services  
**Current Contracts** (`platform/RiskInsure.PublicContracts`):
```
✅ FundsRefunded.cs
✅ FundsSettled.cs
✅ PaymentReceived.cs
✅ PolicyIssued.cs
✅ QuoteAccepted.cs
```

**Missing Integration Events**:
- `BillingAccountCreated` (published by Billing, consumed by Policy)
- `InvoiceGenerated` (published by Billing, consumed by Funds Transfer)
- `CustomerCreated` (published by Customer, consumed by Policy)
- `CustomerVerified` (published by Customer)
- `PolicyCanceled` (published by Policy, consumed by Billing)
- `QuoteCreated` (published by Rating, consumed by Policy)
- `FundTransferInitiated` (published by Funds Transfer, consumed by Billing)
- ... (define all cross-domain events)

**Missing Command Contracts** (Internal domain-specific):
- Policy: `CreatePolicy`, `UpdatePolicy`, `CancelPolicy`, `ProcessPayment`
- Customer: `CreateCustomer`, `VerifyCustomer`, `UpdateProfile`
- Funds Transfer: `InitiateFundTransfer`, `ProcessRefund`
- Rating: `CreateQuote`, `RatePolicy`

**Effort**: 2-3 hours (contract definition + review)

---

### 🟡 ISSUE #5: Manager Pattern Incomplete

**Issue**: Domain managers don't exist or are incomplete in most services  
**Status**:
```
Billing:
  ✅ IBillingAccountManager (working)
  ✅ IBillingPaymentManager (working)

Policy:
  ⏳ IPolicyManager (stub exists, needs implementation)

Customer:
  ⏳ ICustomerManager (stub exists, needs implementation)

Funds Transfer:
  ⏳ IFundTransferManager (stub exists, needs implementation)

Rating:
  ⏳ IRatingManager (stub exists, needs implementation)
```

**Manager Responsibilities** (per Billing example):
- Coordinate business logic (validate → persist → publish events)
- Use repositories for data access
- Raise domain events that trigger integration events
- Handle idempotency checks

**Effort**: 6-8 hours (implementation + unit tests)

---

### 🟡 ISSUE #6: Repositories Incomplete

**Issue**: Cosmos DB repositories not fully implemented  
**Status**:
```
Billing:
  ✅ IBillingAccountRepository (mostly working)
  ✅ IBillingPaymentRepository (working)

Policy:
  ⏳ IPolicyRepository (stub only)

Customer:
  ⏳ ICustomerRepository (stub only)

Funds Transfer:
  ⏳ IFundTransferRepository (stub only)

Rating:
  ⏳ IQuoteRepository (stub only)
```

**Required Implementation**:
- CRUD operations for each domain entity
- Query methods for business operations (e.g., `GetPoliciesByCustomerId()`)
- Idempotency key checking (for duplicate message handling)
- Cosmos DB single-partition strategy (queries within partition key)

**Effort**: 4-6 hours (implementation + Cosmos DB testing)

---

### 🟡 ISSUE #7: Saga Orchestration (Nice-to-Have for Phase 2)

**Issue**: No sagas implemented for multi-step workflows  
**Impact**: Can't handle workflows like "Quote → Policy → Billing → Payment"

**Examples Needed**:
1. **PolicyIssuanceSaga**: Quote → Policy creation → Billing setup → Customer notification
2. **PaymentProcessingSaga**: Invoice → Fund transfer → Payment recording → Policy activation
3. **RefundSaga**: Policy cancellation → Refund initiation → Refund processing → Account credit

**Status**: ⏭️ Out of scope for Phase 1 (implement after handlers working)

---

## Implementation Phases

### 🎯 Phase 1: Foundation (Week 1 - Critical Path)

**Goal**: Enable basic cross-domain event communication  
**Duration**: 3-4 days

#### Phase 1.1: Service Bus Topology (Day 1)
- [ ] Create domain-specific topics in Terraform
- [ ] Define subscriptions for cross-domain events
- [ ] Update Terraform and apply changes
- [ ] Verify topics/subscriptions in Azure Portal

**Deliverable**: `platform/infra/shared-services/servicebus-domain-topics.tf`

#### Phase 1.2: Public Event Contracts (Day 1)
- [ ] Define all integration event contracts (BillingAccountCreated, PolicyIssued, etc.)
- [ ] Add to `platform/RiskInsure.PublicContracts/Events/`
- [ ] Build and verify solution compiles

**Deliverable**: Event contract files (7-10 new .cs files)

#### Phase 1.3: Billing Message Handlers - COMPLETE ✅ (REFERENCE)
- Already implemented (validation only)
- Use as pattern for other services

#### Phase 1.4: Policy Service Implementation (Days 2-3)
- [ ] Implement `IPolicyRepository` (Cosmos DB CRUD)
- [ ] Implement `IPolicyManager` (business logic)
- [ ] Complete `QuoteAcceptedHandler`
- [ ] Implement `BillingAccountCreatedHandler`
- [ ] Add domain contracts for commands
- [ ] Add unit tests (90%+ coverage)

**Deliverable**: Working Policy service with 2 handlers

#### Phase 1.5: Customer Service Implementation (Days 3-4)
- [ ] Implement `ICustomerRepository` (Cosmos DB CRUD)
- [ ] Implement `ICustomerManager` (business logic)
- [ ] Implement `CustomerCreatedHandler`
- [ ] Add domain contracts for commands
- [ ] Add unit tests (90%+ coverage)

**Deliverable**: Working Customer service with 1 handler

### 🎯 Phase 2: Message Routing (Week 2)

**Goal**: Enable API → Endpoint.In command routing  
**Duration**: 2-3 days

#### Phase 2.1: Configure API SendOnly Mode
- [ ] Update NServiceBus config in all service APIs to use `SendOnly()`
- [ ] Configure routing for commands
- [ ] Update Dockerfile/entrypoint if needed

#### Phase 2.2: API Command Publishing
- [ ] Update all API controllers to publish commands (not call managers directly)
- [ ] Implement 202 Accepted responses for async operations
- [ ] Add integration tests

**Deliverables**: All services with command routing working

### 🎯 Phase 3: Remaining Services (Week 3)

**Goal**: Implement Funds Transfer and Rating services  
**Duration**: 3-4 days

#### Phase 3.1: Funds Transfer Service
- [ ] Complete manager and repository
- [ ] Implement handlers for payment events
- [ ] Add command routing from API
- [ ] Integration tests

#### Phase 3.2: Rating & Underwriting Service
- [ ] Complete manager and repository
- [ ] Implement handlers for policy events
- [ ] Add command routing from API
- [ ] Integration tests

**Deliverables**: All 5 services messaging-ready

### 🎯 Phase 4: Advanced Patterns (Week 4+)

**Goal**: Implement sagas, error handling, observability  
**Duration**: Ongoing

- [ ] Implement workflow sagas (PolicyIssuanceSaga, PaymentSaga, etc.)
- [ ] Add error handling and compensation logic
- [ ] Implement custom health checks
- [ ] Add centralized logging/monitoring
- [ ] E2E integration tests

---

## Specific Implementation Tasks

### Task: Create Service Bus Topics (Terraform)

**File**: `platform/infra/shared-services/servicebus-domain-topics.tf`

```terraform
# Domain-specific topics
resource "azurerm_servicebus_topic" "billing_events" {
  name                = "billing-events"
  namespace_name      = azurerm_servicebus_namespace.riskinsure.name
  resource_group_name = local.resource_group_name
  status              = "Active"
  default_message_ttl = "PT1H"  # 1 hour expiry for dev
}

# Subscriptions: Which endpoints subscribe to which topics
resource "azurerm_servicebus_subscription" "policy_subscribes_to_billing" {
  name                = "policy-on-billing"
  namespace_name      = azurerm_servicebus_namespace.riskinsure.name
  topic_name          = azurerm_servicebus_topic.billing_events.name
  resource_group_name = local.resource_group_name
  # Filters go here to select specific event types
}

# ... repeat for all cross-domain subscriptions
```

**Topics to Create**:
1. `billing-events` → subscribed by: Policy, Funds Transfer
2. `policy-events` → subscribed by: Billing, Customer, Funds Transfer
3. `customer-events` → subscribed by: Policy, Billing
4. `fund-events` → subscribed by: Billing, Policy
5. `quote-events` → subscribed by: Policy, Rating

---

### Task: Implement Policy Service Handler

**File**: `services/policy/src/Endpoint.In/Handlers/BillingAccountCreatedHandler.cs`

```csharp
using NServiceBus;
using RiskInsure.Policy.Domain;
using RiskInsure.PublicContracts.Events;

namespace RiskInsure.Policy.Endpoint.In.Handlers;

public class BillingAccountCreatedHandler : IHandleMessages<BillingAccountCreated>
{
    private readonly IPolicyManager _policyManager;
    private readonly ILogger<BillingAccountCreatedHandler> _logger;

    public BillingAccountCreatedHandler(
        IPolicyManager policyManager,
        ILogger<BillingAccountCreatedHandler> logger)
    {
        _policyManager = policyManager;
        _logger = logger;
    }

    public async Task Handle(BillingAccountCreated message, IMessageHandlerContext context)
    {
        try
        {
            _logger.LogInformation(
                "Processing BillingAccountCreated event for customer {CustomerId}",
                message.CustomerId);

            // Idempotency check
            var existingPolicy = await _policyManager.GetPolicyByAccountIdAsync(message.AccountId);
            if (existingPolicy != null)
            {
                _logger.LogInformation("Policy already exists for account {AccountId}", message.AccountId);
                return;  // Idempotent - safe to ignore duplicate
            }

            // Business logic delegated to manager
            var policy = await _policyManager.CreatePolicyFromBillingAccountAsync(
                message.AccountId,
                message.CustomerId,
                message.StartDate);

            // Publish integration event
            await context.Publish(new PolicyCreatedEvent
            {
                MessageId = Guid.NewGuid(),
                OccurredUtc = DateTime.UtcNow,
                PolicyId = policy.Id,
                CustomerId = message.CustomerId,
                AccountId = message.AccountId,
                IdempotencyKey = message.IdempotencyKey
            });

            _logger.LogInformation("Successfully created policy {PolicyId}", policy.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process BillingAccountCreated event");
            throw;  // NServiceBus will retry
        }
    }
}
```

---

### Task: Update API to Use SendOnly Mode

**File**: `services/policy/src/Api/Program.cs`

```csharp
// Configure NServiceBus (send-only endpoint with routing)
builder.Services.AddSingleton(
    (context) =>
    {
        return builder
            .Host
            .NServiceBusEnvironmentConfiguration(
                "RiskInsure.Policy.Api",
                (config, endpoint, routing) =>
                {
                    endpoint.SendOnly();  // ← API only sends, doesn't receive

                    // Route commands to Policy Endpoint
                    routing.RouteToEndpoint(typeof(CreatePolicyCommand), "RiskInsure.Policy.Endpoint");
                });
    });
```

---

### Task: Add Command Contract

**File**: `services/policy/src/Domain/Contracts/Commands/CreatePolicyCommand.cs`

```csharp
namespace RiskInsure.Policy.Domain.Contracts.Commands;

public record CreatePolicyCommand(
    Guid MessageId,
    DateTimeOffset OccurredUtc,
    Guid CustomerId,
    string PolicyNumber,
    DateTimeOffset EffectiveDate,
    string IdempotencyKey);
```

---

## Success Criteria

### Phase 1 Completion Criteria
- [ ] Service Bus topics created and visible in Azure Portal
- [ ] Subscriptions configured and routed
- [ ] Policy service handlers working (publish events successfully)
- [ ] Customer service API working (accepts requests)
- [ ] Policy API ↔ Billing API can exchange events
- [ ] Service Bus SDK can connect to emulator (dev) and Azure (prod)

### End-to-End Success Criteria
- [ ] All 5 services (Billing, Policy, Customer, Funds Transfer, Rating) deploying successfully
- [ ] Inter-service event delivery working (publish in Service A → handle in Service B)
- [ ] API commands routed to Endpoint.In
- [ ] Handlers processing events without errors
- [ ] Idempotency working (duplicate messages handled gracefully)
- [ ] Unit tests 90%+ coverage for all services
- [ ] Integration tests passing (Playwright/E2E)
- [ ] Health checks green on all Container Apps
- [ ] Observability/logging working (structured logs in Azure Monitor)

---

## Testing Strategy

### Unit Tests
- Manager business logic (80-90% coverage)
- Repository Cosmos DB operations (80% coverage)
- Handler idempotency checks
- Command/event validation

### Integration Tests
- Handler receives event → publishes correct downstream event
- Manager reads/writes to Cosmos DB correctly
- NServiceBus routing configured correctly
- API publishes command → Endpoint.In receives it

### E2E Tests (Playwright)
- Create billing account → Policy created (triggered by event)
- Create policy → Invoice generated (cross-domain event flow)
- Cancel policy → Refund initiated (workflow saga)

---

## Deployment Strategy

### Local Development
1. Start emulators: `./scripts/start-emulators.sh`
2. Run all services locally: Each service API + Endpoint.In
3. ServiceBus Emulator handles event routing
4. Cosmos DB Emulator handles state persistence

### Azure Deployment
1. Apply Terraform (infrastructure)
2. Build all service images with `docker build --no-cache`
3. Push to ACR: `az acr build --registry <name> --image <service>:latest .`
4. Deploy Container Apps: CI/CD pipeline triggers
5. Verify: Check logs in Container Apps, ServiceBus monitoring

---

## Q&A / Risk Mitigation

### Q: What if Service Bus topics already exist?
**A**: Check Azure Portal. If they do, Terraform will import them. No action needed.

### Q: How do we test without full infrastructure?
**A**: Use Service Bus Emulator (included in docker-compose.yml). AutoSubscribe must be enabled in dev.

### Q: What happens to old messages if we change topics?
**A**: Old messages in `bundle-1` will expire after 1 hour (dev) or 14 days (prod). Can be manually purged.

### Q: Can we deploy incrementally (one service at a time)?
**A**: **YES** - Billing is reference. Deploy Policy next (depends on Billing). Then Customer (depends on Policy). Follow dependency graph.

### Q: What if a handler fails?
**A**: NServiceBus retries automatically (configured in `NServiceBusConfigurationExtensions.cs`). After retries, message goes to error queue (manual intervention).

### Q: How do we monitor message flow?
**A**: Azure Service Bus monitoring + Application Insights traces. Structured logging captures message IDs for tracing.

---

## Related Documentation

- [Constitution](copilot-instructions/constitution.md) - Non-negotiable architectural rules
- [Project Structure](copilot-instructions/project-structure.md) - Layer responsibilities
- [Messaging Patterns](copilot-instructions/messaging-patterns.md) - Command/event patterns
- [Domain Events](copilot-instructions/domain-events.md) - Event design patterns
- [Service Bus Topology](service-bus-topology.md) - Topic/subscription design
- [Cross-Domain Integration](copilot-instructions/cross-domain-integration.md) - Inter-service patterns

---

## Timeline & Ownership

| Phase | Duration | Owner | Dependencies |
|-------|----------|-------|--------------|
| Phase 1.1 (Service Bus) | 1 day | DevOps | None |
| Phase 1.2 (Contracts) | 1 day | Backend Lead | Phase 1.1 |
| Phase 1.3 (Billing - Reference) | 0 days | N/A (done) | Phase 1.2 |
| Phase 1.4 (Policy) | 2 days | Backend Dev 1 | Phase 1.2 |
| Phase 1.5 (Customer) | 1 day | Backend Dev 2 | Phase 1.2 |
| Phase 2 (API Routing) | 2 days | Backend Lead | Phase 1.5 |
| Phase 3 (Funds Transfer + Rating) | 2 days | Backend Dev 1+2 | Phase 2 |
| Phase 4 (Sagas + Observability) | Ongoing | Architecture | Phase 3 |

---

## Next Steps

1. **Review this document** with the team (1 hour)
2. **Approve Phase 1 implementation plan** (1 hour)
3. **Start Phase 1.1** (Service Bus Terraform) - begin tomorrow
4. **Create GitHub issues** for each task with acceptance criteria
5. **Schedule weekly syncs** to track progress

**First 48-Hour Goals**:
- [ ] Service Bus topics created
- [ ] Event contracts finalized
- [ ] Policy service partially complete
- [ ] Deployment tested

---

## Version History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-19 | Architecture Review | Initial comprehensive roadmap |
