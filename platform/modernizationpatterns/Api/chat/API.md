**Chat API Documentation**

Complete reference for all endpoints, request/response formats, and usage flows.

---

## **Overview**

The Chat API provides RAG (Retrieval-Augmented Generation) endpoints for conversation management and streaming responses. All endpoints are hosted at `http://localhost:5000/api/chat` during development. Base URL is configurable via environment variables.

---

## **Authentication & Headers**

- ✅ No authentication currently (userId passed as query/body param for tracking).
- ✅ All endpoints return JSON unless noted (SSE stream is text/event-stream).
- ✅ CORS should be enabled if frontend and API are on different origins.

---

## **Endpoints**

### **1) POST /api/chat/new — Create New Conversation**

**Purpose**: Initialize a new conversation record. Called when user opens the chat for the first time or clicks "New Chat".

**Request**:
```
URL: POST /api/chat/new?userId=user-12345
Headers: 
  (none required)
Query Parameters:
  - userId (required, string): Unique identifier for the user (e.g., "user-1234567890")
Body: 
  (empty)
```

**Response (200 OK)**:
```json
{
  "conversationId": "a1b2c3d4",
  "userId": "user-12345",
  "createdAt": "2026-03-02T10:30:00Z",
  "message": "Conversation created. Use POST /api/chat/stream to start chatting."
}
```

**Error Cases**:
- `400 Bad Request`: If `userId` is missing or empty.
- `500 Internal Server Error`: Cosmos DB write fails (rare).

**When Called (UI Flow)**:
1. User clicks the chat bubble to open the widget.
2. `useChatWidget.toggleWindow()` detects no conversation exists.
3. `createConversation()` is called in the background (non-blocking) to create the conversation record.
4. Frontend immediately shows greeting while this call completes.
5. Also called when user clicks "New Chat" button in the sidebar.

**Note**: The conversation is created empty (no messages yet). The first user message is added later via the `/stream` endpoint.

---

### **2) POST /api/chat/stream — Send Message & Stream Response**

**Purpose**: Send a user message, retrieve relevant patterns via RAG, and stream the assistant's response token-by-token using Server-Sent Events (SSE).

**Request**:
```
URL: POST /api/chat/stream
Headers:
  Content-Type: application/json
Body (JSON):
{
  "message": "What is event-driven architecture?",
  "conversationId": "a1b2c3d4",
  "userId": "user-12345"
}
```

**Request Fields**:
- `message` (required, string): User's question. Max length depends on model (typically 4000 chars).
- `conversationId` (required, string): ID of the conversation (obtained from `/new` endpoint).
- `userId` (required, string): Unique user identifier (must match the user who created the conversation).

**Response (200 OK - Server-Sent Events Stream)**:

The response is streamed as text/event-stream. The browser receives multiple events:

```
event: embedding_complete
data: Query embedded successfully

event: search_complete
data: Found 3 relevant patterns

event: token
data: Event-driven

event: token
data:  architecture

event: token
data:  is a design pattern

... (more token events) ...

event: completion_done
data: Response streaming complete

event: response_metadata
data: Citations: Event-Driven Architecture (Patterns); CQRS (Patterns); Reactive Streams (Patterns)

event: done
data: Chat exchange complete
```

**Event Types Explained**:
- `embedding_complete`: User query has been embedded into vector space.
- `embedding_skipped`: Embedding failed; continuing with keyword search.
- `search_complete`: Relevant patterns found (number varies).
- `token`: Single token/chunk of the assistant response (appears repeatedly).
- `completion_done`: All tokens streamed; response generation finished.
- `response_metadata`: Metadata including citations (source patterns).
- `done`: Entire exchange complete; response is safe to display.
- `error`: (if error occurs) Error message describing the issue.

**Error Cases**:
- `400 Bad Request`: If `message`, `conversationId`, or `userId` is missing/empty.
- `500 Internal Server Error`: 
  - OpenAI API unavailable.
  - Azure AI Search unreachable.
  - Cosmos DB write fails.
- Stream may terminate early if backend encounters an error (check `error` event).

**When Called (UI Flow)**:
1. User types a question and clicks the Send button.
2. `useChatWidget.sendMessage()` validates input and posts to this endpoint.
3. Frontend appends the user message to the message list.
4. Frontend parses the SSE stream:
   - Each `token` event appends text to the assistant message (streaming effect).
   - `response_metadata` event is parsed to extract citations and display them with the message.
5. When `done` event received, mark the assistant message as complete and enable send button again.
6. Backend persists the conversation (user + assistant message) to Cosmos DB.

**Stream Parsing Notes (Frontend)**:
- The frontend's `useChatWidget.js` parses the stream line-by-line.
- For each line, it extracts `event: <type>` and `data: <content>`.
- Only `token` events are appended to the message; other events update UI state or extract metadata.
- Citations are extracted from `response_metadata` data using regex: `Citations: Pattern1; Pattern2; Pattern3`.

---

### **3) GET /api/chat/{conversationId} — Get Conversation History**

**Purpose**: Retrieve a specific conversation including all messages. Called when user selects a prior conversation from the sidebar.

**Request**:
```
URL: GET /api/chat/a1b2c3d4?userId=user-12345
Headers:
  (none required)
Path Parameters:
  - conversationId (required, string): The conversation ID to fetch.
Query Parameters:
  - userId (required, string): User ID (must match conversation owner for security).
Body: 
  (none)
```

**Response (200 OK)**:
```json
{
  "id": "a1b2c3d4",
  "userId": "user-12345",
  "createdAt": "2026-03-02T10:00:00Z",
  "updatedAt": "2026-03-02T10:15:00Z",
  "status": "active",
  "messages": [
    {
      "role": "assistant",
      "content": "Hi How can i help you with modernization system?",
      "timestamp": "2026-03-02T10:00:05Z",
      "tokensUsed": 20
    },
    {
      "role": "user",
      "content": "What is CQRS?",
      "timestamp": "2026-03-02T10:05:00Z",
      "tokensUsed": 15
    },
    {
      "role": "assistant",
      "content": "CQRS (Command Query Responsibility Segregation) separates read and write models...",
      "timestamp": "2026-03-02T10:05:30Z",
      "tokensUsed": 45
    }
  ]
}
```

**Response Fields**:
- `id`: Conversation ID.
- `userId`: Owner of the conversation.
- `createdAt`: ISO timestamp when conversation was created.
- `updatedAt`: ISO timestamp of the last message/update.
- `status`: "active", "deleted", or "archived".
- `messages`: Array of message objects:
  - `role`: "user" or "assistant".
  - `content`: Message text.
  - `timestamp`: When the message was sent/received.
  - `tokensUsed`: Approximate token count (for billing/analytics).

**Error Cases**:
- `400 Bad Request`: If `userId` is missing.
- `404 Not Found`: If conversation doesn't exist or `userId` doesn't match the conversation owner.

**When Called (UI Flow)**:
1. User clicks a prior conversation in the sidebar.
2. `useChatWidget.selectConversation(convId)` is called.
3. Frontend calls `loadConversation(convId)` which sends this GET request.
4. Response messages are loaded into the message list UI.
5. Conversation ID is set as current, ready for new messages.

---

### **4) GET /api/chat/user/{userId}/conversations — List User's Conversations**

**Purpose**: Fetch all conversations for a user. Used to populate the sidebar conversation list on app load.

**Request**:
```
URL: GET /api/chat/user/user-12345/conversations
Headers: 
  (none required)
Path Parameters:
  - userId (required, string): User ID to list conversations for.
Query Parameters:
  (none)
Body: 
  (none)
```

**Response (200 OK)**:
```json
{
  "userId": "user-12345",
  "conversations": [
    {
      "id": "a1b2c3d4",
      "createdAt": "2026-03-02T10:00:00Z",
      "messages": [
        { "role": "user", "message": "What is CQRS?" }
      ]
    },
    {
      "id": "e5f6g7h8",
      "createdAt": "2026-03-02T09:00:00Z",
      "messages": [
        { "role": "user", "message": "Explain event sourcing" }
      ]
    }
  ],
  "message": "Use POST /api/chat/new to create conversations"
}
```

**Error Cases**:
- `400 Bad Request`: If `userId` is missing.

**Current Limitation**:
- **NOTE**: This endpoint currently returns an empty list. In production, implement a Cosmos DB query to fetch all conversations where `userId == provided userId`. The frontend currently tracks conversations in local state / localStorage.

**When Called (UI Flow)**:
1. On app load, `useChatWidget` initializes state.
2. (Currently not called due to the limitation above).
3. In the future, this will be called to hydrate the sidebar with the user's prior conversations.

---

### **5) DELETE /api/chat/{conversationId} — Delete Conversation**

**Purpose**: Soft-delete a conversation (mark as deleted, but don't remove from database). Called when user clicks the delete (trash icon) on a conversation in the sidebar.

**Request**:
```
URL: DELETE /api/chat/a1b2c3d4?userId=user-12345
Headers:
  (none required)
Path Parameters:
  - conversationId (required, string): The conversation to delete.
Query Parameters:
  - userId (required, string): User ID (must match conversation owner).
Body: 
  (none)
```

**Response (200 OK)**:
```json
{
  "message": "Conversation deleted"
}
```

**Error Cases**:
- `400 Bad Request`: If `userId` is missing.
- `404 Not Found`: If conversation doesn't exist or `userId` doesn't match.

**When Called (UI Flow)**:
1. User hovers over a conversation in the sidebar and clicks the delete icon.
2. `useChatWidget.deleteConversation(convId)` is called.
3. Frontend sends DELETE request.
4. Conversation is removed from the sidebar UI immediately (optimistic update).
5. If current conversation was deleted, the sidebar is cleared and user can start a new chat.

**Soft Delete Note**:
- The conversation is not permanently removed; its `status` is set to "deleted".
- This allows for recovery/archiving later if needed.
- Soft-deleted conversations can be filtered out in future queries.

---

## **Backend System Prompt Configuration**

### **File Location**:
`platform/modernizationpatterns/Api/chat/src/prompts/system-prompt.txt`

### **Purpose**:
The system prompt is kept separate from code so you can modify assistant behavior without recompiling. It's loaded at runtime by `ChatController.BuildSystemPrompt()`.

### **Template Format**:
```
You are a concise assistant helping users understand RiskInsure patterns.

REFERENCE MATERIAL (stay faithful to this content, don't paraphrase):
{REFERENCE_MATERIAL}

INSTRUCTIONS:
1. Answer using information directly from the reference material above
2. Keep answer to 2-3 sentences max
3. Quote or closely follow the source content - do NOT paraphrase
4. Be direct and practical
```

### **How It Works**:
1. Controller loads the template on first request (cached).
2. For each user message, the top 3 relevant patterns are retrieved via Azure AI Search.
3. The `{REFERENCE_MATERIAL}` placeholder is replaced with the pattern content.
4. The filled prompt is passed to OpenAI's GPT model along with conversation history.
5. Response is streamed back to the frontend.

### **Editing the Prompt** (without recompile):
1. Edit `src/prompts/system-prompt.txt` in your API project.
2. Restart the Chat API service.
3. Next request will load the new prompt.

---

## **Frontend to Backend Communication Flow (Summary)**

```
User Action                  → Frontend Call                 → Backend Endpoint
─────────────────────────────────────────────────────────────────────────
Open chat                    → createConversation()          → POST /api/chat/new
Click "New Chat"             → createConversation()          → POST /api/chat/new
Send message                 → sendMessage()                 → POST /api/chat/stream (SSE)
Select prior conversation    → loadConversation(convId)      → GET /api/chat/{convId}
Click Delete on conversation → deleteConversation(convId)    → DELETE /api/chat/{convId}
─────────────────────────────────────────────────────────────────────────
```

---

## **Error Handling & Resilience**

### **Frontend Error Handling**:
- If `/new` fails: Error logged, user can retry by clicking "New Chat" again.
- If `/stream` fails: An error message is appended to the chat ("❌ Error: ..."); user can retry sending.
- If `/conversations/{id}` fails: User sees "failed to load conversation" and can try selecting a different one.
- If DeleteTransaction fails: Error logged; conversation may still appear in UI until page refresh.

### **Backend Error Handling**:
- **Embedding failure**: Falls back to keyword search (logged as warning).
- **Search failure**: Continues without context to LLM (graceful degradation).
- **LLM timeout**: Returns error event in SSE stream.
- **Cosmos DB failure**: Logged and error returned; conversation may be partially persisted.

---

## **Environment & Configuration**

### **API Base URL Resolution** (Frontend):
The frontend determines the API base URL in this order:
1. `import.meta.env.VITE_CHAT_API_BASE` (Vite build-time env var)
2. `window.__CHAT_API_BASE__` (runtime global override in `index.html`)
3. `process.env.REACT_APP_CHAT_API_BASE` (CRA-style env at build time)
4. Fallback: `${window.location.origin}/api/chat` (same-origin, used in production if frontend and API hosted together)

### **Setting for Production**:
- **Azure App Service**: Set `VITE_CHAT_API_BASE` during the build pipeline to the API's public URL.
- **Azure Static Web App + separate App Service API**: Set `VITE_CHAT_API_BASE` to the App Service API URL during build.
- **Same-host deployment**: Leave all unset; fallback will use `/api/chat` (requires reverse proxy or co-hosted deployment).

---

## **Testing Endpoints (cURL / PowerShell examples)**

### **Create Conversation** (PowerShell):
```powershell
$response = Invoke-WebRequest -Uri "http://localhost:5000/api/chat/new?userId=test-user-123" -Method POST
$response.Content | ConvertFrom-Json | Format-List
```

### **Send Message & Stream** (PowerShell):
```powershell
$body = @{
  message = "What is event-driven architecture?"
  conversationId = "abc123"
  userId = "test-user-123"
} | ConvertTo-Json

$response = Invoke-WebRequest -Uri "http://localhost:5000/api/chat/stream" `
  -Method POST `
  -Body $body `
  -ContentType "application/json"

$response.Content  # This is the SSE stream text; parse events as described above
```

### **Get Conversation** (PowerShell):
```powershell
$response = Invoke-WebRequest -Uri "http://localhost:5000/api/chat/abc123?userId=test-user-123" -Method GET
$response.Content | ConvertFrom-Json | Format-List
```

### **Delete Conversation** (PowerShell):
```powershell
$response = Invoke-WebRequest -Uri "http://localhost:5000/api/chat/abc123?userId=test-user-123" -Method DELETE
$response.Content | ConvertFrom-Json | Format-List
```

---

## **Rate Limiting & Quotas**

- **Current**: None enforced (add as needed for production).
- **Recommended**: 
  - 10 new conversations per user per hour.
  - 100 messages per user per hour.
  - Implement via middleware or Azure API Management.

---

## **Logging & Monitoring**

All endpoints log to the application logger:
- Request received (userId, conversationId, message preview)
- Embedding result (success/skip)
- Search result count
- Conversation save status
- Any errors (with full exception details)

Monitor logs in Application Insights or your chosen log service.

---

## **Future Enhancements**

- [ ] Implement `/api/chat/user/{userId}/conversations` with actual Cosmos DB query.
- [ ] Add rate limiting middleware.
- [ ] Add conversation search by content.
- [ ] Add user preferences (model selection, response length, etc.).
- [ ] Add conversation tagging / pinning.
- [ ] Add webhook for conversation events (for integrations).

---

