# AI Chatbot Architecture — Modernization Patterns Atlas

> **Goal**: Add a conversational AI assistant to the Modernization Patterns Atlas that can answer questions about any of the 41+ modernization patterns using RAG (Retrieval-Augmented Generation).

---

## 1  High-Level Summary

| Layer | Technology | Purpose |
|-------|-----------|---------|
| **Front-end** | React chat component (existing Vite SPA) | Renders chat UI inside the Atlas |
| **API gateway** | Azure Static Web Apps managed API *or* Azure API Management | Routes `/api/chat` to the back-end |
| **Back-end API** | Azure Container Apps (Node.js / TypeScript) | Orchestrates RAG pipeline; streaming completions |
| **LLM** | Azure OpenAI Service (GPT-4o / GPT-4.1) | Generates natural-language answers |
| **Vector search** | Azure AI Search (vector index) | Stores pattern embeddings for semantic retrieval |
| **Embeddings** | Azure OpenAI `text-embedding-3-large` | Converts patterns & queries into vectors |
| **Indexing pipeline** | Azure Container Apps (timer / CI trigger) | Chunks, embeds, and pushes pattern content to AI Search |

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
│  AZURE CONTAINER APPS — Chat & Reindex Services                              │
│                                                                              │
│  Service 1: Chat Pod (Node.js/TypeScript)                                  │
│  ┌────────────────────────────────────────────────────────────────┐         │
│  │  1. Embed user question → text-embedding-3-large              │         │
│  │  2. Vector search → Azure AI Search (top-5 chunks)            │         │
│  │  3. Build prompt with context + conversation history          │         │
│  │  4. Stream completions via Azure OpenAI GPT-4o                │         │
│  └────────────────────────────────────────────────────────────────┘         │
│                                                                              │
│  Service 2: Reindex Pod (Node.js/TypeScript)                               │
│  ┌────────────────────────────────────────────────────────────────┐         │
│  │  1. Read pattern files from Git or Blob Storage               │         │
│  │  2. Chunk content into ~500-token segments                    │         │
│  │  3. Embed chunks → text-embedding-3-large                    │         │
│  │  4. Upsert into AI Search vector index                        │         │
│  └────────────────────────────────────────────────────────────────┘         │
│                                                                              │
│  Both services: KEDA auto-scale based on HTTP requests                      │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
               │
               ▼  streams / returns JSON answer
┌──────────────────────────────────────────────────────────────────────────────┐
│  Note: Reindex service can be triggered by:                                  │
│  · GitHub webhook (on push to platform/modernizationpatterns/**)            │
│  · Manual HTTP POST /api/reindex                                            │
│  · Timer trigger (nightly backup)                                           │
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
| **UX extras** | Citation links that deep-link to `/pattern/:slug`; token usage indicator |

### 3.2  API Layer — Azure Container Apps

**Two containerized Node.js/TypeScript services**, deployed in Azure Container Apps Environment with KEDA auto-scaling.

#### Chat Service pseudo-code

```text
POST /api/chat
  ├─ Extract user { message, conversationId, userId }
  ├─ Embed message  →  Azure OpenAI text-embedding-3-large
  ├─ Search Azure AI Search index  (top-k = 5, vector + keyword hybrid)
  ├─ Retrieve conversation history from Cosmos DB
  ├─ Build system prompt:
  │     "You are the Modernization Patterns assistant.
  │      Answer using ONLY the provided patterns.
  │      Be concise and cite pattern names when relevant."
  ├─ Messages: [system] + [retrieval context] + [conversation history] + [user query]
  ├─ Call Azure OpenAI GPT-4o with streaming flag
  ├─ Stream response chunks back via HTTP (SSE or chunked encoding)
  └─ Save conversation to Cosmos DB
```

#### Reindex Service pseudo-code

```text
POST /api/reindex  (or triggered by GitHub webhook / timer)
  ├─ Validate admin authorization (API key or webhook secret)
  ├─ Read all content/patterns/*.json  (from Git or Blob Storage)
  ├─ For each pattern file:
  │     ├─ Parse JSON and extract sections
  │     ├─ Chunk into ~500-token segments (overlap 100 tokens)
  │     ├─ Embed each chunk  →  text-embedding-3-large
  │     └─ Upsert into Azure AI Search index with metadata
  ├─ Log indexing stats  (total chunks, total time)
  └─ Return { status: "complete", totalDocuments, totalChunks }
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

### 3.4  Azure Cosmos DB (Conversation Store)

| Item | Detail |
|------|--------|
| **Database** | `modernization-patterns-db` |
| **Container** | `conversations` (partition key: `/userId`) |
| **Document schema** | `{ id, userId, messages: [{ role, content, timestamp }], startedAt, updatedAt, status }` |
| **Purpose** | Store multi-turn conversation history for context and analytics |
| **Retention** | Optional TTL policy (e.g., 90 days) |

### 3.5  Azure OpenAI Service

| Model | Purpose | Deployment name |
|-------|---------|----------------|
| `gpt-4o` (or `gpt-4.1`) | Chat completions — answer generation, intent classification, KQL generation | `gpt-4o` |
| `text-embedding-3-large` | Embedding queries and pattern content | `text-embedding-3-large` |

- Deploy both models in the **same region** as AI Search for low latency.
- Use **Managed Identity** for auth from Container Apps to OpenAI.

### 3.6  Azure Container Registry (ACR)

| Item | Detail |
| **Registry** | Azure Container Registry (ACR) — stores Chat & Reindex container images |
| **Image names** | `acr.azurecr.io/chat:latest`, `acr.azurecr.io/reindex:latest` |
| **Build trigger** | GitHub Actions on push to `platform/modernizationpatterns/**` |

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

### 4.2  Indexing Trigger Flow

```
Git push to platform/modernizationpatterns/**
  │
  ▼
GitHub webhook  ──►  POST https://ca-reindex.azurecontainerapps.io/api/reindex
  │
  ▼
Reindex service reads files from Git + chunks them
  │
  ▼
Embed chunks    ──►  Azure OpenAI text-embedding-3-large
  │
  ▼
Upsert to index ──►  Azure AI Search vector index (updated)
```

---

## 5  Azure Resources Required

| Resource | SKU / Tier | Notes |
|----------|-----------|-------|
| **Azure OpenAI** | Standard S0 | GPT-4o + text-embedding-3-large deployments |
| **Azure AI Search** | Basic (to start) | 1 index, ~50 MB; upgrade to Standard for semantic ranker |
| **Azure Container Apps** | Consumption (pay-per-vCPU) | Chat + Reindex services with KEDA auto-scale |
| **Azure Container Registry** | Basic | Store chat & reindex container images |
| **Azure Cosmos DB** | Serverless | Conversation storage (Cosmos DB for RiskInsure) |
| **Azure Static Web Apps** | Free or Standard | Already exists for the Atlas |
| **Azure Key Vault** | Standard | Store OpenAI keys |
| **Azure Blob Storage** | Standard | Runtime uploads (optional, for user doc
| **Managed Identity** | System-assigned on Container Apps | Roles: `Cognitive Services OpenAI User`, `Search Index Data Reader` |

### Estimated monthly cost (dev/test)

| Resource | Estimate |
|----------|----------|
| Azure OpenAI (low volume) | ~$20–50 |
| Azure AI Search Basic | ~$70 |
| Azure Container Apps (0.5 vCPU, 2 services) | ~$50–100 |
| Azure Container Registry Basic | ~$10 |
| Azure Cosmos DB Serverless | ~$30–50 |
| Static Web Apps Standard | ~$9 |
| **Total** | **~$190–300/mo** |

---

## 6  Security & Identity

| Concern | Approach |
|---------|----------|
| **API auth** | Azure Static Web Apps built-in auth (Entra ID / Easy Auth) — only authenticated users can call `/api/chat` |
| **Secrets** | No API keys in code; use **Managed Identity** from Container Apps → OpenAI, AI Search |
| **Prompt injection** | System prompt constrains model to answer only from retrieved patterns; conversation history capped at 20 messages |
| **PII** | Pattern content has no PII; no operational logs accessed |
| **Rate limiting** | SWA Managed API rate-limits; Container Apps KEDA scales on HTTP queue depth |

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

- **CI trigger**: Whenever `content/patterns/**` or `copilot-instructions/**` changes on `main` (GitHub webhook).
- **Timer fallback**: Daily at midnight UTC via Container Apps timer trigger.
- **Manual**: HTTP POST to `/api/reindex` for ad-hoc re-index.

---

## 8  Implementation Roadmap

### Phase 1 — Core RAG Setup (1 week)

| Step | Task |
|------|------|
| 1 | Provision Azure resources: OpenAI, AI Search, Cosmos DB, Container Registry |
| 2 | Create Container Apps environment with VNet |
| 3 | Build and push Chat service Docker image to ACR |
| 4 | Build and push Reindex service Docker image to ACR |
| 5 | Deploy both services to Container Apps with Managed Identity |
| 6 | Wire SWA managed API to Container Apps endpoints |

### Phase 2 — Frontend & Integration (1 week)

| Step | Task |
|------|------|
| 7 | Build `ChatWidget.jsx` in React SPA with streaming support |
| 8 | Implement conversation history (Cosmos DB) |
| 9 | Add login page (Entra ID) |
| 10 | End-to-end test: chat → embedding → search → streaming response |
| 11 | Deploy SPA to Azure Static Web Apps |

### Phase 3 — Indexing & CI/CD (1 week)

| Step | Task |
|------|------|
| 12 | Create AI Search vector index with proper schema |
| 13 | Run initial reindex: read `content/patterns/*.json` → chunk → embed → index |
| 14 | Set up GitHub Actions workflow (Git push → webhook → reindex service) |
| 15 | Add timer trigger (nightly reindex fallback) |
| 16 | Monitor + document runbooks |

---

## 9  Folder Structure (Proposed)

```
platform/modernizationpatterns/
├── src/                            ← React SPA (frontend)
│   ├── components/
│   │   └── ChatWidget.jsx          ← new chat UI component
│   ├── hooks/
│   │   └── useChat.js              ← custom hook for streaming chat API
│   ├── pages/
│   │   ├── Home.jsx
│   │   ├── Chat.jsx
│   │   └── Pattern.jsx
│   ├── services/
│   │   └── chatApi.js              ← fetch wrapper for /api/chat
│   ├── App.jsx                     ← add ChatWidget here
│   └── ...
├── api/                            ← Container Apps services
│   ├── chat/
│   │   ├── src/
│   │   │   ├── index.js            ← Chat service entrypoint
│   │   │   └── shared/
│   │   │       ├── openai.js       ← Azure OpenAI client wrapper
│   │   │       ├── aiSearch.js     ← AI Search client wrapper
│   │   │       ├── cosmos.js       ← Cosmos DB client
│   │   │       └── auth.js         ← JWT validation
│   │   ├── Dockerfile              ← Chat service container image
│   │   └── package.json
│   ├── reindex/
│   │   ├── src/
│   │   │   ├── index.js            ← Reindex service entrypoint
│   │   │   └── shared/
│   │   │       ├── openai.js
│   │   │       ├── aiSearch.js
│   │   │       ├── chunker.js      ← text chunking utility
│   │   │       └── auth.js         ← admin secret validation
│   │   ├── Dockerfile              ← Reindex service container image
│   │   └── package.json
│   └── docker-compose.yml          ← local development
├── content/
│   └── patterns/*.json             ← source of truth
├── infra/                          ← Infrastructure as Code
│   ├── main.bicep                  ← root IaC template
│   ├── modules/
│   │   ├── container-apps.bicep    ← Container Apps environment
│   │   ├── ai-search.bicep         ← AI Search index
│   │   ├── cosmos.bicep            ← Cosmos DB
│   │   ├── acr.bicep               ← Container Registry
│   │   └── keyvault.bicep          ← Key Vault
│   └── parameters/
│       ├── dev.bicepparam
│       └── prod.bicepparam
├── .github/workflows/
│   └── chatbot-build-deploy.yml    ← CI/CD: build Docker images, deploy
└── docs/
    └── ai-chatbot-architecture.md  ← this document
```

---

## 10  Key Design Decisions

| # | Decision | Rationale |
|---|----------|-----------|
| 1 | **Azure AI Search as vector store** (not Cosmos DB vector or Pinecone) | Native Azure integration, built-in hybrid search + semantic ranker, no extra vendor |
| 2 | **Hybrid search** (vector + keyword + semantic reranker) | Better recall than pure vector; semantic reranker boosts relevance |
| 3 | **SWA Managed API + Container Apps** | SWA provides auth; Container Apps offers serverless containers, KEDA auto-scale, streaming |
| 4 | **Streaming responses** (HTTP chunked / SSE) | Better UX — tokens appear as generated; avoids timeout on large responses |
| 5 | **Managed Identity everywhere** | No keys in code/environment; follows RiskInsure security posture |
| 6 | **Cosmos DB for conversations** | Fast, globally distributed, built-in TTL for retention policies |
| 7 | **GitHub webhook → reindex** | Automatic indexing on code changes; GitOps pattern for content updates |
| 8 | **Chunking at section level** for patterns | Pattern JSONs have natural sections; respects semantic boundaries |

---

## 11  Sample User Interactions

### Pattern Q&A Example

> **User**: "When should I use the Strangler Fig pattern instead of a big-bang rewrite?"
>
> **Bot**: 
> The **Strangler Fig Migration** pattern is ideal when your legacy platform must stay operational during migration and you need incremental feature rollout.
>
> **Key benefits**:
> - Minimizes downtime; parallel old & new systems
> - Allows incremental validation and user feedback
> - Risk is lower because a single feature can be rolled back
>
> **Avoid if**:
> - The component is small enough to replace outright in a single release window
> - Your team lacks bandwidth for ongoing dual-system maintenance
>
> **Key gotcha**: Routing logic becomes complex without clear domain boundaries. Solution: tie routing decisions to domain slices, implement feature flags, and measure payloads/behavior equivalence before expanding cutover.
>
> **Related patterns**: Parallel Run & Reconciliation, Feature Flag Release, Anti-Corruption Layer
>

---

## 12  User Flow Scenarios

### Scenario 1: Internal User — Pattern Question (Most Common)

**Actor**: RiskInsure employee (authenticated via Entra ID)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ STEP 1: User visits Modernization Patterns Atlas                           │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Browser: https://atlas.riskinsure.com/patterns                           │
│           ↓                                                                 │
│  Azure Static Web Apps (SPA loaded)                                        │
│           ↓                                                                 │
│  React app renders with ChatWidget in bottom-right corner                 │
│           ↓                                                                 │
│  User is already authenticated via Entra ID (seamless SSO)                │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│ STEP 2: User Clicks ChatWidget and Asks a Question                        │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ChatWidget Input:                                                         │
│  ┌────────────────────────────────────────┐                              │
│  │ "When should we use the strangler      │                              │
│  │ pattern vs. replatforming?"            │                              │
│  └────────────────────────────────────────┘                              │
│           │                                                                │
│           ▼                                                                │
│  SWA intercepts request, validates JWT token from Entra ID                │
│  (via managed auth middleware)                                            │
│           │                                                                │
│           ▼  [Route allowed ✓]                                             │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│ STEP 3: Request Hits Chat Service (Container Apps)                        │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  POST https://ca-chat-prod.azurecontainerapps.io/api/chat                │
│  {                                                                         │
│    "message": "When should we use the strangler pattern...",             │
│    "conversationId": "conv-12345",                                       │
│    "userId": "user@riskinsure.com"                                       │
│  }                                                                         │
│           │                                                                │
│           ▼  Chat Pod (Node.js/TypeScript)                                │
│                                                                             │
│  1. Validate JWT token (from SWA middleware)                              │
│  2. Extract message & extract embeddings                                  │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│ STEP 4: Embedding & Search (1-2 seconds)                                   │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Chat Service:                                                            │
│  ┌─────────────────────────────────────────────────────────┐             │
│  │ Call Azure OpenAI text-embedding-3-large               │             │
│  │ Input: "When should we use the strangler pattern..."   │             │
│  │ Output: 1536-dim vector [0.23, -0.45, 0.12, ...]       │             │
│  └──────────────────┬──────────────────────────────────────┘             │
│                     ▼                                                      │
│  ┌─────────────────────────────────────────────────────────┐             │
│  │ Query Azure AI Search                                  │             │
│  │ • Vector search (top-5 chunks)                         │             │
│  │ • Keyword search ("strangler", "pattern")              │             │
│  │ • Semantic re-ranking                                 │             │
│  │                                                        │             │
│  │ Returns:                                               │             │
│  │ [1] Strangler Fig Migration (relevance: 0.92)         │             │
│  │ [2] Parallel Run (relevance: 0.81)                    │             │
│  │ [3] Feature Flags (relevance: 0.78)                   │             │
│  │ [4] Anti-Corruption Layer (relevance: 0.75)           │             │
│  │ [5] Big Bang Rewrite (relevance: 0.73)                │             │
│  └─────────────────────────────────────────────────────────┘             │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│ STEP 5: Build Prompt & Stream Completion (2-5 seconds)                    │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Chat Service builds system prompt:                                       │
│  ┌─────────────────────────────────────────────────────────┐             │
│  │ SYSTEM:                                                │             │
│  │ "You are the Modernization Patterns assistant.         │             │
│  │  Answer using ONLY the patterns provided below.        │             │
│  │  Be concise and cite pattern names.                   │             │
│  │                                                        │             │
│  │  Available patterns:                                  │             │
│  │  1. [Strangler Fig content...]                       │             │
│  │  2. [Parallel Run content...]                        │             │
│  │  3. [Feature Flags content...]                       │             │
│  │  4. [Anti-Corruption Layer content...]               │             │
│  │  5. [Big Bang Rewrite content...]"                   │             │
│  │                                                        │             │
│  │ PREVIOUS MESSAGES: (if multi-turn)                    │             │
│  │ User: "What is X?"                                   │             │
│  │ Assistant: "X is Y because..."                        │             │
│  │                                                        │             │
│  │ USER:                                                 │             │
│  │ "When should we use strangler pattern vs replatforming?" │             │
│  └─────────────────────────────────────────────────────────┘             │
│           │                                                                │
│           ▼  Call Azure OpenAI GPT-4o (streaming)                         │
│                                                                             │
│  Response tokens stream back:                                             │
│  "The Strangler Fig Migration..."                                         │
│  "...is ideal when your legacy..."                                        │
│  "...must stay operational..."                                            │
│  [tokens continue streaming]                                              │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│ STEP 6: Stream Response to Browser (Real-time)                            │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  HTTP Response (Server-Sent Events or chunked encoding):                 │
│  ┌─────────────────────────────────────────────────────────┐             │
│  │ Content-Type: text/event-stream                        │             │
│  │ Cache-Control: no-cache                                │
│  │                                                        │             │
│  │ data: {"delta": "The"}                                │             │
│  │ data: {"delta": " Strangler"}                         │             │
│  │ data: {"delta": " Fig"}                               │             │
│  │ data: {"delta": " Migration"}                         │             │
│  │ data: {"delta": "..."}                                │             │
│  │ ...                                                    │             │
│  │ data: [DONE]                                          │             │
│  └─────────────────────────────────────────────────────────┘             │
│           │                                                                │
│           ▼  ChatWidget receives tokens                                   │
│                                                                             │
│  React state updates in real-time:                                        │
│  ┌────────────────────────────────────────┐                             │
│  │ Bot: "The Strangler Fig Migration...   │                             │
│  │      is ideal when your legacy...      │                             │
│  │      must stay operational..."         │                             │
│  │                                        │                             │
│  │ [Typing animation continues...]        │                             │
│  └────────────────────────────────────────┘                             │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│ STEP 7: Save Conversation & Show Citations                                │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Chat Service (after streaming completes):                                │
│  ┌─────────────────────────────────────────────────────────┐             │
│  │ Save to Cosmos DB:                                     │             │
│  │ {                                                      │             │
│  │   "id": "conv-12345",                                │             │
│  │   "userId": "user@riskinsure.com",                  │             │
│  │   "messages": [                                      │             │
│  │     {                                                │             │
│  │       "role": "user",                              │             │
│  │       "content": "When should we use...",          │             │
│  │       "timestamp": "2026-02-25T10:30:00Z"         │             │
│  │     },                                             │             │
│  │     {                                                │             │
│  │       "role": "assistant",                        │             │
│  │       "content": "The Strangler Fig Migration...", │             │
│  │       "tokensUsed": 287,                          │             │
│  │       "timestamp": "2026-02-25T10:30:05Z"        │             │
│  │     }                                              │             │
│  │   ]                                                 │             │
│  │ }                                                      │             │
│  └─────────────────────────────────────────────────────────┘             │
│           │                                                                │
│           ▼  ChatWidget displays final response with citations:           │
│                                                                             │
│  ┌────────────────────────────────────────┐                             │
│  │ Bot: "The Strangler Fig Migration is   │                             │
│  │ ideal when your legacy platform must   │                             │
│  │ stay operational...                    │                             │
│  │                                        │                             │
│  │ 📖 See also:                           │                             │
│  │ • [Parallel Run]                       │  ← clickable links          │
│  │ • [Feature Flags]                      │                             │
│  │ • [Anti-Corruption Layer]              │                             │
│  │                                        │                             │
│  │ Tokens used: 287 | Total: 542         │                             │
│  └────────────────────────────────────────┘                             │
│           │                                                                │
│           ▼  User can continue multi-turn conversation...                 │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

**Total latency**: ~5–8 seconds (embedding 1-2s + search 0.5s + LLM streaming 2-5s)

---

### Scenario 2: Admin/Developer — Trigger Reindex (After Code Change)

**Actor**: DevOps engineer or pattern content owner

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ STEP 1: Developer Commits Pattern Update to main                          │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Developer edits JSON:                                                    │
│  $ git add content/patterns/strangler-fig.json                            │
│  $ git commit -m "Update Strangler Fig pattern with new example"          │
│  $ git push origin main                                                   │
│           │                                                                │
│           ▼  Pushed to main branch                                         │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│ STEP 2: GitHub Webhook Triggered                                          │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  GitHub Webhook Configuration:                                           │
│  • Event: push                                                            │
│  • Branch: main                                                           │
│  • Paths: platform/modernizationpatterns/**                             │
│  • Endpoint: https://ca-reindex-prod.azurecontainerapps.io/api/reindex  │
│           │                                                                │
│           ▼  GitHub fires webhook                                          │
│                                                                             │
│  POST /api/reindex                                                       │
│  Headers: X-Hub-Signature-256: sha256=abc123...  ← verified by service   │
│  Body: { "pusher": {...}, "files": [...] }                              │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│ STEP 3: Reindex Service Validates & Starts Reindexing                     │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Reindex Pod (Node.js/TypeScript):                                        │
│  ┌─────────────────────────────────────────────────────────┐             │
│  │ 1. Validate webhook signature                         │             │
│  │    → Signature matches stored WEBHOOK_SECRET ✓        │             │
│  │                                                        │             │
│  │ 2. Extract changed files from webhook payload         │             │
│  │    → file: content/patterns/strangler-fig.json        │             │
│  │                                                        │             │
│  │ 3. Clone/fetch latest repo content (or read from      │             │
│  │    mounted volume if local)                           │             │
│  │                                                        │             │
│  │ 4. Parse strangler-fig.json                           │             │
│  │    {                                                  │             │
│  │      "name": "Strangler Fig Migration",              │             │
│  │      "category": "Decomposition",                    │             │
│  │      "content": "...",                              │             │
│  │      "example": "..."                                │             │
│  │    }                                                  │             │
│  └─────────────────────────────────────────────────────────┘             │
│           │                                                                │
│           ▼  Log status                                                   │
│  "Started reindex for strangler-fig.json"                                 │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│ STEP 4: Chunk, Embed, & Upsert (30-60 seconds)                            │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  For each pattern file:                                                   │
│                                                                             │
│  A. Chunk content into ~500-token segments                               │
│     ┌──────────────────────────────────────────┐                         │
│     │ Chunk 1: "Strangler Fig is a pattern..." │  ~450 tokens            │
│     │ Chunk 2: "Key benefits: ..."            │  ~480 tokens            │
│     │ Chunk 3: "Avoid if: ..."                │  ~420 tokens            │
│     │ Chunk 4: "Example: BigCorp bank..."     │  ~510 tokens            │
│     └──────────────────────────────────────────┘                         │
│           │                                                                │
│           ▼  B. Embed each chunk                                          │
│                                                                             │
│     Call Azure OpenAI text-embedding-3-large (batch API for efficiency) │
│     ┌──────────────────────────────────────────┐                         │
│     │ Chunk 1 → [0.23, -0.45, 0.12, ...] (1536 dims) │                  │
│     │ Chunk 2 → [0.11, -0.33, 0.22, ...] (1536 dims) │                  │
│     │ Chunk 3 → [0.19, -0.41, 0.08, ...] (1536 dims) │                  │
│     │ Chunk 4 → [0.28, -0.47, 0.15, ...] (1536 dims) │                  │
│     └──────────────────────────────────────────┘                         │
│           │                                                                │
│           ▼  C. Upsert to AI Search                                       │
│                                                                             │
│     POST /indexes/modernization-patterns/docs                            │
│     [                                                                    │
│       {                                                                 │
│         "id": "strangler-fig-0",                                        │
│         "patternSlug": "strangler-fig",                                 │
│         "title": "Strangler Fig Migration",                            │
│         "category": "Decomposition",                                   │
│         "content": "Strangler Fig is a pattern...",                    │
│         "contentVector": [0.23, -0.45, 0.12, ...],                    │
│         "chunkIndex": 0                                                │
│       },                                                               │
│       {                                                                │
│         "id": "strangler-fig-1",                                       │
│         "patternSlug": "strangler-fig",                               │
│         "title": "Strangler Fig Migration",                           │
│         "category": "Decomposition",                                  │
│         "content": "Key benefits: ...",                               │
│         "contentVector": [0.11, -0.33, 0.22, ...],                   │
│         "chunkIndex": 1                                               │
│       },                                                              │
│       ...                                                             │
│     ]                                                                 │
│           │                                                                │
│           ▼  AI Search indexes documents (existing chunks replaced)       │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│ STEP 5: Log Completion & Notify (Optional)                                │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Reindex Service logs:                                                    │
│  ┌─────────────────────────────────────────────────────────┐             │
│  │ INFO: Reindex complete                                 │             │
│  │ - Files processed: 1                                   │             │
│  │ - Chunks created: 4                                    │             │
│  │ - Chunks embedded: 4                                   │             │
│  │ - Chunks upserted: 4                                   │             │
│  │ - Duration: 45 seconds                                 │             │
│  │ - Next query will use updated pattern                 │             │
│  └─────────────────────────────────────────────────────────┘             │
│           │                                                                │
│           ▼  Optional: Slack notification                                │
│                                                                             │
│  Slack #reindex-logs:                                                    │
│  ✅ Reindex successful (45s)                                             │
│     Files: 1 | Chunks: 4 | Branch: main                                 │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│ STEP 6: Users See Updated Content (Immediately)                           │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Next user query about Strangler Fig will:                                │
│  • Find the newly indexed content                                        │
│  • Include the latest example/gotcha updates                             │
│  • Show most relevant chunks                                             │
│                                                                             │
│  User (1 min later):                                                     │
│  "Show me an example of the strangler pattern"                           │
│           │                                                                │
│           ▼  Chat Service searches AI Search                              │
│              → Returns fresh Chunk 4 (newly indexed!)                     │
│              → LLM synthesizes response with latest example              │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

**Total reindex time**: ~30–60 seconds (depends on file count & AI Search latency)  
**Availability**: Zero downtime — search queries continue while reindex happens

---

### Scenario 3: Nightly Timer Fallback (Scheduled Reindex)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ EVERY DAY AT 00:00 UTC                                                      │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Container Apps invokes Reindex Pod:  (via internal timer trigger)        │
│  ┌─────────────────────────────────────────────────────────────────┐      │
│  │ POST /api/reindex/scheduled                                    │      │
│  │ Authorization: Bearer {admin-token}                            │      │
│  │                                                                  │      │
│  │ Purpose: Catch any missed webhooks or manual edits             │      │
│  └─────────────────────────────────────────────────────────────────┘      │
│           │                                                                  │
│           ▼  Reindex Service                                                 │
│  • Reads ALL files in content/patterns/**                                 │
│  • Compares checksums with last-indexed version                          │
│  • Reindexes only changed files                                          │
│  • Logs summary                                                           │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

### Scenario 4: Manual Trigger (Admin UI or CLI)

**Option A: Admin calls reindex manually**

```bash
# Authenticate as admin with service principal
az login --service-principal ...

# Trigger reindex
curl -X POST \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  https://ca-reindex-prod.azurecontainerapps.io/api/reindex \
  -H "Content-Type: application/json" \
  -d '{"force": true}'

# Response
{
  "status": "complete",
  "totalDocuments": 41,
  "totalChunks": 164,
  "duration": "42 seconds"
}
```

---

### Summary: Request/Response Timeline

| Step | Component | Duration | Status |
|------|-----------|----------|--------|
| 1. User types question | ChatWidget | — | 🔵 User action |
| 2. SWA validates auth | Static Web Apps | <50ms | 🔵 Instant |
| 3. Embed question | Azure OpenAI | 1–2s | 🟡 API call |
| 4. Search vector DB | AI Search | 0.5–1s | 🟡 API call |
| 5. Build prompt + context | Chat Service | <100ms | 🟢 In-process |
| 6. Stream LLM response | Azure OpenAI | 2–5s | 🟡 Streaming |
| 7. Display in ChatWidget | React | Real-time | 🟢 Streaming |
| 8. Save conversation | Cosmos DB | 0.5–1s | 🟡 Background |
| **Total end-to-end** | — | **5–9s** | ✅ Complete |

---

### Access Control & Permissions Matrix

| Action | Internal User | Admin/Dev | Public (Future) |
|--------|--------------|-----------|-----------------|
| Ask questions | ✅ Yes (Entra ID) | ✅ Yes | ⚠️ With APIM + quota |
| View conversation history | ✅ Own only | ✅ Yes | — |
| Trigger manual reindex | ❌ No | ✅ Yes (with API key) | ❌ No |
| View index stats | ❌ No | ✅ Via logs | ❌ No |
| Delete conversation | ✅ Own only | ✅ Any | — |
| Export data | ❌ No | ✅ Yes | — |

---

## 13  References

- [Azure AI Search vector search docs](https://learn.microsoft.com/azure/search/vector-search-overview)
- [Azure OpenAI streaming completions](https://learn.microsoft.com/azure/ai-services/openai/how-to/chat-completions)
- [Azure Container Apps](https://learn.microsoft.com/azure/container-apps/)
- [Azure Static Web Apps managed API](https://learn.microsoft.com/azure/static-web-apps/apis-functions)
- [RAG pattern best practices](https://learn.microsoft.com/azure/search/retrieval-augmented-generation-overview)
