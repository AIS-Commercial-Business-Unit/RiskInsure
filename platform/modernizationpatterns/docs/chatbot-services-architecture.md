# Modernization Patterns Chatbot - Complete Service Architecture Guide

## Purpose

This document is for **everyone**—from non-technical stakeholders to developers. It explains:
- What the chatbot system is in plain English
- EXACTLY what each service does
- EVERY backend call and interaction when users ask questions or content is updated
- Configuration and storage details
- How failures are handled

Anyone reading this should understand the entire architecture end-to-end.

## 0. What is this chatbot? (Laymen explanation)

Imagine a physical library with a librarian:
- **The website** = front desk where visitors enter.
- **The chat widget** = bell to call the librarian.
- **The librarian** = the chat API that listens to your question.
- **The note cards catalog** = Azure AI Search with searchable chunks of your patterns.
- **The meaning fingerprinter** = Azure OpenAI that understands what note cards mean and writes answers.
- **The conversation diary** = Azure Cosmos DB that remembers your conversation history.
- **The re-cataloging staff** = Reindex API that reads new books and updates the catalog when content changes.

When you ask a question:
1. Chat widget sends your question to the librarian (Chat API).
2. Librarian creates a "meaning fingerprint" of your question (embedding).
3. Librarian looks it up in the smart catalog (Azure Search).
4. Librarian finds matching note cards (chunks).
5. Librarian writes an answer based on those cards (chat completion).
6. Answer is sent back to you, word-by-word (streaming).
7. The conversation is recorded in the diary (Cosmos DB).

Every time patterns or inbox documents change:
1. GitHub detects the change.
2. Reindex staff read the new files.
3. They create new note cards (chunks).
4. They fingerprint each card (embeddings).
5. They update the catalog (Azure Search).
6. Next time you ask, you get fresh answers.

## 1. High-level architecture

The chatbot is a Retrieval-Augmented Generation (RAG) system built from:
- **React frontend**: Chat UI in the browser
- **Chat API** (.NET 10): The "brain" that answers questions
- **Reindex API** (.NET 10): The "librarian staff" that maintains the knowledge base
- **Azure OpenAI**: Understanding and text generation
- **Azure AI Search**: Smart knowledge catalog
- **Azure Cosmos DB**: Conversation memory
- **GitHub Actions**: Automation for content updates and deployment

### At chat runtime (user asks a question):
1. User types question → Frontend sends to Chat API
2. Chat API: "Let me understand this question" → Calls Azure OpenAI embedding
3. Chat API: "Find relevant content" → Searches Azure AI Search
4. Chat API: "What did we talk about before?" → Loads from Cosmos DB
5. Chat API: "Now write an answer" → Calls Azure OpenAI chat completion
6. Chat API: "Send answer back, word by word" → Streams SSE events to browser
7. Chat API: "Remember this conversation" → Saves to Cosmos DB

### At indexing time (content changes):
1. GitHub detects commit to patterns or inbox
2. Reindex workflow: "Wake up the Reindex API"
3. Reindex API: "Read all pattern and inbox files"
4. Reindex API: "Break them into small chunks"
5. Reindex API: "Create a meaning fingerprint for each chunk" → Calls Azure OpenAI embedding
6. Reindex API: "Upload chunks to the catalog" → Uploads to Azure AI Search
7. Workflow: "Report what was indexed"

## 2. What each service does (in detail)

## 2.1 Frontend Chat Widget (React SPA)

**Laymen explanation**: This is what you see and interact with. It's like the chat window on a website.

**In plain English**:
- Displays a floating chat bubble in the bottom-right corner.
- Opens a window where you type questions.
- Shows responses as they stream in (word by word).
- Keeps a list of past conversations so you can come back later.
- Remember your theme preference (light/dark).

**Technical responsibilities**:
- Render chat UI and message list.
- Manage input state (what you typed).
- Manage loading state (waiting for response).
- Manage conversation state (which chat you're in).
- Send requests to backend `/api/chat/stream` endpoint.
- Parse Server-Sent Events (SSE) to display streamed text.
- Extract and display citation metadata.
- Create/select/delete conversation entries.

**Code locations**:
- Main app router: `platform/modernizationpatterns/src/App.jsx`
- Widget container: `platform/modernizationpatterns/src/components/ChatWidget/ChatWidget.jsx`
- Hooks and state: `platform/modernizationpatterns/src/components/ChatWidget/useChatWidget.js`
- Chat window UI: `platform/modernizationpatterns/src/components/ChatWidget/ChatWindow.jsx`
- Sidebar (past conversations): `platform/modernizationpatterns/src/components/ChatWidget/Sidebar.jsx`
- Single conversation row: `platform/modernizationpatterns/src/components/ChatWidget/ConversationItem.jsx`

**Important behavior**:
- In local development: uses `http://localhost:5000/api/chat`
- In cloud: uses Azure Container App URL
- Streams response incrementally so you see text appear in real-time

## 2.2 Chat API Service (.NET 10)

**Laymen explanation**: This is the chatbot's brain. It listens to your question, finds relevant knowledge, and generates an answer.

**In plain English**:
- Listen for your question from the frontend.
- Understand what you're asking (create a "meaning fingerprint").
- Search the knowledge catalog for matching content.
- Retrieve what you talked about before (conversation history).
- Write an answer using both the retrieved content and history.
- Send answer back word-by-word so you see it streaming.
- Remember the conversation you just had.

**Technical responsibilities**:
- Validate request format and required fields.
- Execute RAG (Retrieval-Augmented Generation) orchestration.
- Call OpenAI for embeddings and completions.
- Call Azure AI Search for retrieval.
- Load and save conversations in Cosmos DB.
- Stream responses via SSE (Server-Sent Events).
- Extract and format citations.
- Provide health check endpoints.

**Primary endpoints**:
- `POST /api/chat/new` - Start a new conversation
- `POST /api/chat/stream` - Send a message and get streamed response
- `GET /api/chat/{conversationId}` - Retrieve a past conversation
- `GET /api/chat/user/{userId}/conversations` - List all your conversations
- `DELETE /api/chat/{conversationId}` - Delete a conversation
- `GET /health` - Health probe
- `GET /health/ready` - Readiness probe

**Code locations**:
- Startup and configuration: `platform/modernizationpatterns/Api/chat/Program.cs`
- All endpoints: `platform/modernizationpatterns/Api/chat/src/Controllers/ChatController.cs`
- Models: `platform/modernizationpatterns/Api/chat/src/Models/Conversation.cs`

## 2.3 OpenAI Service (.NET - inside Chat API)

**Laymen explanation**: This is like a smart translator that turns questions into "meaning fingerprints" and writes human-readable answers.

**In plain English**:
- Takes your question and creates a numeric "fingerprint" that captures its meaning.
- Takes relevant knowledge chunks and writes a natural answer.
- Remembers the last few turns of conversation for context.
- When writing an answer, it stays professional and concise.

**Technical responsibilities**:
- Call Azure OpenAI to embed (convert text to meaning vectors).
- Call Azure OpenAI to generate chat completions (write answers).
- Build message history with system prompt context.
- Handle API keys and endpoint configuration securely.

**Configuration keys needed**:
- `AzureOpenAI:Endpoint` - Azure OpenAI service URL
- `AzureOpenAI:ApiKey` - Secret key for API access
- `AzureOpenAI:ChatDeploymentName` - Model for answering (default: gpt-4.1)
- `AzureOpenAI:EmbeddingDeploymentName` - Model for fingerprints (default: text-embedding-3-small)

**Code location**: `platform/modernizationpatterns/Api/chat/src/Services/OpenAiService.cs`

## 2.4 Search Service (.NET - inside Chat API)

**Laymen explanation**: This is like looking up a question in a library's smart card catalog.

**In plain English**:
- Takes your question text.
- Searches the Azure AI Search index (the catalog).
- Returns the top 5 most relevant knowledge chunks.
- Those chunks are then used to write your answer.

**Technical responsibilities**:
- Format search query for Azure AI Search.
- Execute search with Cosmos database document fields.
- Parse results and normalize response objects.
- Handle API errors gracefully.

**Configuration keys needed**:
- `AzureSearch:Endpoint` - Azure Search service URL
- `AzureSearch:ApiKey` - Secret key for access
- `AzureSearch:IndexName` - Name of the index (default: modernization-patterns)

**Code location**: `platform/modernizationpatterns/Api/chat/src/Services/SearchService.cs`

**Note**: Currently sends text search only. Can be enhanced later to use both text and vector search.

## 2.5 Conversation Service (.NET - inside Chat API)

**Laymen explanation**: This saves and loads your conversations like a diary.

**In plain English**:
- When you send a message, it records your question AND the answer.
- When you come back later, it can load all your past conversations.
- By default, conversations expire after 90 days (for data privacy).

**Technical responsibilities**:
- Connect to Azure Cosmos DB.
- Read conversation by ID and user.
- Save (upsert) conversation after each exchange.
- Initialize database and container if they don't exist yet.
- Handle emulator connections for local development.

**Storage details**:
- **Database**: `modernization-patterns-db`
- **Container**: `conversations`
- **Partition key**: `/userId` (separates conversations by user)
- **TTL**: 90 days (7,776,000 seconds) - old conversations expire automatically

**Model structure** (`Conversation.cs`):
```csharp
- Id: unique conversation identifier
- UserId: which user owns it
- Messages: list of turns
  - Each message: role (user/assistant), content, timestamp, tokensUsed
- CreatedAt: when conversation started
- UpdatedAt: when last message was sent
- Status: active/deleted/archived
- TTL: auto-expiration seconds
```

**Code location**: `platform/modernizationpatterns/Api/chat/src/Services/ConversationService.cs`

## 2.6 Reindex API Service (.NET 10)

**Laymen explanation**: This is the librarian who reads new books and updates the library's catalog when patterns or inbox content changes.

**In plain English**:
- Reads all pattern JSON files.
- Reads all inbox documents (PDFs, Word docs, markdown, etc.).
- Breaks large documents into bite-sized chunks.
- Creates "meaning fingerprints" for each chunk.
- Uploads all chunks to the search index so chat can find them.
- Reports how many items were indexed.

**Technical responsibilities**:
- Discover pattern and inbox files from disk.
- Extract text from various formats (JSON, PDF, DOCX, Markdown, TXT).
- Chunk extracted text intelligently.
- Generate embeddings for all chunks.
- Upload indexed documents to Azure AI Search.
- Return metrics for reporting.
- Support clean reindex (delete all and start fresh).

**Primary endpoints**:
- `POST /api/reindex` - Run full reindex (supports `?clean=true` for full rebuild)
- `GET /api/reindex/status` - Check current index status
- `POST /api/reindex/single/{slug}` - Reindex one pattern for testing
- `GET /health` - Health probe
- `GET /health/ready` - Readiness probe

**Code locations**:
- Startup: `platform/modernizationpatterns/Api/reindex/Program.cs`
- Orchestration: `platform/modernizationpatterns/Api/reindex/src/Controllers/ReindexController.cs`

**Supported input file types**:
- `.json` - Pattern files or JSON data
- `.md` / `.markdown` - Markdown documentation
- `.txt` - Plain text files
- `.docx` - Microsoft Word documents
- `.pdf` - PDF documents (all pages are extracted)

## 2.7 Chunking Service (.NET - inside Reindex API)

**Laymen explanation**: This takes large documents and breaks them into smaller "note cards" that are easy for the AI to understand.

**In plain English**:
- Reads a 10-page pattern JSON file.
- Breaks it into 3-4 focused chunks (overview, implementation, complexity, examples).
- Ensures chunks overlap slightly so context isn't lost when reading one piece.
- For inbox documents (PDFs, etc.), it breaks them by sentence boundaries.
- Ensures each chunk ID is safe for the search index (no weird characters).

**Technical responsibilities**:
- Parse pattern JSON and extract semantic sections.
- Build structured chunks for patterns (overview, diagram, implementation, etc.).
- Build generic chunks for inbox documents.
- Split long text on sentence boundaries.
- Apply token overlap for context continuity.
- Generate Azure Search-safe document IDs.
- Estimate token counts for sizing.

**Chunk settings**:
- **Target chunk size**: ~500 tokens (estimate: 1 token ≈ 4 characters)
- **Overlap**: ~100 tokens (to prevent context loss)

**Chunk types for patterns**:
- `overview` - Title, summary, decision guidance
- `diagram` - Starter diagram and key components
- `implementation` - Implementation details and gotchas
- `complexity` - Complexity assessment
- `example` - Real-world examples
- `related` - Related patterns and further reading
- `guidance` - Long opinionated guidance (split further if needed)

**Chunk types for inbox**:
- `inbox-{sourceType}` - e.g., `inbox-pdf`, `inbox-docx`, `inbox-md`

**Code location**: `platform/modernizationpatterns/Api/reindex/src/Services/ChunkingService.cs`

## 2.8 Embedding Service (.NET - inside Reindex API)

**Laymen explanation**: This creates "meaning fingerprints" for each chunk so the search engine can find them quickly.

**In plain English**:
- Takes chunk text.
- Sends to Azure OpenAI.
- Gets back 1,536 numbers that represent the "meaning" of that chunk.
- Those numbers allow fast similarity searches.

**Technical responsibilities**:
- Call Azure OpenAI embeddings endpoint.
- Batch multiple texts per request for efficiency.
- Handle rate limiting with delays between batches.
- Return vectors (lists of numbers) for each chunk.

**Batch behavior**:
- **Batch size**: 16 chunks per request
- **Delay**: 200ms between batches (to prevent throttling)

**Default embedding model**: `text-embedding-3-small` (1,536 dimensions)

**Code location**: `platform/modernizationpatterns/Api/reindex/src/Services/EmbeddingService.cs`

## 2.9 Indexing Service (.NET - inside Reindex API)

**Laymen explanation**: This is like the library system that stores books on shelves and makes them searchable.

**In plain English**:
- Creates or updates the search index schema (the shelf structure).
- Uploads document chunks with their meaning fingerprints.
- Can delete all documents for a clean restart.
- Tells you how many documents are indexed.

**Technical responsibilities**:
- Create/update index schema in Azure AI Search.
- Upload documents with merge-or-upload strategy.
- Delete all documents for clean reindex mode.
- Return current index document count.

**Index fields stored**:
- `id` - Unique chunk identifier
- `patternSlug` - Which pattern it belongs to
- `title` - Display name
- `category` - For filtering (e.g., "inbox", "technical")
- `chunkType` - What kind of chunk (overview, implementation, etc.)
- `chunkIndex` - Order within the pattern
- `content` - The actual text you search over
- `contentVector` - The meaning fingerprint (1,536 numbers)

**Vector configuration**:
- **Dimensions**: 1,536
- **Algorithm**: HNSW (fast approximate nearest-neighbor search)
- **Metric**: Cosine similarity (how close are two meaning vectors)

**Code location**: `platform/modernizationpatterns/Api/reindex/src/Services/IndexingService.cs`

## 2.10 GitHub Workflow Automation

**Laymen explanation**: These are automated scripts that run when content changes, automatically reindexing the knowledge base.

**Reindex Workflow** (`.github/workflows/modernizationpatterns-reindex.yml`)
- **Triggers**: When pattern or inbox files change, or manual dispatch
- **Does**:
  - Detects what changed (added, modified, deleted files)
  - Wakes up the Reindex API (may be sleeping from scale-to-zero)
  - Chooses strategy: clean reindex (if deletions) or incremental (if only adds/mods)
  - Retries if there are temporary failures
  - Publishes summary showing counts of each file type indexed

**Content Validation Workflow** (`.github/workflows/modernizationpatterns-content-validate.yml`)
- **Triggers**: When PR changes pattern files
- **Does**:
  - Validates JSON syntax
  - Checks required fields exist
  - Reports back in PR with results

**Static Web App Deployment** (`.github/workflows/modernizationpatterns-static-webapp.yml`)
- **Triggers**: When UI or content changes land on main
- **Does**:
  - Builds frontend React site
  - Deploys to Azure Static Web Apps

## 3. Complete end-to-end flows (EVERY backend interaction)

## 3.1 User asks a question (detailed flow)

**What the user sees**: "I'll type 'What is CQRS?' and hit send"

**What happens in the backend** (step by step):

```
STEP 1: Frontend receives question
  - User types: "What is CQRS?"
  - User clicks Send button
  - Frontend creates conversationId (if new) and userId
  - Frontend UI state: isLoading = true

STEP 2: Frontend calls Chat API
  - Endpoint: POST http://localhost:5000/api/chat/stream
  - Request body:
    {
      "message": "What is CQRS?",
      "conversationId": "a1b2c3d4",
      "userId": "user-timestamp-12345"
    }
  - Response type: text/event-stream (streaming)
  - Frontend creates Reader and enters SSE parsing loop

STEP 3: Chat API validates request
  - Check message is not empty
  - Check conversationId is provided
  - Check userId is provided
  - If validation fails: send error event and return 400

STEP 4: Chat API creates embedding of the question
  - Calls OpenAiService.EmbedTextAsync("What is CQRS?")
  - OpenAiService calls Azure OpenAI embeddings endpoint:
    POST https://[azure-endpoint]/openai/deployments/text-embedding-3-small/embeddings?api-version=2024-06-01
    Headers: api-key=[secret-key]
    Body: {"input": "What is CQRS?"}
  - Receives back: 1,536 floating-point numbers (the meaning fingerprint)
  - Sends SSE event to frontend: "embedding_complete"

STEP 5: Chat API searches for relevant patterns
  - Calls SearchService.SearchPatternsAsync(
      query: "What is CQRS?",
      embeddingVector: [the 1,536 numbers],
      topK: 5
    )
  - SearchService calls Azure AI Search:
    POST https://[search-endpoint]/indexes/modernization-patterns/docs/search?api-version=2024-05-01-preview
    Headers: api-key=[secret-key]
    Body:
    {
      "search": "What is CQRS?",
      "top": 5,
      "count": true,
      "select": "id,patternSlug,title,category,content"
    }
  - Azure Search returns: Top 5 chunks matching query
    Example results:
    - CQRS Pattern (overview)
    - Event Sourcing (related pattern)
    - Query Model (supporting concept)
    - Write Model (supporting concept)
    - Consistency Patterns (related)
  - Sends SSE event to frontend: "search_complete: Found 5 relevant patterns"

STEP 6: Chat API loads conversation history
  - Current conversation ID: a1b2c3d4
  - Calls ConversationService.GetConversationAsync(
      conversationId: "a1b2c3d4",
      userId: "user-timestamp-12345"
    )
  - ConversationService queries Cosmos DB:
    SELECT * FROM conversations c
    WHERE c.id = "a1b2c3d4" 
    AND c.userId = "user-timestamp-12345"
  - Returns: Conversation document with previous messages (if any)
    Example (new conversation): empty messages array
    Example (continuing): 
    [
      {role: "user", content: "What is DDD?", timestamp: "2026-03-13T10:00:00Z"},
      {role: "assistant", content: "Domain-Driven Design...", timestamp: "2026-03-13T10:00:05Z"}
    ]

STEP 7: Chat API builds the prompt with context
  - Reads system prompt from file:
    platform/modernizationpatterns/Api/chat/src/prompts/system-prompt.txt
  - If file missing, uses fallback prompt
  - Injects top 3 retrieved patterns into {REFERENCE_MATERIAL} placeholder
  - Final system prompt includes conversation context rules

STEP 8: Chat API calls completion endpoint
  - Calls OpenAiService.GetCompletionAsync(
      systemPrompt: [the built prompt with references],
      userMessage: "What is CQRS?",
      history: [previous turns if any, max 6 most recent]
    )
  - OpenAiService calls Azure OpenAI chat completions:
    POST https://[azure-endpoint]/openai/deployments/gpt-4.1/chat/completions?api-version=2024-06-01
    Headers: api-key=[secret-key]
    Body:
    {
      "messages": [
        {
          "role": "system",
          "content": "You are a concise assistant... [system prompt with pattern references]"
        },
        {role: "user", content: "What is CQRS?"}
      ],
      "temperature": 0.2,
      "max_tokens": 800
    }
  - Azure OpenAI generates complete answer

STEP 9: Chat API streams response back to frontend
  - Converts completion string into small chunks (3 words at a time)
  - For each chunk, sends SSE event:
    event: token
    data: CQRS (Command Query Responsibility Segregation)...
  - Sends event: "completion_done: Response streaming complete"
  
  - Frontend receives each token event:
    - Parses: event type = "token", data = the text
    - Appends data to assistant message content
    - UI updates in real-time (you see text appearing)

STEP 10: Chat API sends citation metadata
  - Extracts source patterns from top 3 retrieved chunks
  - Creates citations: "CQRS (Technical Pattern); Event Sourcing (Technical Pattern)..."
  - Sends SSE event:
    event: response_metadata
    data: Citations: CQRS (Patterns); Event Sourcing (Patterns); Consistency Patterns (Patterns)
  - Frontend parses this and displays source links

STEP 11: Chat API saves conversation to Cosmos DB
  - Creates/updates conversation document:
    {
      "id": "a1b2c3d4",
      "userId": "user-timestamp-12345",
      "messages": [
        {
          "role": "user",
          "content": "What is CQRS?",
          "timestamp": "2026-03-13T10:05:00Z",
          "tokensUsed": 4
        },
        {
          "role": "assistant",
          "content": "CQRS (Command Query Responsibility Segregation) is a pattern...",
          "timestamp": "2026-03-13T10:05:02Z",
          "tokensUsed": 156
        }
      ],
      "createdAt": "2026-03-13T10:05:00Z",
      "updatedAt": "2026-03-13T10:05:02Z",
      "status": "active",
      "ttl": 7776000
    }
  - Calls Cosmos DB upsertItemAsync with partition key userId
  - Document is stored for future reference

STEP 12: Chat API sends done event
  - Sends final SSE event:
    event: done
    data: Chat exchange complete
  - Frontend:
    - Stops SSE reader
    - Marks message as not streaming
    - Re-enables input box
    - Updates conversation in sidebar
    - Sets isLoading = false

STEP 13: Frontend updates UI
  - Message fully rendered
  - Citations displayed with links to patterns
  - You can now type another question or start new conversation
```

## 3.2 Content changes - Reindex Flow (detailed)

**What happens**: Developer pushes changes to patterns or inbox documents

**What happens in the backend** (step by step):

```
STEP 1: GitHub detects changes
  - Developer pushes to main branch
  - File changes detected:
    - platform/modernizationpatterns/content/patterns/somepattern.json
    - platform/modernizationpatterns/content/_inbox/somefile.pdf
  - Reindex workflow automatically triggers (or user manually dispatches)

STEP 2: Workflow detects change type
  - Git diff of base vs head SHA
  - Identifies files added, modified, or deleted
  - If deletions detected: will use clean=true mode
  - If only adds/mods: will use clean=false (faster, incremental)
  - Sends decision to next job

STEP 3: Workflow wakes up Reindex API
  - Gets Reindex API endpoint URL from Azure Container Apps
  - Reindex container may be at zero scale (sleeping to save costs)
  - Health check loop runs up to 18 times:
    GET /health
    - Retry if 404/502/503 (container starting up)
    - Continue when 200 (container ready)
    - Waiting 10 seconds between attempts
  - Sends status update: "Container ready"

STEP 4: Workflow calls reindex endpoint
  - Calls: POST https://modernizationpatterns-reindex.../api/reindex?clean=false
  - Waits up to 300 seconds for response
  - If timeout/failure: retries up to 5 times with 30-second backoff

STEP 5: Reindex API resolves content paths
  - Looks for content folders:
    - config-driven path first
    - Relative paths from current directory
    - Fallback: c:\RiskInsure\RiskInsure\platform\modernizationpatterns\content\...
  - Confirms patterns folder exists: content/patterns
  - Confirms inbox folder exists: content/_inbox

STEP 6: Reindex API reads pattern files
  - Scans: content/patterns/*.json
  - Reads each file:
    - strangler-fig-migration.json
    - event-driven-architecture.json
    - cqrs.json
    - ... (41 total patterns)
  - Each file contains JSON with:
    title, category, subcategory, summary, diagram, technologies, gotchas, complexity, examples, etc.

STEP 7: Reindex API reads inbox files
  - Scans: content/_inbox/* (all subdirectories)
  - Supported extensions: .pdf, .docx, .md, .markdown, .txt, .json
  - Example files found:
    - README.md
    - patternssummary.md
    - toplevelpattens.md
    - somewhitepaper.pdf
    - architecture-guide.docx

STEP 8: Reindex API extracts text by format
  - For JSON/Markdown/TXT: reads file as string
  - For DOCX: 
    Uses DocumentFormat.OpenXml.WordprocessingDocument
    Opens document, extracts MainDocumentPart.Body.InnerText
  - For PDF:
    Uses UglyToad.PdfPig
    Opens document, iterates pages
    Concatenates page.Text from all pages
  - Result: raw text ready for chunking

STEP 9: Reindex API chunks patterns
  - For each pattern JSON:
    - Creates structured chunks by semantic sections:
      - overview: title + summary + decision guidance
      - diagram: diagram description and nodes
      - implementation: technologies + gotchas + guidance
      - complexity: level + rationale + demands
      - example: real-world context + approach + outcome
      - related: related patterns + tags + further reading
      - guidance: long guidance text (split further if >500 tokens)
  - Example for CQRS:
    - Chunk 1 ID: "cqrs_overview" - overview section
    - Chunk 2 ID: "cqrs_diagram" - diagram section
    - Chunk 3 ID: "cqrs_implementation" - implementation section
    - Chunk 4 ID: "cqrs_complexity" - complexity section
    - Chunk 5 ID: "cqrs_example" - real-world example
    - Chunk 6 ID: "cqrs_related" - related information

STEP 10: Reindex API chunks inbox documents
  - For each inbox file:
    - Chunks by sentence boundaries
    - Target: 500 tokens per chunk
    - Overlap: 100 tokens between chunks
    - Chunk IDs: "inbox_filename_0", "inbox_filename_1", etc.
    - Example for "whitepaper.pdf":
      - Chunk 1 ID: "inbox_whitepaper_0"
      - Chunk 2 ID: "inbox_whitepaper_1"
      - ... (more chunks if file is long)
    - Metadata: category="inbox", chunkType="inbox-pdf"

STEP 11: Reindex API generates all embedding vectors
  - Collects all chunk texts: ~1000 total chunks (41 patterns + inbox)
  - Calls EmbeddingService.EmbedBatchAsync(all texts)
  - Service batches into groups of 16
  - For each batch, calls Azure OpenAI:
    POST https://[azure-endpoint]/openai/deployments/text-embedding-3-small/embeddings
    Body: {"input": [chunk1, chunk2, ..., chunk16]}
    Returns 16 vectors of 1,536 numbers each
  - Delays 200ms between batches to prevent rate limiting
  - Total time: ~30-60 seconds depending on file sizes

STEP 12: Reindex API optionally cleans old docs
  - If clean=true mode (files were deleted):
    - Calls IndexingService.DeleteAllDocumentsAsync
    - Searches index:
      GET /indexes/modernization-patterns/docs/search
      Result: all 1000+ existing documents
    - Deletes all documents in batches
    - Waits 2 seconds for propagation

STEP 13: Reindex API uploads vectorized documents
  - Creates IndexDocument objects pairing chunks with vectors:
    {
      "id": "cqrs_overview",
      "patternSlug": "cqrs",
      "title": "CQRS Pattern",
      "category": "technical",
      "chunkType": "overview",
      "chunkIndex": 0,
      "content": "CQRS stands for...",
      "contentVector": [0.0123, -0.0456, ...1536 numbers...]
    }
  - Batches upload 100 documents at a time
  - Calls Azure AI Search MergeOrUploadDocumentsAsync
  - Azure Search updates/creates index entries
  - All 1000+ documents uploaded

STEP 14: Reindex API returns metrics
  - Response JSON:
    {
      "status": "success",
      "patternsProcessed": 41,
      "inboxDocumentsProcessed": 7,
      "chunksCreated": 1021,
      "documentsUploaded": 1021,
      "documentsDeleted": 0,
      "totalDocumentsInIndex": 1021,
      "elapsedSeconds": 45.23,
      "patterns": ["cqrs", "event-sourcing", "saga-pattern", ...],
      "inboxDocuments": ["README.md", "patternssummary.md", ...]
    }

STEP 15: Workflow publishes summary
  - Parses response JSON with jq
  - Extracts: patterns indexed, inbox documents, file-type counts
  - Counts files by extension:
    - .pdf count: 2
    - .docx count: 1
    - .md count: 3
    - .txt count: 0
    - .json count: 1
  - Writes to GITHUB_STEP_SUMMARY:
    | Item | Count |
    |---|---:|
    | Patterns Indexed | 41 |
    | Inbox Documents | 7 |
    | PDF Files | 2 |
    | DOCX Files | 1 |
    | Markdown Files | 3 |
    ...

STEP 16: Chat knows about fresh content
  - Within 30 seconds, Azure Search index is fully updated
  - Next time someone asks a question:
    - Search finds the newly indexed chunks
    - Chat can answer using fresh content
    - Old content still available (no deletions)
```

## 4. Operational checkpoints

Health endpoints:
- Chat API: /health and /health/ready
- Reindex API: /health and /health/ready

Essential runtime checks:
1. Confirm chat API health returns 200.
2. Confirm reindex API health returns 200.
3. Confirm Azure Search index has documents.
4. Confirm chat stream emits token events.
5. Confirm Cosmos documents are upserting.

## 5. Known implementation details and caveats

1. Conversation preview mapping in sidebar can show fallback text when wrong property is read.
- Stored message model uses content.
- Preview should read latest message content.

2. Conversation list endpoint currently returns placeholder empty list in controller.

3. Search service accepts embedding parameter, but current request body path is text search.

## 6. Suggested improvements

1. Implement Cosmos query for user conversation listing endpoint.
2. Enable hybrid/vector retrieval payload in SearchService.
3. Ensure sidebar preview uses latest message content and keeps conversations state synchronized.
4. Add endpoint auth/rate limiting hardening where needed.
5. Add targeted tests for chunk id safety, SSE parsing, and retrieval quality.

## 7. Quick reference

Frontend:
- React 18 + Vite + React Router

Chat API:
- ASP.NET Core net10.0
- Serilog logging
- Cosmos persistence
- Azure OpenAI chat+embedding

Reindex API:
- ASP.NET Core net10.0
- Azure Search indexing
- PDF/DOCX extraction
- Embedding batch ingestion

Content source:
- platform/modernizationpatterns/content/patterns
- platform/modernizationpatterns/content/_inbox

This guide is intended as the service-by-service architecture baseline for onboarding, maintenance, and future enhancement planning.
