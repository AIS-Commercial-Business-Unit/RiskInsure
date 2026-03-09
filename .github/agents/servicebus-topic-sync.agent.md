````chatagent
# Service Bus Resource Sync Agent

**Version**: 1.1.0 | **Type**: Infrastructure Sync Agent | **Last Updated**: 2026-03-03


## Purpose

This agent prevents Azure Service Bus runtime failures caused by missing queues, topics, or subscriptions. It discovers endpoint names, published event types, and subscribed event handlers from code and ensures Terraform in `platform/infra/shared-services/servicebus.tf` is synchronized.

This is required because installers are intentionally kept OFF in all environments.


## Agent Overview

**Name**: `servicebus-resource-sync`  
**Trigger**: `@agent servicebus-resource-sync: Sync endpoint queues, topics, and subscriptions to Terraform`  
**Scope**: Cross-service endpoint/event discovery + Terraform queue/topic/subscription synchronization  
**Mode**: Deterministic, idempotent file update


## Responsibilities

1. Detect all endpoint queue names from endpoint host configuration
2. Detect all published events from source code and resolve topic names
3. Detect all subscribed events from `IHandleMessages<T>` handlers and map to endpoint subscriptions
4. Compare discovered resources to Terraform `locals.queue_names`, `locals.topic_names`, and `locals.subscriptions`
5. Add missing resources to Terraform (no removals)
6. Produce a sync report with changes and unresolved mappings


## Discovery Rules

### Rule 1: Publish Call Detection

Scan all service source code for publish patterns including:

Primary scan roots:

### Rule 1B: Endpoint Queue Detection

Scan endpoint host startup files for:

Primary scan roots:

Queue derivation:

Ignore list (must not be added/managed by this agent):

### Rule 2: Event Type Resolution

For each publish call, resolve the final event type:

Expected topic format:

### Rule 3: Topic Source of Truth

Read `platform/infra/shared-services/servicebus.tf` and locate:

Treat this list as authoritative for provisioning.

### Rule 3B: Subscription Source of Truth

Read `platform/infra/shared-services/servicebus.tf` and locate:

Treat this map as authoritative for topic→endpoint forwarding subscriptions.

### Rule 3C: Subscription Inference

Detect subscriber handlers by scanning endpoint handler classes for:

Infer subscription target endpoint:

Infer topic name:

Infer subscription entry fields:

Infer deterministic key format:

### Rule 4: Sync Behavior


### Rule 5: Deterministic Ordering

After insertion:


## Execution Workflow

### Phase 1: Discover Endpoint Queues, Published Events, and Subscribed Events

1. Search codebase for endpoint names from `.NServiceBusEnvironmentConfiguration("...")`
2. Search codebase for publish invocations and resolve fully-qualified event topics
3. Search codebase for `IHandleMessages<TEvent>` and resolve fully-qualified subscribed event topics
4. Map each handler to owning endpoint name
5. Emit unresolved items list (if any)

### Phase 2: Compare with Terraform

1. Parse current `locals.queue_names`, `locals.topic_names`, and `locals.subscriptions`
2. Compute set difference:
   - `missing_queues = discovered_endpoint_queues - queue_names`
   - `missing_topics = published_events - topic_names`
   - `missing_subscriptions = inferred_subscriptions - subscriptions`

### Phase 3: Apply Terraform Update

1. If any missing set is non-empty:
   - Update `platform/infra/shared-services/servicebus.tf`
   - Insert missing queue names in `locals.queue_names`
   - Insert missing topic names in `locals.topic_names`
   - Insert missing subscriptions in `locals.subscriptions`
   - Keep lists/maps sorted and unique
2. If all missing sets are empty:
   - Report no changes needed

### Phase 4: Report

Return a report with:


## Guardrails



## Example Output

```text
Service Bus Resource Sync Report

Endpoint queues discovered: 5
Published events discovered: 12
Subscribed handler events discovered: 10

Queues already provisioned: 5
Queues added: 0

Topics already provisioned: 9
Topics added: 3
  - RiskInsure.RatingAndUnderwriting.Domain.Contracts.Events.QuoteStarted
  - RiskInsure.RatingAndUnderwriting.Domain.Contracts.Events.UnderwritingSubmitted
  - RiskInsure.RatingAndUnderwriting.Domain.Contracts.Events.QuoteCalculated

Subscriptions already provisioned: 3
Subscriptions added: 2
   - funds_refunded_to_billing
   - funds_settled_to_billing

Unresolved mappings: 1
  - services/policy/src/Domain/Managers/PolicyManager.cs (variable type not inferable)

Status: UPDATED (Terraform queues/topics/subscriptions synchronized)
```


## Suggested Usage


````