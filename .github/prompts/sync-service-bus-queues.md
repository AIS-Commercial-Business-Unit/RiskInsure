# Sync RabbitMQ Topology

## Quick Prompt

```
Update services/billing/src/Infrastructure/queues.ps1 to match all current message handlers and event subscriptions
```

## Detailed Instructions

When you need to sync RabbitMQ queue/exchange setup scripts with the current codebase:

1. **Identify the service**: Specify which service (e.g., `billing`, `payments`, `fileintegration`)

2. **Run the scan**: The agent will:
   - Find all `IHandleMessages<T>` implementations
   - Identify command handlers (namespace ends with `.Commands`)
   - Identify event handlers (namespace ends with `.Events`)
   - Find all `context.Publish<T>()` calls to document event publishers
   - Extract endpoint names from `Program.cs` files

3. **Review output**: Check the generated `queues.ps1` for:
   - All endpoints created
   - Required queues/exchanges exist
   - Required bindings exist for subscribed events
   - Documented event publishers

4. **Verify cross-service bindings**: Manually add bindings for events from other services

## Command Reference

```powershell
# List queues
docker exec rabbitmq rabbitmqctl list_queues name messages consumers

# List exchanges
docker exec rabbitmq rabbitmqctl list_exchanges name type

# Optional: use rabbitmqadmin for scripted declarations
# rabbitmqadmin declare queue name=<EndpointQueueName> durable=true
# rabbitmqadmin declare exchange name=<ExchangeName> type=topic durable=true
# rabbitmqadmin declare binding source=<ExchangeName> destination_type=queue destination=<EndpointQueueName> routing_key=<RoutingKey>

# Examples
docker exec rabbitmq rabbitmqctl list_queues name | grep RiskInsure.Billing.Endpoint
```

## Validation Checklist

- [ ] Every message handler has a corresponding subscription
- [ ] Command handlers are unique (one handler per command)
- [ ] Event handlers can be multiple (pub/sub pattern)
- [ ] Cross-service bindings documented
- [ ] Connection string placeholder present
- [ ] Error, audit, and monitoring queues included

## Related Documentation

- [NServiceBus Documentation](https://docs.particular.net/nservicebus/)
- [NServiceBus RabbitMQ Transport](https://docs.particular.net/transports/rabbitmq/)
- [Infrastructure/.agent-queue-sync.md](../../services/billing/src/Infrastructure/.agent-queue-sync.md)
