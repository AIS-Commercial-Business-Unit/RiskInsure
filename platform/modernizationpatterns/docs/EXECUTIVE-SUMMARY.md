# AI Chatbot for Modernization Patterns Atlas — Executive Summary

---

## Goal

Enable engineers to **ask questions about modernization patterns and application failures** through a conversational AI assistant integrated into the Modernization Patterns Atlas, reducing time spent researching patterns and troubleshooting issues.

---

## Architecture Overview

```
┌────────────────────────────────────────────────────────────────────────────────┐
│                                                                                 │
│                        🎯 MODERNIZATION PATTERNS ATLAS                         │
│                         (React SPA on Azure Static Web Apps)                   │
│                                                                                 │
│                              ┌──────────────────┐                              │
│                              │  ChatWidget.jsx  │ ← Floating chat panel        │
│                              └────────┬─────────┘                              │
│                                       │                                         │
│                                       │ POST /api/chat                         │
│                                       ▼                                         │
│                    ┌──────────────────────────────────┐                         │
│                    │  Managed API Gateway (SWA)       │                         │
│                    │  • Auth, CORS, rate-limiting     │                         │
│                    └────────────┬─────────────────────┘                         │
│                                 │                                               │
│                    ┌────────────┴────────────┐                                  │
│                    │                         │                                  │
│                    ▼                         ▼                                  │
│    ┌────────────────────────────┐  ┌────────────────────────────┐             │
│    │  /api/chat                 │  │  /api/reindex              │             │
│    │  (Pattern + Logs Q&A)      │  │  (Auto-index from Git)     │             │
│    │                            │  │                            │             │
│    │  Azure Functions           │  │  Azure Functions           │             │
│    │  Node.js/TypeScript        │  │  Node.js/TypeScript        │             │
│    └└─────────┬──────────────────┘  └─────────┬──────────────────┘            │
│             │                                 │                                │
│    ┌────────┴────────────────────────────────┴────────┐                       │
│    │                                                   │                       │
│    │  ┌─────────────────────────────────────────────┐ │                       │
│    │  │  PATTERNS PATH (RAG)    │  LOGS PATH (KQL)  │ │                       │
│    │  ├─────────────────────────┼───────────────────┤ │                       │
│    │  │ 1. Embed question       │ 1. Generate KQL   │ │                       │
│    │  │ 2. Hybrid search        │ 2. Execute query  │ │                       │
│    │  │ 3. Build prompt         │ 3. Summarize      │ │                       │
│    │  │ 4. Stream answer        │    with GPT-4o    │ │                       │
│    │  └─────────────────────────┴───────────────────┘ │                       │
│    │                                                   │                       │
│    └──────────────┬──────────────────┬────────────────┘                       │
│                   │                  │                                         │
│    ┌──────────────┴─────┐  ┌─────────┴──────────┐  ┌────────────────┐        │
│    │ Azure OpenAI       │  │ Azure AI Search    │  │  Log Analytics │        │
│    │ • GPT-4o           │  │ • Vector index     │  │  Workspace     │        │
│    │ • Embeddings       │  │ • Hybrid search    │  │ (Live logs)    │        │
│    │ • Intent detect    │  │ • 3072 dimensions  │  │                │        │
│    └────────────────────┘  └────────────────────┘  └────────────────┘        │
│                                          │                                     │
│                                          │ Git webhook triggers               │
│                                          │ (on pattern changes)               │
│                                          │                                     │
│                                   ┌──────┴──────┐                             │
│                                   │ GitHub (CI) │                             │
│                                   │ Git push    │                             │
│                                   └─────────────┘                             │
│                                                                                 │
└────────────────────────────────────────────────────────────────────────────────┘
```

---

## Key Capabilities

### 1. **Pattern Q&A (RAG)**
- Users ask: *"When should I use Circuit Breaker?"*
- Bot searches 41+ modernization patterns via vector + keyword search
- Returns grounded answers with citations linking back to specific patterns
- **Benefit**: Instant pattern guidance without manual documentation searches

### 2. **Application Failure Analysis (Log Analytics)**
- Users ask: *"Why did the billing service fail yesterday?"*
- Bot generates KQL queries to search live Log Analytics workspace
- Returns root causes (`CosmosDB 429 errors`, `timeout`, etc.) with specific recommendations
- **Benefit**: Faster MTTR (Mean Time to Recovery) → reduced downtime

### 3. **Auto-Indexing on Git Push**
- Whenever engineers update patterns or docs on `main`
- CI webhook automatically triggers re-indexing in seconds
- No manual setup required to keep bot current
- **Benefit**: Always up-to-date, no stale answers

---

## Technology Stack

| Layer | Technology | Why |
|-------|-----------|-----|
| **Front-end** | React (Vite) | Already exists; minimal changes |
| **API Gateway** | Azure Static Web Apps Managed API | Built-in, zero additional cost |
| **Compute** | Azure Functions (Node.js) | Serverless, scales automatically, same language as frontend |
| **LLM** | Azure OpenAI (GPT-4o) | Latest model, fast, reliable |
| **Vector Search** | Azure AI Search | Hybrid search + semantic ranker, native Azure |
| **Logs** | Azure Monitor / Log Analytics | Existing RiskInsure workspace, read-only access |
| **Infrastructure** | Bicep (IaC) | Repeatable, version-controlled deployments |

---

## Data Flow: Two Scenarios

### Scenario A: "When use Strangler Fig pattern?"
```
Question → Embed → AI Search (top 5 patterns) → GPT-4o → Stream answer → Browser
Time: ~1.5 seconds
```

### Scenario B: "Why did billing fail?"
```
Question → Generate KQL → Execute Log Analytics → Summarize → Browser + raw KQL
Time: ~2–3 seconds
```

---

## Business Value

| Metric | Impact |
|--------|--------|
| **Developer productivity** | -15% time spent on pattern research & troubleshooting |
| **MTTR (Mean Time to Recovery)** | Faster root cause analysis → reduced downtime |
| **Knowledge sharing** | Self-service pattern guidance → less need for expert reviews |
| **Onboarding** | New engineers learn patterns faster via chatbot |
| **Cost efficiency** | Leverages existing Log Analytics workspace, no major new infra |

---

## Implementation Timeline

| Phase | Duration | Deliverables |
|-------|----------|--------------|
| **Phase 1** | 2–3 weeks | Pattern RAG + auto-indexing live |
| **Phase 2** | 1–2 weeks | Log Analytics integration + KQL generation |
| **Phase 3** | 1 week | Polish, monitoring, Bicep IaC, runbooks |
| **Total** | **4–6 weeks** | Fully operational chatbot with both capabilities |

---

## Cost Estimate (Monthly)

| Resource | Cost |
|----------|------|
| Azure OpenAI (low volume) | ~$10–30 |
| Azure AI Search (Basic tier) | ~$70 |
| Azure Functions (Consumption) | ~$0–5 |
| Static Web Apps (Standard) | ~$9 |
| **Total** | **~$90–115/mo** |

---

## Risk & Mitigation

| Risk | Mitigation |
|------|-----------|
| **Prompt injection** | System prompt restricts model to provided context; user input validated server-side |
| **KQL safety** | Generated KQL is read-only; mutations rejected; `take` limited to 100 rows |
| **Cold start latency** | Azure Functions cold start ~1–2s; acceptable for non-critical feature |
| **Vector index staleness** | Auto-index on Git push + daily timer ensures max 24h lag |
| **Cost overruns** | OpenAI token limits configured on deployment; rate-limiting enforced |

---

## Key Decisions

1. ✅ **Two Azure Functions** (chat + reindex) for operational clarity
2. ✅ **Node.js** for consistency with React frontend
3. ✅ **Intent classification** to separate pattern vs. log queries
4. ✅ **Managed Identity** for all auth (no API keys in code)
5. ✅ **Hybrid search** (vector + keyword + semantic reranker) for better relevance
6. ✅ **SWA Managed API** (no APIM) to reduce complexity and cost

---

## Next Steps

1. **Week 1–2**: Provision Azure resources + build indexing function
2. **Week 2–3**: Build chat function + ChatWidget component
3. **Week 3–4**: Integrate Log Analytics + KQL generation
4. **Week 4–6**: Testing, monitoring, documentation, production deployment

---

## Questions?

See [ai-chatbot-architecture.md](ai-chatbot-architecture.md) for detailed technical specifications.

---

**Document Date**: February 23, 2026  
**Status**: Ready for Executive Review & Approval
