# AI Chatbot Architecture — Modernization Patterns Atlas

> **Goal**: Add a conversational AI assistant to the Modernization Patterns Atlas that can  
> (1) answer questions about any of the 41+ modernization patterns, and  
> (2) query Azure Log Analytics to explain *why an application failed*.

---

## 1  High-Level Summary

| Layer | Technology | Purpose |
|-------|-----------|---------|
| **Front-end** | React chat component (existing Vite SPA) | Renders chat UI inside the Atlas |
| **API gateway** | Azure Static Web Apps managed API *or* Azure API Management | Routes `/api/chat` to the back-end |
| **Back-end API** | Azure Functions (Node.js / Python) | Orchestrates RAG pipeline + Log Analytics queries |
| **LLM** | Azure OpenAI Service (GPT-4o / GPT-4.1) | Generates natural-language answers |
| **Vector search** | Azure AI Search (vector index) | Stores pattern embeddings for semantic retrieval |
| **Embeddings** | Azure OpenAI `text-embedding-3-large` | Converts patterns & queries into vectors |
| **Log Analytics** | Azure Monitor / Log Analytics Workspace | Stores application logs & failures |
| **Indexing pipeline** | Azure Functions (timer / CI trigger) | Chunks, embeds, and pushes pattern content to AI Search |

---

## 2  Architecture Diagram (Logical)

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                         USER (Browser)                                       │
│  ┌────────────────────────────────────────────────────────────────────────┐  │
│  │  Modernization Patterns Atlas  (React SPA on Azure Static Web Apps)   │  │
│  │  ┌──────────────────┐                                                 │  │
│  │  │  ChatWidget.jsx  │  ← floating chat panel                          │  │
│  │  └────────┬─────────┘                                                 │  │
│  └───────────┼───────────────────────────────────────────────────────────┘  │
└──────────────┼──────────────────────────────────────────────────────────────┘
               │ POST /api/chat  { question, conversationId }
               ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│  AZURE STATIC WEB APPS — Managed API  (or APIM)                             │
│  · CORS, auth, rate-limiting                                                 │
└──────────────┬───────────────────────────────────────────────────────────────┘
               │
               ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│  AZURE FUNCTIONS — Chat Orchestrator                                         │
│                                                                              │
│  1. Intent detection  ─────────────────────────────────────────────┐         │
│     "Is this about patterns or about app failures/logs?"           │         │
│                                                                    │         │
│  ┌─────────────────────────────┐    ┌──────────────────────────┐   │         │
│  │  PATTERNS path (RAG)       │    │  LOGS path               │   │         │
│  │                             │    │                          │   │         │
│  │  2a. Embed user question    │    │  2b. Generate KQL query  │   │         │
│  │      → text-embedding-3    │    │      via Azure OpenAI    │   │         │
│  │  3a. Vector search          │    │  3b. Execute KQL against │   │         │
│  │      → Azure AI Search     │    │      Log Analytics API   │   │         │
│  │  4a. Build prompt with      │    │  4b. Summarise results   │   │         │
│  │      retrieved chunks       │    │      with Azure OpenAI   │   │         │
│  └─────────────┬───────────────┘    └────────────┬─────────────┘   │         │
│                │                                  │                 │         │
│                └──────────┬───────────────────────┘                 │         │
│                           ▼                                        │         │
│              5. Azure OpenAI GPT-4o                                │         │
│                 → grounded answer + citations                      │         │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
               │
               ▼  streams / returns JSON answer
┌──────────────────────────────────────────────────────────────────────────────┐
│  INDEXING PIPELINE  (runs on CI or timer)                                    │
│                                                                              │
│  content/patterns/*.json  →  chunk  →  embed  →  upsert to AI Search index  │
│  copilot-instructions/*   →  chunk  →  embed  →  upsert to AI Search index  │
│  README / docs            →  chunk  →  embed  →  upsert to AI Search index  │
└──────────────────────────────────────────────────────────────────────────────┘
```

---

## 3  Component Details

### 3.1  Front-End — Chat Widget

| Item | Detail |
|------|--------|
| **Component** | `ChatWidget.jsx` — a floating panel toggled by a button in the bottom-right corner |
| **State** | Conversation history stored in React state; optionally persisted to `sessionStorage` |
| **API call** | `POST /api/chat` with `{ question, conversationHistory[] }` |
| **Streaming** | Use `fetch` with `ReadableStream` to display tokens as they arrive (SSE or chunked JSON) |
| **UX extras** | Citation links that deep-link to `/pattern/:slug`; "Ask about logs" toggle |

### 3.2  API Layer — Azure Functions

A single HTTP-triggered Azure Function (or two: one for chat, one for indexing).

#### Chat function pseudo-code

```text
POST /api/chat
  ├─ 1. Classify intent (patterns vs. logs) using a lightweight system prompt
  │
  ├─ PATTERNS path
  │     ├─ Embed question  →  Azure OpenAI text-embedding-3-large
  │     ├─ Search Azure AI Search index  (top-k = 5, vector + keyword hybrid)
  │     ├─ Build system prompt:
  │     │     "You are the Modernization Patterns assistant.
  │     │      Answer using ONLY the following context: {chunks}.
  │     │      Cite patterns by title."
  │     └─ Call Azure OpenAI GPT-4o  →  stream answer back
  │
  ├─ LOGS path
  │     ├─ Generate KQL query from natural language via GPT-4o
  │     │     system prompt includes Log Analytics table schema
  │     ├─ Execute KQL via Azure Monitor Query API
  │     │     POST https://api.loganalytics.azure.com/v1/workspaces/{id}/query
  │     ├─ Summarise results via GPT-4o
  │     │     "Explain why the app failed based on these log rows: {rows}"
  │     └─ Return answer + raw KQL for transparency
  │
  └─ Return { answer, citations[], kql? }
```

#### Indexing function pseudo-code

```text
TIMER or HTTP trigger  →  reindex
  ├─ Read all content/patterns/*.json
  ├─ Read copilot-instructions/*.md  (architecture docs)
  ├─ For each document:
  │     ├─ Chunk into ~500-token segments (overlap 100 tokens)
  │     ├─ Embed with text-embedding-3-large  (1536 or 3072 dims)
  │     └─ Upsert into Azure AI Search index
  └─ Log indexing stats
```

### 3.3  Azure AI Search — Vector Index

| Setting | Value |
|---------|-------|
| **Index name** | `modernization-patterns` |
| **Key field** | `id` (composite: `{patternSlug}_{chunkIndex}`) |
| **Vector field** | `contentVector` — `Collection(Edm.Single)`, 3072 dimensions |
| **Algorithm** | HNSW (default) |
| **Text fields** | `title`, `category`, `subcategory`, `content` (searchable) |
| **Filterable** | `category`, `subcategory`, `complexity` |
| **Semantic config** | Enable semantic ranker for hybrid search re-ranking |

#### Index schema (simplified)

```json
{
  "name": "modernization-patterns",
  "fields": [
    { "name": "id",            "type": "Edm.String",  "key": true },
    { "name": "patternSlug",   "type": "Edm.String",  "filterable": true },
    { "name": "title",         "type": "Edm.String",  "searchable": true },
    { "name": "category",      "type": "Edm.String",  "filterable": true, "facetable": true },
    { "name": "subcategory",   "type": "Edm.String",  "filterable": true },
    { "name": "complexity",    "type": "Edm.String",  "filterable": true },
    { "name": "content",       "type": "Edm.String",  "searchable": true },
    { "name": "chunkIndex",    "type": "Edm.Int32" },
    { "name": "contentVector", "type": "Collection(Edm.Single)",
      "searchable": true,
      "vectorSearchDimensions": 3072,
      "vectorSearchProfileName": "default-hnsw" }
  ],
  "vectorSearch": {
    "algorithms": [{ "name": "default-hnsw", "kind": "hnsw" }],
    "profiles":   [{ "name": "default-hnsw", "algorithm": "default-hnsw" }]
  },
  "semantic": {
    "configurations": [{
      "name": "default",
      "prioritizedFields": {
        "titleField": { "fieldName": "title" },
        "contentFields": [{ "fieldName": "content" }]
      }
    }]
  }
}
```

### 3.4  Azure OpenAI Service

| Model | Purpose | Deployment name |
|-------|---------|----------------|
| `gpt-4o` (or `gpt-4.1`) | Chat completions — answer generation, intent classification, KQL generation | `gpt-4o` |
| `text-embedding-3-large` | Embedding queries and pattern content | `text-embedding-3-large` |

- Deploy both models in the **same region** as AI Search for low latency.
- Use **Managed Identity** for auth from Azure Functions to OpenAI.

### 3.5  Azure Log Analytics Integration

| Item | Detail |
|------|--------|
| **Workspace** | The existing RiskInsure Log Analytics workspace (Container Apps, App Service) |
| **Auth** | Azure Functions uses Managed Identity with `Log Analytics Reader` role |
| **API** | Azure Monitor Query REST API (`api.loganalytics.azure.com`) |
| **Tables** | `ContainerAppConsoleLogs_CL`, `AppExceptions`, `AppTraces`, `AppRequests` |
| **Safety** | GPT generates KQL but it runs through a **read-only** API; the function validates the query has no mutations |

#### How "Why did this app fail?" works

1. User asks: *"Why did the billing endpoint fail yesterday?"*
2. GPT-4o generates a KQL query using the table schema in its system prompt:
   ```kql
   AppExceptions
   | where TimeGenerated > ago(1d)
   | where AppRoleName contains "billing"
   | project TimeGenerated, ProblemId, OuterMessage, InnermostMessage
   | order by TimeGenerated desc
   | take 20
   ```
3. The function executes the KQL via the Monitor API.
4. GPT-4o summarises the rows into a human-readable explanation.
5. The raw KQL is included so the user can paste it into Azure Portal for further drill-down.

---

## 4  Data Flow Diagrams

### 4.1  Pattern Q&A (RAG)

```
User question
  │
  ▼
Embed question  ──►  Azure OpenAI Embeddings
  │
  ▼
Hybrid search   ──►  Azure AI Search (vector + keyword + semantic reranker)
  │
  ▼  top-5 chunks
Build prompt    ──►  system prompt + retrieved context + user question
  │
  ▼
Generate answer ──►  Azure OpenAI GPT-4o  ──►  streamed tokens to browser
```

### 4.2  Log Analytics Q&A

```
User question
  │
  ▼
Generate KQL   ──►  Azure OpenAI GPT-4o  (system prompt with table schemas)
  │
  ▼
Execute KQL    ──►  Azure Monitor Query API
  │
  ▼  result rows
Summarise      ──►  Azure OpenAI GPT-4o  ──►  answer + raw KQL to browser
```

---

## 5  Azure Resources Required

| Resource | SKU / Tier | Notes |
|----------|-----------|-------|
| **Azure OpenAI** | Standard S0 | GPT-4o + text-embedding-3-large deployments |
| **Azure AI Search** | Basic (to start) | 1 index, ~50 MB; upgrade to Standard for semantic ranker |
| **Azure Functions** | Consumption or Flex Consumption | Low traffic; cold-start acceptable |
| **Azure Static Web Apps** | Free or Standard | Already exists for the Atlas |
| **Azure Log Analytics** | Existing workspace | No new resource needed |
| **Azure Key Vault** | Standard | Store OpenAI keys (if not using MI) |
| **Managed Identity** | System-assigned on Functions | Roles: `Cognitive Services OpenAI User`, `Search Index Data Reader`, `Log Analytics Reader` |

### Estimated monthly cost (dev/test)

| Resource | Estimate |
|----------|----------|
| Azure OpenAI (low volume) | ~$10–30 |
| Azure AI Search Basic | ~$70 |
| Azure Functions Consumption | ~$0–5 |
| Static Web Apps Standard | ~$9 |
| **Total** | **~$90–115/mo** |

---

## 6  Security & Identity

| Concern | Approach |
|---------|----------|
| **API auth** | Azure Static Web Apps built-in auth (Entra ID / Easy Auth) — only authenticated users can call `/api/chat` |
| **Secrets** | No API keys in code; use **Managed Identity** from Functions → OpenAI, AI Search, Log Analytics |
| **KQL safety** | Generated KQL is validated server-side (reject DML, limit `take` to 100 rows) |
| **PII** | Pattern content has no PII; log queries should redact sensitive columns via column allow-list |
| **Rate limiting** | APIM or SWA proxy rate-limits; OpenAI TPM limits configured on deployment |
| **Prompt injection** | System prompt instructs model to only answer from provided context; conversation history capped |

---

## 7  Indexing Strategy

### What gets indexed

| Content source | Chunk strategy | Metadata |
|---------------|---------------|----------|
| `content/patterns/*.json` (41 files) | One chunk per logical section (summary, guidance, gotchas, example, etc.) | `patternSlug`, `category`, `subcategory`, `complexity` |
| `copilot-instructions/*.md` (19 files) | ~500 token chunks with 100 token overlap | `sourceType: "architecture"` |
| `docs/*.md` | ~500 token chunks | `sourceType: "documentation"` |
| `content/sources/sources.json` | One chunk per source | `sourceType: "reference"` |

### When to re-index

- **CI trigger**: Whenever `content/patterns/**` or `copilot-instructions/**` changes on `main`.
- **Timer fallback**: Daily at midnight UTC via Azure Functions timer trigger.
- **Manual**: HTTP-triggered function for ad-hoc re-index.

---

## 8  Implementation Roadmap

### Phase 1 — Pattern RAG (2–3 weeks)

| Step | Task |
|------|------|
| 1 | Provision Azure OpenAI, AI Search, Functions |
| 2 | Build indexing function (read JSONs → chunk → embed → upsert) |
| 3 | Build chat function (embed question → search → prompt → stream answer) |
| 4 | Build `ChatWidget.jsx` in the React SPA |
| 5 | Wire SWA managed API to Functions |
| 6 | End-to-end test with sample questions |

### Phase 2 — Log Analytics Integration (1–2 weeks)

| Step | Task |
|------|------|
| 7 | Grant Functions MI `Log Analytics Reader` on workspace |
| 8 | Add Log Analytics table schema to system prompt |
| 9 | Implement KQL generation + execution + summarisation logic |
| 10 | Add "Ask about logs" mode toggle in ChatWidget |
| 11 | Validate with real failure scenarios |

### Phase 3 — Polish & Governance (1 week)

| Step | Task |
|------|------|
| 12 | Add conversation history / session management |
| 13 | Add feedback thumbs-up/down (store in Cosmos DB or App Insights) |
| 14 | Harden KQL validation and add guardrails |
| 15 | Add Terraform/Bicep for all new resources |
| 16 | Document runbooks and cost alerts |

---

## 9  Folder Structure (Proposed)

```
platform/modernizationpatterns/
├── src/
│   ├── components/
│   │   └── ChatWidget.jsx          ← new chat UI component
│   ├── hooks/
│   │   └── useChat.js              ← custom hook for streaming chat API
│   ├── routes/
│   │   ├── Home.jsx
│   │   └── Pattern.jsx
│   ├── App.jsx                     ← add ChatWidget here
│   └── ...
├── api/                            ← Azure Functions (SWA managed API)
│   ├── chat/
│   │   ├── index.js                ← chat orchestrator function
│   │   └── function.json
│   ├── reindex/
│   │   ├── index.js                ← indexing function
│   │   └── function.json
│   ├── shared/
│   │   ├── aiSearch.js             ← AI Search client wrapper
│   │   ├── openai.js               ← Azure OpenAI client wrapper
│   │   ├── logAnalytics.js         ← Log Analytics query wrapper
│   │   └── chunker.js              ← text chunking utility
│   ├── host.json
│   ├── local.settings.json         ← local dev secrets (git-ignored)
│   └── package.json
├── content/
│   └── patterns/*.json             ← source of truth
├── infra/
│   ├── main.bicep                  ← IaC for all new resources
│   ├── modules/
│   │   ├── ai-search.bicep
│   │   ├── openai.bicep
│   │   └── functions.bicep
│   └── parameters/
│       ├── dev.bicepparam
│       └── prod.bicepparam
└── docs/
    └── ai-chatbot-architecture.md  ← this document
```

---

## 10  Key Design Decisions

| # | Decision | Rationale |
|---|----------|-----------|
| 1 | **Azure AI Search as vector store** (not Cosmos DB vector or Pinecone) | Native Azure integration, built-in hybrid search + semantic ranker, no extra vendor |
| 2 | **Hybrid search** (vector + keyword + semantic reranker) | Better recall than pure vector; semantic reranker boosts relevance |
| 3 | **SWA managed API** for hosting Functions | Zero additional infra; Functions are co-deployed with the SPA |
| 4 | **Intent classification** before retrieval | Avoids mixing pattern context into log queries and vice versa |
| 5 | **Streaming responses** (SSE) | Better UX — tokens appear as generated rather than waiting for full response |
| 6 | **Managed Identity everywhere** | No keys to rotate; follows RiskInsure security posture |
| 7 | **Generated KQL** with guardrails | Enables natural-language log queries while keeping the system read-only |
| 8 | **Chunking at section level** for patterns | Pattern JSONs have natural sections; respects semantic boundaries |

---

## 11  Sample User Interactions

### Pattern question

> **User**: "When should I use the strangler fig pattern instead of a big-bang rewrite?"
>
> **Bot**: "The **Strangler Fig Migration** pattern is ideal when your legacy platform
> must stay operational during migration and you need incremental releases. Avoid it only
> when the component is small enough to replace outright. The key gotcha: routing becomes
> messy without clear ownership — tie routing to domain slices and measure equivalence
> before expanding cutover.  
> 📖 *Related: Parallel Run & Reconciliation, Feature Flag Release*"

### Log Analytics question

> **User**: "Why did the billing endpoint fail yesterday?"
>
> **Bot**: "Based on Log Analytics, the Billing Endpoint.In container restarted 4 times
> between 2:00–2:15 PM UTC yesterday. The root cause was a `CosmosException: Request
> rate is large (429)` — the container exceeded provisioned RU/s on the `billing`
> container.  
> **Recommendation**: Increase RU/s or enable autoscale on the Cosmos billing container.  
> 🔍 *KQL used*: `ContainerAppConsoleLogs_CL | where TimeGenerated > ago(1d) | where
> ContainerAppName_s contains 'billing' | where Log_s contains 'Exception'`"

---

## 12  References

- [Azure AI Search vector search docs](https://learn.microsoft.com/azure/search/vector-search-overview)
- [Azure OpenAI on your data](https://learn.microsoft.com/azure/ai-services/openai/concepts/use-your-data)
- [Azure Monitor Query API](https://learn.microsoft.com/azure/azure-monitor/logs/api/overview)
- [Azure Static Web Apps managed API](https://learn.microsoft.com/azure/static-web-apps/apis-functions)
- [RAG pattern best practices](https://learn.microsoft.com/azure/search/retrieval-augmented-generation-overview)
