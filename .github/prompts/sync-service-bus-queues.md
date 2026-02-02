# Sync Service Bus Queues

## Quick Prompt

```
Update services/billing/src/Infrastructure/queues.ps1 to match all current message handlers and event subscriptions
```

## Detailed Instructions

When you need to sync Service Bus queue setup scripts with the current codebase:

1. **Identify the service**: Specify which service (e.g., `billing`, `payments`, `fileintegration`)

2. **Run the scan**: The agent will:
   - Find all `IHandleMessages<T>` implementations
   - Identify command handlers (namespace ends with `.Commands`)
   - Identify event handlers (namespace ends with `.Events`)
   - Find all `context.Publish<T>()` calls to document event publishers
   - Extract endpoint names from `Program.cs` files

3. **Review output**: Check the generated `queues.ps1` for:
   - All endpoints created
   - All command subscriptions (one per command)
   - All event subscriptions (can be multiple per event)
   - Documented event publishers

4. **Verify cross-service subscriptions**: Manually add subscriptions for events from other services

## Command Reference

```powershell
# Create infrastructure queues (once per namespace)
asb-transport queue create error
asb-transport queue create audit
asb-transport queue create particular.monitoring

# Create endpoint
asb-transport endpoint create <EndpointName>

# Subscribe to message
asb-transport endpoint subscribe <EndpointName> <FullTypeName>

# Examples
asb-transport endpoint create RiskInsure.Billing.Endpoint
asb-transport endpoint subscribe RiskInsure.Billing.Endpoint RiskInsure.Billing.Domain.Contracts.Commands.RecordPayment
```

## Validation Checklist

- [ ] Every message handler has a corresponding subscription
- [ ] Command handlers are unique (one handler per command)
- [ ] Event handlers can be multiple (pub/sub pattern)
- [ ] Cross-service subscriptions documented
- [ ] Connection string placeholder present
- [ ] Error, audit, and monitoring queues included

## Related Documentation

- [NServiceBus Documentation](https://docs.particular.net/nservicebus/)
- [ASB Transport CLI](https://docs.particular.net/transports/azure-service-bus/operational-scripting)
- [Infrastructure/.agent-queue-sync.md](../../services/billing/src/Infrastructure/.agent-queue-sync.md)
