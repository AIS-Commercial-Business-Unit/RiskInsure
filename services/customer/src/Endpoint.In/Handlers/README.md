# Customer Endpoint.In

This endpoint processes inbound messages for the Customer domain.

**Note**: Currently, the Customer domain does not subscribe to any events from other domains, so there are no message handlers in this project. The Handlers folder exists for future use when event subscriptions are needed.

## Future Handlers

When Customer domain needs to react to events from other domains (e.g., PolicyCancelled, BillingAccountClosed), handlers will be added to the Handlers folder.
