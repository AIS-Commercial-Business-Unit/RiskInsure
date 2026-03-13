# Modernization Patterns Chatbot - Technical Runbook

## 1. Scope and purpose

This runbook documents the current production-style architecture and implementation details of the Modernization Patterns chatbot in this repository.

It covers:
- Runtime architecture
- Service boundaries
- API contracts and event stream behavior
- Indexing pipeline internals
- Data models and storage
- Deployment and CI/CD workflows
- Operations, diagnostics, and known gaps

## 2. System architecture

### 2.1 Runtime components

- Frontend SPA (React + Vite)
- Chat API service (.NET 10, ASP.NET Core)
- Reindex API service (.NET 10, ASP.NET Core)
- Azure OpenAI (chat + embedding deployments)
- Azure AI Search (vector index)
- Azure Cosmos DB (conversation state)
- GitHub Actions workflows for content validation, reindex trigger, and site deployment

### 2.2 Service boundaries

- Frontend handles UX and streaming render only.
- Chat API handles RAG orchestration and conversation persistence.
- Reindex API handles ingestion, chunking, embedding, and index writes.

These boundaries are intentional so user-facing latency is not impacted by batch indexing workloads.

## 3. Repository implementation map

### 3.1 Frontend

- App + route registration: platform/modernizationpatterns/src/App.jsx
- Widget shell: platform/modernizationpatterns/src/components/ChatWidget/ChatWidget.jsx
- State and API integration: platform/modernizationpatterns/src/components/ChatWidget/useChatWidget.js
- Chat window and controls: platform/modernizationpatterns/src/components/ChatWidget/ChatWindow.jsx
- Sidebar conversation list: platform/modernizationpatterns/src/components/ChatWidget/Sidebar.jsx
- Conversation row rendering: platform/modernizationpatterns/src/components/ChatWidget/ConversationItem.jsx

### 3.2 Chat API

- Startup and DI: platform/modernizationpatterns/Api/chat/Program.cs
- Controller and endpoints: platform/modernizationpatterns/Api/chat/src/Controllers/ChatController.cs
- OpenAI integration: platform/modernizationpatterns/Api/chat/src/Services/OpenAiService.cs
- Search integration: platform/modernizationpatterns/Api/chat/src/Services/SearchService.cs
- Conversation persistence: platform/modernizationpatterns/Api/chat/src/Services/ConversationService.cs
- Models: platform/modernizationpatterns/Api/chat/src/Models/Conversation.cs

### 3.3 Reindex API

- Startup and DI: platform/modernizationpatterns/Api/reindex/Program.cs
- Reindex orchestration: platform/modernizationpatterns/Api/reindex/src/Controllers/ReindexController.cs
- Chunking and chunk IDs: platform/modernizationpatterns/Api/reindex/src/Services/ChunkingService.cs
- Embedding generation: platform/modernizationpatterns/Api/reindex/src/Services/EmbeddingService.cs
- Search schema and writes: platform/modernizationpatterns/Api/reindex/src/Services/IndexingService.cs

### 3.4 Workflows

- Reindex on content changes: .github/workflows/modernizationpatterns-reindex.yml
- Static site deployment: .github/workflows/modernizationpatterns-static-webapp.yml
- Content validation on PR: .github/workflows/modernizationpatterns-content-validate.yml

## 4. Chat API deep dive

### 4.1 Startup behavior

File: platform/modernizationpatterns/Api/chat/Program.cs

Key points:
- Serilog bootstrap logger + request logging enabled.
- Optional Application Insights sink if connection string exists.
- CORS policy AllowSWA allows any origin/method/header.
- Registers:
  - IOpenAiService -> OpenAiService
  - ISearchService -> SearchService
  - IConversationService -> ConversationService
- Health probes:
  - GET /health
  - GET /health/ready (checks core service registration)

### 4.2 Endpoint contract summary

File: platform/modernizationpatterns/Api/chat/src/Controllers/ChatController.cs

Implemented endpoints:
1. POST /api/chat/stream
2. GET /api/chat/{conversationId}?userId={userId}
3. POST /api/chat/new?userId={userId}
4. GET /api/chat/user/{userId}/conversations
5. DELETE /api/chat/{conversationId}?userId={userId}

Notes:
- /stream uses text/event-stream and writes SSE events.
- /new currently returns a generated conversation id and does not immediately persist.
- /user/{userId}/conversations currently returns an empty list placeholder.

### 4.3 RAG pipeline execution in /stream

Execution stages in order:
1. Input validation (message, userId)
2. SSE response setup
3. Query embedding attempt via OpenAI embedding deployment
4. Search retrieval from Azure AI Search (topK = 5)
5. Conversation history fetch from Cosmos DB
6. Prompt construction from system template + retrieved context
7. Completion generation from chat deployment
8. Simulated token streaming by chunking completion string
9. Citation metadata event emission
10. Conversation upsert to Cosmos DB
11. done event emission

SSE event types emitted:
- embedding_complete
- embedding_skipped
- search_complete
- token
- completion_done
- response_metadata
- done
- error

### 4.4 Prompt handling

Prompt template source:
- platform/modernizationpatterns/Api/chat/src/prompts/system-prompt.txt

Fallback prompt is used if file is not found/readable.

Reference material injection:
- Up to 3 retrieved search results are inserted into the prompt via {REFERENCE_MATERIAL} replacement.

### 4.5 OpenAI service details

File: platform/modernizationpatterns/Api/chat/src/Services/OpenAiService.cs

Configuration keys:
- AzureOpenAI:Endpoint
- AzureOpenAI:ApiKey
- AzureOpenAI:ChatDeploymentName (default gpt-4.1)
- AzureOpenAI:EmbeddingDeploymentName (default text-embedding-3-small)

API usage:
- Embeddings endpoint: /embeddings?api-version=2024-06-01
- Chat completions endpoint: /chat/completions?api-version=2024-06-01

Completion behavior:
- history tail window: last 6 messages
- temperature: 0.2
- max_tokens: 800

### 4.6 Search service details

File: platform/modernizationpatterns/Api/chat/src/Services/SearchService.cs

Configuration keys:
- AzureSearch:Endpoint
- AzureSearch:ApiKey
- AzureSearch:IndexName (default modernization-patterns)

Current search payload:
- search = query
- top = topK
- count = true
- select = id,patternSlug,title,category,content

Important note:
- Method accepts embeddingVector argument but current request payload is text search only. Hybrid/vector payload can be added later.

### 4.7 Conversation persistence details

File: platform/modernizationpatterns/Api/chat/src/Services/ConversationService.cs

Store:
- Cosmos DB database: modernization-patterns-db (default)
- Container: conversations
- Partition key: /userId

Behavior:
- Uses custom System.Text.Json serializer for Cosmos SDK compatibility.
- Creates database/container idempotently at startup.
- Includes emulator SSL bypass when using localhost emulator endpoint.
- If init fails, service degrades gracefully and skips persistence writes.

Model:
- Conversation includes id, userId, messages, createdAt, updatedAt, status, ttl.
- Message includes role, content, timestamp, tokensUsed.

## 5. Reindex API deep dive

### 5.1 Startup behavior

File: platform/modernizationpatterns/Api/reindex/Program.cs

Registers singleton services:
- IChunkingService -> ChunkingService
- IEmbeddingService -> EmbeddingService
- IIndexingService -> IndexingService

Health probes:
- GET /health
- GET /health/ready

### 5.2 Endpoint contract summary

File: platform/modernizationpatterns/Api/reindex/src/Controllers/ReindexController.cs

Implemented endpoints:
1. POST /api/reindex?clean={bool}&pattern={slug?}
2. GET /api/reindex/status
3. POST /api/reindex/single/{slug}

Output includes:
- patternsProcessed
- inboxDocumentsProcessed
- chunksCreated
- documentsUploaded
- documentsDeleted
- totalDocumentsInIndex
- elapsedSeconds
- patterns
- inboxDocuments

### 5.3 Ingestion source resolution

Path resolution supports multiple execution contexts:
- Config-driven paths first
- Relative candidate paths from current directory
- Absolute fallback path for common local setup

Source folders:
- Patterns: content/patterns
- Inbox: content/_inbox

Supported inbox extensions:
- .json
- .md
- .markdown
- .txt
- .docx
- .pdf

### 5.4 Text extraction

Extraction methods in controller:
- json/md/markdown/txt -> File.ReadAllTextAsync
- docx -> DocumentFormat.OpenXml.WordprocessingDocument
- pdf -> UglyToad.PdfPig

### 5.5 Chunking strategy

File: platform/modernizationpatterns/Api/reindex/src/Services/ChunkingService.cs

Pattern chunking:
- Structured chunks by semantic section:
  - overview
  - diagram
  - implementation
  - complexity
  - example
  - related
  - guidance (if long guidance split)

Inbox chunking:
- Generic sentence-based chunking
- Target chunk size: 500 tokens (estimated)
- Overlap: 100 tokens
- Category set to inbox
- Chunk type formatted as inbox-{sourceType}

Token estimate heuristic:
- ~4 chars per token

### 5.6 Safe chunk ID generation

Method: ToSafeChunkId

Rules enforced:
- Lowercase normalization
- Allowed characters only: a-z, 0-9, _, -, =
- Invalid chars replaced with _
- Repeated underscores collapsed
- Empty result fallback: chunk
- Max-length guard:
  - If >120 chars, truncate and append 8-byte SHA256 hash suffix

This prevents Azure Search invalid document key failures.

### 5.7 Embedding generation details

File: platform/modernizationpatterns/Api/reindex/src/Services/EmbeddingService.cs

Behavior:
- Batch embeddings with batch size 16
- 200ms delay between batches for rate limiting buffer
- Uses Azure OpenAI embeddings endpoint directly

Deployment default:
- text-embedding-3-small

### 5.8 Index schema and upload details

File: platform/modernizationpatterns/Api/reindex/src/Services/IndexingService.cs

Index fields:
- id (key)
- patternSlug
- title
- category
- chunkType
- chunkIndex
- content
- contentVector

Vector config:
- Dimensions: 1536
- HNSW profile: vector-profile
- Metric: cosine

Upload method:
- MergeOrUploadDocumentsAsync
- Batch size: 100

Clean mode behavior:
- Searches all ids and deletes all documents before reindex

## 6. Frontend deep dive

### 6.1 Widget lifecycle and state

File: platform/modernizationpatterns/src/components/ChatWidget/useChatWidget.js

State includes:
- isOpen
- isExpanded
- isSidebarOpen
- conversationId
- messages
- inputValue
- isLoading
- userId
- conversations
- theme

Behavior highlights:
- userId generated on mount as user-{timestamp}
- Theme persisted in localStorage
- Scroll-to-bottom on message updates
- Expand mode locks page body scroll

### 6.2 API endpoint selection

API base logic:
- localhost -> http://localhost:5000/api/chat
- non-localhost -> Azure Container App URL

### 6.3 Streaming parser behavior

- Uses fetch + ReadableStream reader
- Parses SSE lines event-by-event
- Appends token data to assistant message content
- Extracts citations from response_metadata event line
- Marks assistant message as non-streaming on completion

### 6.4 Conversation sidebar behavior

Files:
- platform/modernizationpatterns/src/components/ChatWidget/Sidebar.jsx
- platform/modernizationpatterns/src/components/ChatWidget/ConversationItem.jsx

Current preview mapping in Sidebar:
- conv.messages?.[conv.messages.length - 1]?.message

Important mismatch:
- Chat model message property is content, not message
- So preview can fall back to New conversation

Recommended correction:
- Use conv.messages?.[conv.messages.length - 1]?.content

Also ensure conversation list state is synchronized after send/load operations if preview should update reliably without reload.

## 7. CI/CD and workflows

### 7.1 Reindex workflow

File: .github/workflows/modernizationpatterns-reindex.yml

Triggers:
- workflow_dispatch
- push to main for:
  - platform/modernizationpatterns/content/_inbox/**
  - platform/modernizationpatterns/content/patterns/**

Workflow jobs:
1. detect-changes
2. trigger-reindex
3. verify-index

Reindex strategy logic:
- If deleted files detected -> full clean reindex
- If only added/modified files -> clean=false flow

Resilience behavior:
- Cold-start health probing loop (up to 18 attempts)
- Retry on transient HTTP statuses (404/502/503/504/000)
- Response body and status captured separately for robust parsing

Summary outputs include:
- patterns indexed
- inbox documents indexed
- extension-wise counts (.pdf, .docx, .md/.markdown, .txt, .json)

### 7.2 Static web app deploy workflow

File: .github/workflows/modernizationpatterns-static-webapp.yml

Triggers on main for UI/content paths and deploys app_location platform/modernizationpatterns to dist.

### 7.3 Content validation workflow

File: .github/workflows/modernizationpatterns-content-validate.yml

On PR:
- Runs npm validate-content script
- Validates JSON syntax with jq
- Ensures required fields exist
- Comments summary back to PR

## 8. Configuration and runtime requirements

### 8.1 Chat API config

Expected keys in development/production config:
- ConnectionStrings:CosmosDb
- CosmosDb:DatabaseName
- AzureOpenAI:Endpoint
- AzureOpenAI:ApiKey
- AzureOpenAI:ChatDeploymentName
- AzureOpenAI:EmbeddingDeploymentName
- AzureSearch:Endpoint
- AzureSearch:ApiKey
- AzureSearch:IndexName

### 8.2 Reindex API config

Expected keys:
- AzureOpenAI:Endpoint
- AzureOpenAI:ApiKey
- AzureOpenAI:EmbeddingDeploymentName
- AzureSearch:Endpoint
- AzureSearch:ApiKey
- AzureSearch:IndexName
- Reindex:PatternsPath (optional)
- Reindex:InboxPath (optional)

### 8.3 Local run references

- platform/modernizationpatterns/RUNNING-LOCALLY.md
- platform/modernizationpatterns/Api/chat/API.md

## 9. Operations and diagnostics

### 9.1 Health checks

Chat API:
- GET /health
- GET /health/ready

Reindex API:
- GET /health
- GET /health/ready

### 9.2 Typical runbook checks

1. Verify chat health endpoint.
2. Verify reindex health endpoint.
3. Check Azure Search index document count.
4. Trigger reindex status endpoint.
5. Send smoke chat request and validate SSE events.
6. Verify Cosmos writes for conversation record.

### 9.3 Common failure classes

1. Azure OpenAI deployment mismatch
- Symptoms: 404 deployment not found, failed embeddings/completions
- Fix: align deployment names in config and infra defaults

2. Invalid Azure Search document keys
- Symptoms: upload failures with key format/length errors
- Fix: enforce safe chunk id generation

3. Reindex workflow false negatives
- Symptoms: HTTP 200 but job fails parsing output
- Fix: separate body capture from status code capture

4. Sidebar preview always New conversation
- Symptoms: no recent question displayed
- Fix: use content property and sync conversations state

## 10. Performance notes

- Embedding batch size in reindex set to 16.
- Index upload batch size set to 100.
- Chunking target around 500 tokens with overlap 100.
- Chat completion history capped to last 6 messages for prompt size control.

## 11. Security considerations

Current design uses key-based service access via configuration.

Recommended hardening path:
- Migrate to managed identity where possible.
- Restrict CORS policy by origin in production.
- Protect reindex trigger endpoint with explicit authorization.
- Add request throttling and abuse controls for chat endpoint.
- Add audit logging for reindex trigger origin and actor.

## 12. Testing strategy suggestions

### 12.1 Unit-level

- Chunking logic and token overlap behavior
- Safe chunk id generation constraints
- Search payload construction
- Prompt assembly fallback behavior

### 12.2 Integration-level

- Reindex end-to-end against test index
- Chat RAG flow with deterministic test fixtures
- Cosmos conversation upsert/read lifecycle

### 12.3 Frontend-level

- SSE parser line handling and event transitions
- Sidebar preview rendering from latest message content
- Theme and expansion UX behavior

## 13. Known implementation gaps

1. Conversation listing endpoint placeholder
- GET user conversations currently returns empty list intentionally.

2. Search service vector parameter not yet applied
- embeddingVector argument accepted but not used in request payload.

3. Sidebar preview property mismatch
- Sidebar reads .message but stored message field is .content.

4. Conversation preview sync
- conversations state creation/update path can miss latest assistant/user content unless explicitly synchronized.

## 14. Suggested next engineering steps

1. Implement real conversation listing query from Cosmos DB.
2. Enable hybrid/vector query payload in SearchService using embeddingVector.
3. Fix Sidebar preview mapping to content field and update conversations state on send/load.
4. Add auth guard on reindex endpoint.
5. Add targeted tests for chunk IDs, SSE parsing, and conversation preview behavior.

## 15. Appendix A: quick endpoint reference

### Chat API
- POST /api/chat/new?userId=...
- POST /api/chat/stream
- GET /api/chat/{conversationId}?userId=...
- GET /api/chat/user/{userId}/conversations
- DELETE /api/chat/{conversationId}?userId=...

### Reindex API
- POST /api/reindex?clean=true|false&pattern={optional}
- POST /api/reindex/single/{slug}
- GET /api/reindex/status

### Health
- GET /health
- GET /health/ready

## 16. Appendix B: package/runtime highlights

Frontend:
- React 18
- React Router 6
- Vite 5

Chat API:
- net10.0
- Azure.Search.Documents
- Microsoft.Azure.Cosmos
- Serilog + App Insights sink

Reindex API:
- net10.0
- Azure.Search.Documents
- DocumentFormat.OpenXml
- UglyToad.PdfPig

This runbook reflects current observed implementation in the repository and should be kept updated alongside service changes.
