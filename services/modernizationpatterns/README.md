# Modernization Patterns Management Service

Provides the RAG (Retrieval-Augmented Generation) chatbot API and content reindexing capabilities for the Modernization Patterns knowledge base.

## Service Overview

| Component | Port | Purpose |
|-----------|------|---------|
| Api | 5001 | Chat API with RAG pipeline and SSE streaming |
| Endpoint.In | 5010 | Reindex API for content ingestion |

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                        Chat API (Api)                               │
│  - Receives user questions via HTTP POST                            │
│  - Retrieves context from Azure AI Search                           │
│  - Generates responses using Azure OpenAI (GPT-4.1)                 │
│  - Streams responses via Server-Sent Events (SSE)                   │
│  - Persists conversations to Cosmos DB                              │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                    Reindex API (Endpoint.In)                        │
│  - Reads pattern JSON files from content/patterns                   │
│  - Extracts text from inbox documents (PDF, DOCX, MD)               │
│  - Chunks content into smaller searchable units                     │
│  - Generates embeddings via Azure OpenAI                            │
│  - Uploads vectors to Azure AI Search                               │
└─────────────────────────────────────────────────────────────────────┘
```

## Project Structure

```
services/modernizationpatterns/
├── src/
│   ├── Api/                    # Chat API (HTTP endpoints)
│   │   ├── Controllers/
│   │   │   └── ChatController.cs
│   │   ├── prompts/
│   │   │   └── system-prompt.txt
│   │   └── Program.cs
│   ├── Domain/                 # Business logic interfaces & models
│   │   ├── Models/
│   │   │   ├── Conversation.cs
│   │   │   └── SearchModels.cs
│   │   └── Services/           # Interface definitions
│   ├── Infrastructure/         # External service implementations
│   │   ├── AzureOpenAi/
│   │   ├── AzureSearch/
│   │   ├── CosmosDb/
│   │   └── Chunking/
│   └── Endpoint.In/            # Reindex worker API
│       └── Controllers/
│           └── ReindexController.cs
├── test/
│   ├── Unit.Tests/
│   └── Integration.Tests/
└── docs/
```

## API Endpoints

### Chat API (`http://localhost:5001`)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/health` | Health check |
| POST | `/api/chat` | Send a chat message (SSE streaming response) |
| GET | `/api/chat/conversations` | List user conversations |
| GET | `/api/chat/conversations/{id}` | Get conversation details |
| DELETE | `/api/chat/conversations/{id}` | Delete a conversation |

#### Chat Request Example

```http
POST /api/chat
Content-Type: application/json
Accept: text/event-stream

{
  "userId": "user-123",
  "conversationId": "optional-uuid",
  "message": "What is the strangler fig pattern?"
}
```

### Reindex API (`http://localhost:5010`)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/health` | Health check |
| POST | `/api/reindex` | Full reindex of all patterns |
| POST | `/api/reindex?clean=true` | Clean reindex (delete first) |
| POST | `/api/reindex?pattern=strangler-fig` | Index specific pattern |
| GET | `/api/reindex/status` | Check index status |

## Configuration

### Environment Variables / appsettings.json

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://YOUR-OPENAI.openai.azure.com/",
    "ApiKey": "YOUR-KEY",
    "ChatDeploymentName": "gpt-4.1",
    "EmbeddingDeploymentName": "text-embedding-3-small"
  },
  "AzureSearch": {
    "Endpoint": "https://YOUR-SEARCH.search.windows.net",
    "ApiKey": "YOUR-KEY",
    "IndexName": "modernization-patterns"
  },
  "Cosmos": {
    "Endpoint": "https://YOUR-COSMOS.documents.azure.com:443/",
    "ApiKey": "YOUR-KEY",
    "DatabaseName": "modernization-patterns",
    "ContainerName": "conversations"
  }
}
```

## Local Development

### Prerequisites

- .NET 10 SDK
- Azure OpenAI resource with gpt-4.1 and text-embedding-3-small deployments
- Azure AI Search resource
- Azure Cosmos DB (or emulator)

### Running the Services

```powershell
# Terminal 1: Chat API
cd services/modernizationpatterns/src/Api
cp appsettings.Development.json.template appsettings.Development.json
# Edit appsettings.Development.json with your Azure credentials
dotnet run

# Terminal 2: Reindex API
cd services/modernizationpatterns/src/Endpoint.In
cp appsettings.Development.json.template appsettings.Development.json
# Edit appsettings.Development.json with your Azure credentials
dotnet run

# Terminal 3: Index the content
curl -X POST http://localhost:5010/api/reindex
```

### Testing the Chat

```powershell
# Simple test (non-streaming)
curl -X POST http://localhost:5001/api/chat `
  -H "Content-Type: application/json" `
  -d '{"userId":"test","message":"What is the strangler fig pattern?"}'
```

## Content Sources

The reindex service reads patterns from:

- `platform/modernizationpatterns/content/patterns/*.json` — Structured pattern definitions
- `platform/modernizationpatterns/content/_inbox/` — Supporting documents (PDF, DOCX, MD)

## Technology Stack

| Component | Technology |
|-----------|------------|
| Framework | .NET 10 |
| AI Chat | Azure OpenAI (gpt-4.1) |
| Embeddings | Azure OpenAI (text-embedding-3-small, 1536 dims) |
| Vector Search | Azure AI Search (HNSW algorithm) |
| Persistence | Azure Cosmos DB |
| Streaming | Server-Sent Events (SSE) |
| Logging | Serilog |
