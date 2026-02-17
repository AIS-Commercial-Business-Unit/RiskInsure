# Service Bus Topology: Topics, Subscriptions, and Event Routing

**Status**: Documentation for infrastructure fix  
**Last Updated**: February 12, 2026  
**Audience**: DevOps, Backend Engineers, Infrastructure Team

---

## Problem Statement

Your e2e tests are failing because **messages are not being routed between endpoints**. When one service publishes an event, another service's endpoint doesn't receive it—even though both are connected to the same Service Bus.

### Example of Current Failure

1. **Quote service publishes** `QuoteApproved` event
2. **Policy endpoint** is waiting to handle `QuoteApproved` (to create a policy)
3. **Result**: Message goes into one queue, Policy's endpoint listens on a different queue
4. **Outcome**: Message never reaches Policy endpoint → e2e test times out → failure

---

## Root Cause Analysis

### Current Broken Architecture

**Terraform** (`platform/infra/shared-services/servicebus.tf`):
```terraform
resource "azurerm_servicebus_topic" "bundle" {
  name = "bundle-1"  # ← ONE generic topic for everything
}
# NO subscriptions defined
```

**Code** (all `NServiceBusConfigurationExtensions.cs`):
```csharp
endpointConfiguration.DisableFeature<AutoSubscribe>();  # ← Disabled in dev
```

**What Happens**:
- All events from all domains → single `bundle-1` topic
- AutoSubscribe is disabled (doesn't work on emulator)
- No explicit subscriptions → no endpoint listening
- Message is published but nobody receives it
- After 1 hour, message expires and is deleted

### Why This Breaks Both Emulator AND Azure Production

| Stage | Emulator | Azure Production |
|-------|----------|------------------|
| **Dev with emulator** | ✗ Topics/subscriptions not fully supported + AutoSubscribe disabled = messages lost | N/A |
| **Prod deployment to Azure** | N/A | ✗ Only `bundle-1` in Terraform + no subscriptions = same routing failure |

**Conclusion**: This is not just an emulator problem. The routing architecture is fundamentally broken for both environments.

---

## Solution Architecture

### Core Principle: Domain Events → Domain Topics → Multi-Endpoint Subscriptions

```
Quote Service publishes → billing-events topic
                            ├─ Policy subscription
                            ├─ FundsTransferMgt subscription
                            └─ Premium subscription
```

Each subscription is **independent**:
- Policy endpoint processes via its own subscription
- FundsTransferMgt endpoint processes via its own subscription
- Each maintains its own checkpoint, dead-letter queue, and retry policy
- If Policy fails, FundsTransferMgt still processes the message

### Design Decision: One Topic per Publisher + One Subscription per Listener

**Why ONE topic per PUBLISHER domain?**
- Clear event categorization (all Billing events in one place)
- Easy to monitor domain-specific events
- Subscribers know where to find events from a domain
- Supports future filtering and routing rules

**Why ONE subscription per LISTENER endpoint?**
- Independent processing and failure handling
- Each endpoint controls its own retry/dead-letter behavior
- Prevents competition for messages
- Enables per-endpoint monitoring

---

## Current vs. Target State

### CURRENT (Broken)

```
Service Bus Namespace
└── Topic: bundle-1 (generic catch-all)
    └── (NO subscriptions)
    
Each Endpoint Queue
├── RiskInsure.Billing.Endpoint.In
├── RiskInsure.Policy.Endpoint.In
├── RiskInsure.Customer.Endpoint.In
├── RiskInsure.RatingAndUnderwriting.Endpoint.In
├── RiskInsure.FundsTransferMgt.Endpoint.In
└── RiskInsure.Premium.Endpoint.In

Problem: No link between topic and endpoint queues
```

### TARGET (Fixed)

```
Service Bus Namespace
├── Queue: RiskInsure.Billing.Endpoint.In (for commands)
├── Queue: RiskInsure.Policy.Endpoint.In (for commands)
│
├── Topic: billing-events (events from Billing domain)
│   ├── Subscription: policy-sub
│   ├── Subscription: fundstransfermgt-sub
│   ├── Subscription: premium-sub
│   ├── Subscription: ratingunderwriting-sub
│   └── Subscription: customer-sub
│
├── Topic: policy-events (events from Policy domain)
│   ├── Subscription: billing-sub
│   ├── Subscription: fundstransfermgt-sub
│   └── Subscription: customer-sub
│
├── Topic: customer-events (events from Customer domain)
│   ├── Subscription: billing-sub
│   ├── Subscription: policy-sub
│   └── Subscription: ratingunderwriting-sub
│
├── Topic: ratingunderwriting-events (events from RatingAndUnderwriting domain)
│   ├── Subscription: policy-sub
│   ├── Subscription: premium-sub
│   └── Subscription: billing-sub
│
├── Topic: fundstransfermgt-events (events from FundsTransferMgt domain)
│   └── Subscription: billing-sub
│
└── Topic: premium-events (events from Premium domain)
    ├── Subscription: policy-sub
    ├── Subscription: customer-sub
    └── Subscription: billing-sub
```

---

## Implementation Plan

### Phase 1: Terraform Infrastructure (platform/infra/shared-services/servicebus.tf)

Create **one topic per domain**:

```terraform
# ==========================================================================
# Domain-Specific Event Topics
# ==========================================================================

resource "azurerm_servicebus_topic" "billing_events" {
  name              = "billing-events"
  namespace_id      = azurerm_servicebus_namespace.riskinsure.id
  max_size_in_megabytes = 1024
}

resource "azurerm_servicebus_topic" "policy_events" {
  name              = "policy-events"
  namespace_id      = azurerm_servicebus_namespace.riskinsure.id
  max_size_in_megabytes = 1024
}

resource "azurerm_servicebus_topic" "customer_events" {
  name              = "customer-events"
  namespace_id      = azurerm_servicebus_namespace.riskinsure.id
  max_size_in_megabytes = 1024
}

resource "azurerm_servicebus_topic" "ratingunderwriting_events" {
  name              = "ratingunderwriting-events"
  namespace_id      = azurerm_servicebus_namespace.riskinsure.id
  max_size_in_megabytes = 1024
}

resource "azurerm_servicebus_topic" "fundstransfermgt_events" {
  name              = "fundstransfermgt-events"
  namespace_id      = azurerm_servicebus_namespace.riskinsure.id
  max_size_in_megabytes = 1024
}

resource "azurerm_servicebus_topic" "premium_events" {
  name              = "premium-events"
  namespace_id      = azurerm_servicebus_namespace.riskinsure.id
  max_size_in_megabytes = 1024
}

resource "azurerm_servicebus_topic" "fileintegration_events" {
  name              = "fileintegration-events"
  namespace_id      = azurerm_servicebus_namespace.riskinsure.id
  max_size_in_megabytes = 1024
}
```

Create **subscriptions per listener**:

```terraform
# ==========================================================================
# Billing Events Subscriptions
# ==========================================================================

resource "azurerm_servicebus_topic_subscription" "policy_sub_billing" {
  name                = "policy-subscription"
  topic_id            = azurerm_servicebus_topic.billing_events.id
  max_delivery_count  = 10
  
  # Enable dead-letter on filter match
  dead_letter_on_filter_evaluation_exception = true
}

resource "azurerm_servicebus_topic_subscription" "fundstransfermgt_sub_billing" {
  name                = "fundstransfermgt-subscription"
  topic_id            = azurerm_servicebus_topic.billing_events.id
  max_delivery_count  = 10
  dead_letter_on_filter_evaluation_exception = true
}

resource "azurerm_servicebus_topic_subscription" "premium_sub_billing" {
  name                = "premium-subscription"
  topic_id            = azurerm_servicebus_topic.billing_events.id
  max_delivery_count  = 10
  dead_letter_on_filter_evaluation_exception = true
}

resource "azurerm_servicebus_topic_subscription" "ratingunderwriting_sub_billing" {
  name                = "ratingunderwriting-subscription"
  topic_id            = azurerm_servicebus_topic.billing_events.id
  max_delivery_count  = 10
  dead_letter_on_filter_evaluation_exception = true
}

resource "azurerm_servicebus_topic_subscription" "customer_sub_billing" {
  name                = "customer-subscription"
  topic_id            = azurerm_servicebus_topic.billing_events.id
  max_delivery_count  = 10
  dead_letter_on_filter_evaluation_exception = true
}

# ==========================================================================
# Policy Events Subscriptions
# ==========================================================================

resource "azurerm_servicebus_topic_subscription" "billing_sub_policy" {
  name                = "billing-subscription"
  topic_id            = azurerm_servicebus_topic.policy_events.id
  max_delivery_count  = 10
  dead_letter_on_filter_evaluation_exception = true
}

resource "azurerm_servicebus_topic_subscription" "fundstransfermgt_sub_policy" {
  name                = "fundstransfermgt-subscription"
  topic_id            = azurerm_servicebus_topic.policy_events.id
  max_delivery_count  = 10
  dead_letter_on_filter_evaluation_exception = true
}

resource "azurerm_servicebus_topic_subscription" "customer_sub_policy" {
  name                = "customer-subscription"
  topic_id            = azurerm_servicebus_topic.policy_events.id
  max_delivery_count  = 10
  dead_letter_on_filter_evaluation_exception = true
}

# ==========================================================================
# Customer Events Subscriptions
# ==========================================================================

resource "azurerm_servicebus_topic_subscription" "billing_sub_customer" {
  name                = "billing-subscription"
  topic_id            = azurerm_servicebus_topic.customer_events.id
  max_delivery_count  = 10
  dead_letter_on_filter_evaluation_exception = true
}

resource "azurerm_servicebus_topic_subscription" "policy_sub_customer" {
  name                = "policy-subscription"
  topic_id            = azurerm_servicebus_topic.customer_events.id
  max_delivery_count  = 10
  dead_letter_on_filter_evaluation_exception = true
}

resource "azurerm_servicebus_topic_subscription" "ratingunderwriting_sub_customer" {
  name                = "ratingunderwriting-subscription"
  topic_id            = azurerm_servicebus_topic.customer_events.id
  max_delivery_count  = 10
  dead_letter_on_filter_evaluation_exception = true
}

# ==========================================================================
# RatingAndUnderwriting Events Subscriptions
# ==========================================================================

resource "azurerm_servicebus_topic_subscription" "policy_sub_ratingunderwriting" {
  name                = "policy-subscription"
  topic_id            = azurerm_servicebus_topic.ratingunderwriting_events.id
  max_delivery_count  = 10
  dead_letter_on_filter_evaluation_exception = true
}

resource "azurerm_servicebus_topic_subscription" "premium_sub_ratingunderwriting" {
  name                = "premium-subscription"
  topic_id            = azurerm_servicebus_topic.ratingunderwriting_events.id
  max_delivery_count  = 10
  dead_letter_on_filter_evaluation_exception = true
}

resource "azurerm_servicebus_topic_subscription" "billing_sub_ratingunderwriting" {
  name                = "billing-subscription"
  topic_id            = azurerm_servicebus_topic.ratingunderwriting_events.id
  max_delivery_count  = 10
  dead_letter_on_filter_evaluation_exception = true
}

# ==========================================================================
# FundsTransferMgt Events Subscriptions
# ==========================================================================

resource "azurerm_servicebus_topic_subscription" "billing_sub_fundstransfermgt" {
  name                = "billing-subscription"
  topic_id            = azurerm_servicebus_topic.fundstransfermgt_events.id
  max_delivery_count  = 10
  dead_letter_on_filter_evaluation_exception = true
}

# ==========================================================================
# Premium Events Subscriptions
# ==========================================================================

resource "azurerm_servicebus_topic_subscription" "policy_sub_premium" {
  name                = "policy-subscription"
  topic_id            = azurerm_servicebus_topic.premium_events.id
  max_delivery_count  = 10
  dead_letter_on_filter_evaluation_exception = true
}

resource "azurerm_servicebus_topic_subscription" "customer_sub_premium" {
  name                = "customer-subscription"
  topic_id            = azurerm_servicebus_topic.premium_events.id
  max_delivery_count  = 10
  dead_letter_on_filter_evaluation_exception = true
}

resource "azurerm_servicebus_topic_subscription" "billing_sub_premium" {
  name                = "billing-subscription"
  topic_id            = azurerm_servicebus_topic.premium_events.id
  max_delivery_count  = 10
  dead_letter_on_filter_evaluation_exception = true
}

# ==========================================================================
# FileIntegration Events Subscriptions
# ==========================================================================

resource "azurerm_servicebus_topic_subscription" "billing_sub_fileintegration" {
  name                = "billing-subscription"
  topic_id            = azurerm_servicebus_topic.fileintegration_events.id
  max_delivery_count  = 10
  dead_letter_on_filter_evaluation_exception = true
}
```

### Phase 2: Enable NServiceBus AutoSubscribe (for Production)

**In production**, NServiceBus can auto-subscribe when `AutoSubscribe` is enabled and proper topics/subscriptions exist.

**Update** `services/*/src/Infrastructure/NServiceBusConfigurationExtensions.cs`:

**Current** (both dev and prod disable):
```csharp
endpointConfiguration.DisableFeature<AutoSubscribe>();
```

**Target** (enable in production only):
```csharp
if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
{
    // Auto-subscribe enabled - requires topics/subscriptions to exist in Terraform
    // NServiceBus will register subscriptions for all handled events
}
else
{
    // Development: Disable because emulator doesn't support auto-subscribe properly
    endpointConfiguration.DisableFeature<AutoSubscribe>();
}
```

### Phase 3: Verify Publisher Configuration (No Changes Needed)

Each endpoint should **publish events to its own domain topic**.

**Example: Policy endpoint publishes to policy-events topic**

This is typically handled by NServiceBus automatically when you call `context.Publish(new PolicyCreated(...))`.

---

## Concrete Example: Quote → Policy Flow

### Current (Broken)

```
Quote Service
  ├─ publishes QuoteApproved
  └─ → bundle-1 topic (no subscription) → MESSAGE LOST

Policy Endpoint.In
  ├─ listening for QuoteApproved
  ├─ but listening on policy endpoint queue (not topic subscription)
  └─ MESSAGE NEVER ARRIVES
```

### After Fix

```
Quote Service
  ├─ publishes QuoteApproved
  └─ → billing-events topic ✓

Service Bus
  └─ billing-events topic
      └─ policy-subscription ✓

Policy Endpoint.In
  ├─ subscribes to policy-sub (created in Terraform)
  └─ RECEIVES MESSAGE ✓

Handler in Policy Endpoint
  ├─ CreatePolicyFromQuoteHandler
  ├─ processes QuoteApproved
  └─ creates policy in Cosmos DB ✓
```

### NServiceBus Code (No Changes to Business Logic)

```csharp
// Quote service - just publish, NServiceBus routes to billing-events topic
await context.Publish(new QuoteApproved(
    MessageId: Guid.NewGuid(),
    OccurredUtc: DateTimeOffset.UtcNow,
    QuoteId: quoteId,
    IdempotencyKey: $"quote-{quoteId}"
));

// Policy endpoint - handler receives it automatically via subscription
public class CreatePolicyFromQuoteHandler : IHandleMessages<QuoteApproved>
{
    public async Task Handle(QuoteApproved message, IMessageHandlerContext context)
    {
        // Create policy from approved quote
        await _policyManager.CreatePolicyFromQuoteAsync(message.QuoteId);
        
        // Publish new event
        await context.Publish(new PolicyCreated(...));
    }
}
```

---

## Event Routing Matrix

This matrix shows which events belong to which topics and which endpoints should be subscribed:

| Event | Publisher Topic | Subscribers |
|-------|-----------------|-------------|
| `BillingAccountCreated` | billing-events | Policy, FundsTransferMgt, Premium, Customer, RatingAndUnderwriting |
| `QuoteApproved` | billing-events | Policy |
| `PolicyCreated` | policy-events | Billing, FundsTransferMgt, Customer |
| `PolicyIssued` | policy-events | Billing, FundsTransferMgt, Customer |
| `RiskAssessmentCompleted` | ratingunderwriting-events | Policy, Premium, Billing |
| `PaymentInstructionReady` | fileintegration-events | Billing |
| `AchPaymentInstructionProcessed` | fileintegration-events | Billing |
| `CustomerCreated` | customer-events | Billing, Policy, RatingAndUnderwriting |

**Note**: You'll need to document the complete list in your domain contracts. Each event has a specific publisher and a specific set of subscribers.

---

## Testing Strategy

### Local Development (Emulator)

1. **Disable AutoSubscribe** (stays as-is for now - keep for dev)
2. **Create topics/subscriptions manually** via script:
   ```bash
   # scripts/create-servicebus-topology.sh (new)
   az servicebus topic create --resource-group mygroup --namespace-name mybus --name billing-events
   az servicebus topic subscription create --resource-group mygroup \
     --namespace-name mybus --topic-name billing-events --name policy-subscription
   ```

### Azure Deployment

1. `terraform apply` creates all topics and subscriptions
2. NServiceBus `AutoSubscribe` enabled
3. Verify subscriptions created in Azure Portal:
   - Navigate to Service Bus → Topics
   - Click each topic → Subscriptions
   - Verify expected subscriptions exist

### E2E Testing

```csharp
// test/e2e/tests/quote-to-policy-flow.spec.ts
test("Quote approved → Policy endpoint receives and creates policy", async ({ page }) => {
    // 1. Create quote via Quote API
    const quoteResponse = await page.request.post("/api/quotes", { ... });
    
    // 2. Approve quote via Quote API
    await page.request.post(`/api/quotes/${quoteResponse.id}/approve`, { ... });
    
    // 3. Wait for Policy Endpoint to process (max 5 seconds)
    await page.waitForTimeout(5000);
    
    // 4. Verify policy was created via Policy API
    const policyResponse = await page.request.get(`/api/policies?quoteId=${quoteResponse.id}`);
    expect(policyResponse.status()).toBe(200);
    expect(await policyResponse.json()).toHaveProperty("id");
});
```

---

## Troubleshooting

### Message Not Received

**Symptom**: Published event, but handler never processes it.

**Checklist**:
- [ ] Topic exists in Service Bus (check Azure Portal → Topics)
- [ ] Subscription exists on the topic (Portal → Topics → {topic} → Subscriptions)
- [ ] Subscription name matches endpoint name (e.g., `policy-subscription`)
- [ ] Handler is registered in endpoint (`IHandleMessages<YourEvent>`)
- [ ] Event is marked as public contract (in `PublicContracts` project)
- [ ] Emulator script created subscriptions correctly
- [ ] AutoSubscribe disabled for dev (expected), enabled properly for prod

### Multiple Endpoints Receiving Duplicate Messages

**Symptom**: Same message processed twice by two endpoints.

**Cause**: Subscription misconfiguration or handler registered twice.

**Fix**:
- Verify each endpoint has ONE subscription per topic (not multiple)
- Check `Endpoint.In/Program.cs` doesn't register same handler twice
- Verify topic/subscription routing is correct in Terraform

### Dead-Letter Queue Growing

**Symptom**: Messages failing and accumulating in dead-letter.

**Check**:
- [ ] Handler throwing exception?
- [ ] Max delivery count reached? (default 10)
- [ ] Message format correct?
- [ ] Cosmos DB connection working?

---

## Summary of Changes

| Component | Current | Target | Impact |
|-----------|---------|--------|--------|
| **Terraform** | 1 generic topic (`bundle-1`) | 7 domain topics (billing, policy, etc.) | ✅ Enables proper routing |
| **Terraform** | 0 subscriptions | ~25+ subscriptions (1 per listener per topic) | ✅ Links topics to endpoints |
| **Code** | AutoSubscribe disabled everywhere | AutoSubscribe disabled in dev, enabled in prod | ✅ Auto-registers subscriptions in Azure |
| **Code** | No routing config | None needed (NServiceBus handles via topics/subs) | ✅ Simplifies code |
| **Testing** | Messages get lost | Messages successfully routed | ✅ E2E tests pass |

---

## References

- [NServiceBus Publishing](https://docs.particular.net/nservicebus/publish-subscribe/)
- [Azure Service Bus Topics and Subscriptions](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-queues-topics-subscriptions)
- [RiskInsure Messaging Patterns](messaging-patterns.md)
- [RiskInsure Domain Events](domain-events.md)
- [TERRAFORM-ANALYSIS.md](../TERRAFORM-ANALYSIS.md) - Full infra analysis

---

## Next Steps

1. **Review this document** with the team
2. **Create GitHub issue** to track implementation
3. **Design subscription naming convention** (e.g., `{domain}-subscription` or `{endpoint}-{topic}`)
4. **Update Terraform** with topics and subscriptions
5. **Create local setup script** for emulator (bash or PowerShell)
6. **Test e2e flow** Quote → Policy → Premium
7. **Deploy to dev Azure environment**
8. **Verify in production environment**
