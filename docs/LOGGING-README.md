# RiskInsure Logging Readme (Azure / Application Insights)

This guide provides KQL snippets to diagnose slow event publishing in `ratingandunderwriting`, especially around `QuoteManager` publish calls.

## Scope

These queries focus on telemetry added in:
- `RiskInsure.RatingAndUnderwriting.Domain.Managers.QuoteManager`
- Activity source: `RiskInsure.RatingAndUnderwriting.Publishing`
- Message publish operation name: `messaging.publish`

## Before You Run Queries

- Set the time range to at least the full test run window.
- Use the same Application Insights resource as the deployed API/Endpoint.
- If table names differ in your workspace (`AppTraces` vs `traces`), use the equivalent table.

---

## 1) Find Slow Publish Logs (direct signal)

```kusto
traces
| where timestamp > ago(24h)
| where cloud_RoleName has "rating" or cloud_RoleName has "underwriting"
| where message has "Slow event publish detected"
| extend EventName = tostring(customDimensions.EventName)
| extend QuoteId = tostring(customDimensions.QuoteId)
| extend ElapsedMs = todouble(customDimensions.ElapsedMs)
| extend ThresholdMs = todouble(customDimensions.ThresholdMs)
| extend IdempotencyKey = tostring(customDimensions.IdempotencyKey)
| project timestamp, cloud_RoleName, operation_Id, EventName, QuoteId, ElapsedMs, ThresholdMs, IdempotencyKey, message
| order by timestamp desc
```

---

## 2) Publish Duration Distribution (P50/P95/P99)

```kusto
traces
| where timestamp > ago(24h)
| where cloud_RoleName has "rating" or cloud_RoleName has "underwriting"
| where message has "published for Quote" or message has "Slow event publish detected"
| extend EventName = tostring(customDimensions.EventName)
| extend ElapsedMs = todouble(customDimensions.ElapsedMs)
| where isnotempty(EventName) and isnotnull(ElapsedMs)
| summarize
    Count = count(),
    P50 = percentile(ElapsedMs, 50),
    P95 = percentile(ElapsedMs, 95),
    P99 = percentile(ElapsedMs, 99),
    Max = max(ElapsedMs)
  by EventName, bin(timestamp, 15m)
| order by timestamp desc, EventName asc
```

---

## 3) Publish Failures / Timeouts

```kusto
traces
| where timestamp > ago(24h)
| where cloud_RoleName has "rating" or cloud_RoleName has "underwriting"
| where message has "Event publish failed" or severityLevel >= 3
| extend EventName = tostring(customDimensions.EventName)
| extend QuoteId = tostring(customDimensions.QuoteId)
| extend ElapsedMs = todouble(customDimensions.ElapsedMs)
| extend IdempotencyKey = tostring(customDimensions.IdempotencyKey)
| project timestamp, severityLevel, cloud_RoleName, operation_Id, EventName, QuoteId, ElapsedMs, IdempotencyKey, message
| order by timestamp desc
```

---

## 4) Drill Into One Quote (end-to-end during a run)

Replace the `quoteId` value first.

```kusto
let quoteId = "<QUOTE_ID_HERE>";
traces
| where timestamp > ago(24h)
| where cloud_RoleName has "rating" or cloud_RoleName has "underwriting"
| where tostring(customDimensions.QuoteId) == quoteId or message has quoteId
| extend EventName = tostring(customDimensions.EventName)
| extend ElapsedMs = todouble(customDimensions.ElapsedMs)
| extend IdempotencyKey = tostring(customDimensions.IdempotencyKey)
| project timestamp, cloud_RoleName, operation_Id, severityLevel, EventName, ElapsedMs, IdempotencyKey, message
| order by timestamp asc
```

---

## 5) Correlate Slow Publish With Service Bus Dependencies

This helps determine if the delay is in broker SDK/network dependency calls.

```kusto
let SlowPublishes = traces
| where timestamp > ago(24h)
| where message has "Slow event publish detected"
| project SlowTs = timestamp, operation_Id, PublishMessage = message,
          EventName = tostring(customDimensions.EventName),
          QuoteId = tostring(customDimensions.QuoteId),
          PublishElapsedMs = todouble(customDimensions.ElapsedMs);

dependencies
| where timestamp > ago(24h)
| where type has "ServiceBus" or target has "servicebus" or name has "ServiceBus"
| join kind=inner SlowPublishes on operation_Id
| project timestamp, SlowTs, cloud_RoleName, operation_Id, type, target, name, duration, resultCode, success,
          EventName, QuoteId, PublishElapsedMs, PublishMessage
| order by timestamp desc
```

---

## 6) Operation Timeline for a Single Correlation Id

Replace operation id from a slow row in Query #1.

```kusto
let op = "<OPERATION_ID_HERE>";
union isfuzzy=true traces, dependencies, requests, exceptions
| where timestamp > ago(24h)
| where operation_Id == op
| project timestamp, itemType, cloud_RoleName, operation_Name, operation_Id,
          message = iff(itemType == "trace", tostring(message), ""),
          depName = iff(itemType == "dependency", tostring(name), ""),
          depTarget = iff(itemType == "dependency", tostring(target), ""),
          depDuration = iff(itemType == "dependency", tostring(duration), ""),
          reqName = iff(itemType == "request", tostring(name), ""),
          reqDuration = iff(itemType == "request", tostring(duration), ""),
          problem = iff(itemType == "exception", tostring(type), "")
| order by timestamp asc
```

---

## 7) Top Slow Events by Volume and Impact

```kusto
traces
| where timestamp > ago(24h)
| where message has "published for Quote" or message has "Slow event publish detected"
| extend EventName = tostring(customDimensions.EventName)
| extend ElapsedMs = todouble(customDimensions.ElapsedMs)
| where isnotempty(EventName) and isnotnull(ElapsedMs)
| summarize
    Count = count(),
    AvgMs = avg(ElapsedMs),
    P95Ms = percentile(ElapsedMs, 95),
    MaxMs = max(ElapsedMs)
  by EventName
| order by P95Ms desc
```

---

## Practical Triage Flow

1. Run **Query #1** to confirm slow publish events exist.
2. Copy one `operation_Id` and run **Query #6**.
3. Run **Query #5** to see if Service Bus dependencies align with the delay.
4. Run **Query #2** and **#7** to identify which event type regresses most.
5. Use **Query #4** with a problematic quote from your E2E test.

## Notes

- Slow threshold is currently **2 seconds** in `QuoteManager` and can be adjusted if needed.
- Local runs may hide transport/network latency that appears in Azure; these queries are intended for deployed test environments.
