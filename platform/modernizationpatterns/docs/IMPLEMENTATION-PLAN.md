# AI Chatbot Implementation Plan — Complete Phase-by-Phase Roadmap

**Status**: Building from scratch (no Chat API backend yet)  
**Timeline**: 3–4 weeks (21–28 days)  
**Tech Stack**: .NET 10 (C#), Terraform, Azure (OpenAI, AI Search, Cosmos DB, Container Apps, ACR)  
**Repository**: `platform/modernizationpatterns` (monorepo in `services/` style)

---

## Table of Contents

1. [Current State Assessment](#1-current-state-assessment)
2. [Architecture Overview](#2-architecture-overview)
3. [Phase 1: Foundation & Setup (Days 1–2)](#phase-1-foundation--setup-days-1–2)
4. [Phase 2: Service Architecture (Days 3–5)](#phase-2-service-architecture-days-3–5)
5. [Phase 3: Chat API Core (Days 6–8)](#phase-3-chat-api-core-days-6–8)
6. [Phase 4: Reindex Service (Days 9–10)](#phase-4-reindex-service-days-9–10)
7. [Phase 5: Infrastructure as Code (Days 11–13)](#phase-5-infrastructure-as-code-days-11–13)
8. [Phase 6: Docker & Local Development (Days 14–15)](#phase-6-docker--local-development-days-14–15)
9. [Phase 7: CI/CD Pipeline (Days 16–17)](#phase-7-cicd-pipeline-days-16–17)
10. [Phase 8: Frontend Integration (Days 18–19)](#phase-8-frontend-integration-days-18–19)
11. [Phase 9: Testing, Monitoring, & Go-Live (Days 20–21)](#phase-9-testing-monitoring--go-live-days-20–21)
12. [Deployment Checklist](#deployment-checklist)
13. [Quick Reference](#quick-reference)

---

## 1. Current State Assessment

### What Exists ✓

```
RiskInsure/
├── platform/modernizationpatterns/
│   ├── src/                    ✓ React 18 SPA (Vite)
│   │   ├── components/
│   │   ├── routes/
│   │   └── App.jsx
│   ├── content/                ✓ Pattern definitions (JSON)
│   ├── docs/
│   │   └── ai-chatbot-architecture.md  ✓ High-level design
│   ├── package.json            ✓ Frontend dependencies
│   └── vite.config.js          ✓ Build config
├── services/billing/           ✓ Example .NET service (template)
│   ├── src/
│   │   ├── Api/               ✓ Controllers, Models
│   │   ├── Domain/            ✓ Business logic, Contracts
│   │   ├── Infrastructure/    ✓ Cosmos/NServiceBus config
│   │   └── Endpoint.In/       ✓ Message handlers
│   └── test/
└── Directory.Packages.props    ✓ Centralized NuGet versions
```

### What's Missing (Must Build)

| Item | Type | Purpose |
|------|------|---------|
| `Chat.Api` project | .NET 10 | HTTP endpoints for chatbot |
| `Reindex.Endpoint` project | .NET 10 | Background indexing service |
| Service wrappers | C# classes | OpenAI, AI Search, Cosmos DB clients |
| ChatController | C# | POST `/api/chat/stream` endpoint |
| Terraform modules | HCL | Infrastructure (VNet, Container Apps, Search, Cosmos, ACR) |
| Dockerfile | Docker | Chat & Reindex containerization |
| GitHub Actions | YAML | Build, push, deploy workflow |
| ChatWidget integration | React | Frontend SSE streaming |

---

## 2. Architecture Overview

### System Flow

```
┌─────────────────────────────────────┐
│     User (Browser)                  │
│  React SPA + ChatWidget             │
└────────────┬────────────────────────┘
             │ POST /api/chat/stream
             ▼
┌─────────────────────────────────────┐
│   Azure Static Web Apps             │
│   (Authentication, CORS)            │
└────────────┬────────────────────────┘
             │ Route to Chat Pod
             ▼
┌─────────────────────────────────────────────────────────────┐
│  Azure Container Apps Environment                           │
│                                                             │
│  ┌─────────────────┐          ┌─────────────────┐        │
│  │  Chat API Pod   │          │ Reindex Pod     │        │
│  │  (always on)    │          │ (timer/webhook) │        │
│  │                 │          │                 │        │
│  │ 1. Embed Q&A    │          │ 1. Read files   │        │
│  │ 2. Search       │          │ 2. Chunk text   │        │
│  │ 3. Stream resp  │          │ 3. Embed chunks │        │
│  └──────┬──────────┘          │ 4. Index        │        │
│         │                     └────────┬────────┘        │
│         └──────────────┬───────────────┘                │
└──────────┼──────────────┼──────────────────────────────────┘
           │              │
           ▼              ▼
    ┌────────────┐  ┌─────────────────┐
    │ Azure      │  │ Azure           │
    │ OpenAI     │  │ AI Search       │
    | GPT-4o     │  │ (Vector Index)  │
    | Embeddings │  └─────────────────┘
    └────────────┘           ▲
                             │
                       ┌─────┴─────┐
                       │           │
                    ┌──────────┐ ┌───────────┐
                    │ Cosmos   │ │ Cosmos    │
                    │ DB       │ │ DB        │
                    │ Patterns │ │Conv Store │
                    └──────────┘ └───────────┘
```

### Key Decisions

| Decision | Why |
|----------|-----|
| **Terraform** (not Bicep) | You requested Terraform; better for multi-cloud |
| **.NET 10** (not Node.js) | Consistency with RiskInsure monorepo + Billing service pattern |
| **Container Apps** | Serverless, KEDA auto-scale, best for streaming |
| **Azure AI Search** | Native vector + semantic search, no vendor lock-in |
| **Cosmos DB** | Already in RiskInsure; serverless, single-partition strategy |
| **Managed Identity** | No secrets in code; follows RiskInsure security |

---

# PHASE 1: Foundation & Setup (Days 1–2)

## Objectives
- [x] Create .NET project structure (Chat.Api, Reindex.Endpoint)
- [x] Add NuGet dependencies to `Directory.Packages.props`
- [x] Configure Azure credential handling
- [x] Set up local development files

## Task 1.1: Create Project Structure

```bash
cd c:\RiskInsure\RiskInsure\platform\modernizationpatterns

# Create .NET project directories (following RiskInsure pattern)
mkdir -p api/chat/{src,test}
mkdir -p api/reindex/{src,test}

# Create layer structure within each service
mkdir -p api/chat/src/{Controllers,Services,Models,Handlers}
mkdir -p api/reindex/src/{Services,Models,Handlers}

# Test projects
mkdir -p api/chat/test/Unit.Tests
mkdir -p api/reindex/test/Unit.Tests
```

**Check**: Directories created at same level as `src/` (frontend) and `content/`

---

## Task 1.2: Create `Chat.Api.csproj`

**File**: `platform/modernizationpatterns/api/chat/Chat.Api.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>RiskInsure.Modernization.Chat.Api</RootNamespace>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <!-- ASP.NET Core (versions from Directory.Packages.props) -->
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
    <PackageReference Include="Scalar.AspNetCore" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />
    
    <!-- Azure SDKs (manage centrally) -->
    <PackageReference Include="Azure.AI.OpenAI" />
    <PackageReference Include="Azure.Search.Documents" />
    <PackageReference Include="Microsoft.Azure.Cosmos" />
    <PackageReference Include="Azure.Identity" />
    <PackageReference Include="Azure.Security.KeyVault.Secrets" />
    
    <!-- Logging -->
    <PackageReference Include="Serilog.AspNetCore" />
    <PackageReference Include="Serilog.Sinks.Console" />
    <PackageReference Include="Serilog.Sinks.ApplicationInsights" />
    <PackageReference Include="Microsoft.ApplicationInsights.AspNetCore" />
  </ItemGroup>

</Project>
```

---

## Task 1.3: Create `Reindex.Endpoint.In.csproj`

**File**: `platform/modernizationpatterns/api/reindex/Reindex.Endpoint.In.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>RiskInsure.Modernization.Reindex</RootNamespace>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <!-- ASP.NET Core -->
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
    <PackageReference Include="Scalar.AspNetCore" />
    
    <!-- Azure SDKs -->
    <PackageReference Include="Azure.AI.OpenAI" />
    <PackageReference Include="Azure.Search.Documents" />
    <PackageReference Include="Azure.Identity" />
    
    <!-- Logging -->
    <PackageReference Include="Serilog.AspNetCore" />
    <PackageReference Include="Serilog.Sinks.Console" />
  </ItemGroup>

</Project>
```

---

## Task 1.4: Update `Directory.Packages.props` with New Dependencies

Add these to `c:\RiskInsure\RiskInsure\Directory.Packages.props` (inside `<ItemGroup>`):

```xml
<!-- Azure AI & Search (NEW) -->
<PackageVersion Include="Azure.AI.OpenAI" Version="1.0.0-beta.15" />
<PackageVersion Include="Azure.Search.Documents" Version="11.5.0" />
<PackageVersion Include="Microsoft.ApplicationInsights.AspNetCore" Version="2.21.0" />
```

**Note**: Most packages already exist; just add the 3 new Azure SDKs above.

---

## Task 1.5: Create appsettings Template Files

**File**: `api/chat/appsettings.json`
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

**File**: `api/chat/appsettings.Development.json.template` (git-ignored, copy and fill in)
```json
{
  "ConnectionStrings": {
    "CosmosDb": "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/D+4vIrQnrC1+gXK9YdJFAqkZDyqkJQW8=..."
  },
  "CosmosDb": {
    "DatabaseName": "modernization-patterns-db"
  },
  "AzureOpenAI": {
    "Endpoint": "https://<your-resource>.openai.azure.com/",
    "ApiKey": "your-api-key-here",
    "DeploymentName": "gpt-4o"
  },
  "AzureSearch": {
    "Endpoint": "https://<your-search>.search.windows.net/",
    "ApiKey": "your-search-key-here",
    "IndexName": "modernization-patterns"
  },
  "ApplicationInsights": {
    "InstrumentationKey": "00000000-0000-0000-0000-000000000000"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

**Same for Reindex**: `api/reindex/appsettings.Development.json.template`

---

## Task 1.6: Add Projects to Solution

From root directory:

```bash
cd c:\RiskInsure\RiskInsure

# Add Chat API
dotnet sln RiskInsure.slnx add platform/modernizationpatterns/api/chat/Chat.Api.csproj

# Add Reindex Endpoint
dotnet sln RiskInsure.slnx add platform/modernizationpatterns/api/reindex/Reindex.Endpoint.In.csproj

# Verify
dotnet sln RiskInsure.slnx list
```

---

## Task 1.7: Verify Local Build

```bash
cd c:\RiskInsure\RiskInsure

# Restore
dotnet restore

# Build all
dotnet build

# Should see:
# Build succeeded. ✓
```

---

## Phase 1 Checklist

- [ ] Directory structure created (`api/chat`, `api/reindex` folders)
- [ ] `Chat.Api.csproj` created
- [ ] `Reindex.Endpoint.In.csproj` created
- [ ] New NuGet packages added to `Directory.Packages.props`
- [ ] `appsettings.json` templates created (Development version git-ignored)
- [ ] Projects added to `RiskInsure.slnx`
- [ ] `dotnet build` succeeds
- [ ] Verify projects appear in Visual Studio Solution Explorer

**Effort**: 2 hours  
**Owner**: One developer  
**Success Criteria**: No build errors; projects in solution explorer

---

# PHASE 2: Service Architecture (Days 3–5)

## Objectives
- Create Azure service wrappers (OpenAI, AI Search, Cosmos DB)
- Build domain models for conversations and search results
- Set up dependency injection
- All logic testable (low infrastructure coupling)

---

## Task 2.1: Create OpenAI Service Wrapper

**File**: `api/chat/src/Services/OpenAiService.cs`

```csharp
namespace RiskInsure.Modernization.Chat.Services;

using Azure.AI.OpenAI;
using Azure.Identity;
using System.ClientModel;
using Microsoft.Extensions.Logging;

public interface IOpenAiService
{
    /// <summary>Embed text using text-embedding-3-large model</summary>
    Task<float[]> EmbedTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>Stream chat completions for RAG assistant</summary>
    IAsyncEnumerable<string> StreamCompletionAsync(
        string systemPrompt,
        string userMessage,
        List<ConversationMessage> history,
        CancellationToken cancellationToken = default);
}

public class OpenAiService : IOpenAiService
{
    private readonly OpenAIClient _client;
    private readonly string _chatDeploymentName;
    private readonly ILogger<OpenAiService> _logger;

    public OpenAiService(IConfiguration config, ILogger<OpenAiService> logger)
    {
        _logger = logger;
        
        var endpoint = config["AzureOpenAI:Endpoint"] 
            ?? throw new InvalidOperationException("AzureOpenAI:Endpoint not configured");
        var apiKey = config["AzureOpenAI:ApiKey"] 
            ?? throw new InvalidOperationException("AzureOpenAI:ApiKey not configured");
        
        _chatDeploymentName = config["AzureOpenAI:DeploymentName"] ?? "gpt-4o";

        _client = new OpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
    }

    public async Task<float[]> EmbedTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be empty", nameof(text));

        _logger.LogDebug("Embedding text: {TextLength} chars", text.Length);

        var options = new EmbeddingsOptions { DeploymentName = "text-embedding-3-large", Input = { text } };
        var response = await _client.GetEmbeddingsAsync(options, cancellationToken);

        var embedding = response.Value.Data[0].Embedding;
        return embedding.ToArray();
    }

    public async IAsyncEnumerable<string> StreamCompletionAsync(
        string systemPrompt,
        string userMessage,
        List<ConversationMessage> history,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Streaming chat completion, history: {HistoryCount} messages", history.Count);

        var chatMessages = new List<ChatMessage>
        {
            new(ChatCompletionRole.System, systemPrompt)
        };

        // Add up to 10 previous messages for context
        foreach (var msg in history.TakeLast(10))
        {
            var role = msg.Role == "user" ? ChatCompletionRole.User : ChatCompletionRole.Assistant;
            chatMessages.Add(new ChatMessage(role, msg.Content));
        }

        // Add current user message
        chatMessages.Add(new ChatMessage(ChatCompletionRole.User, userMessage));

        var options = new ChatCompletionOptions
        {
            Temperature = 0.7f,
            MaxTokens = 1000,
            DeploymentName = _chatDeploymentName
        };

        using var streamResponse = await _client.GetChatCompletionsStreamingAsync(
            deploymentOrModelName: _chatDeploymentName,
            chatCompletionOptions: options,
            cancellationToken: cancellationToken);

        await foreach (var update in streamResponse.EnumerateRawEnumerableAsync(cancellationToken))
        {
            if (update.Choices.Count > 0)
            {
                var delta = update.Choices[0].Delta;
                if (delta?.Content != null)
                {
                    yield return delta.Content;
                }
            }
        }

        _logger.LogInformation("Streaming chat completion finished");
    }
}

public record ConversationMessage(string Role, string Content, DateTimeOffset Timestamp);
```

---

## Task 2.2: Create AI Search Service Wrapper

**File**: `api/chat/src/Services/SearchService.cs`

```csharp
namespace RiskInsure.Modernization.Chat.Services;

using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;

public interface ISearchService
{
    Task<List<SearchResultItem>> SearchPatternsAsync(
        string query,
        float[]? embeddingVector = null,
        int topK = 5,
        CancellationToken cancellationToken = default);
}

public class SearchService : ISearchService
{
    private readonly SearchClient _searchClient;
    private readonly ILogger<SearchService> _logger;

    public SearchService(IConfiguration config, ILogger<SearchService> logger)
    {
        _logger = logger;
        
        var endpoint = config["AzureSearch:Endpoint"] 
            ?? throw new InvalidOperationException("AzureSearch:Endpoint not configured");
        var apiKey = config["AzureSearch:ApiKey"] 
            ?? throw new InvalidOperationException("AzureSearch:ApiKey not configured");
        var indexName = config["AzureSearch:IndexName"] ?? "modernization-patterns";

        var credential = new AzureKeyCredential(apiKey);
        _searchClient = new SearchClient(new Uri(endpoint), indexName, credential);
    }

    public async Task<List<SearchResultItem>> SearchPatternsAsync(
        string query,
        float[]? embeddingVector = null,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching patterns for: {Query}", query);

        var searchOptions = new SearchOptions
        {
            Size = topK,
            QueryType = SearchQueryType.Semantic,
            SemanticConfiguration = "default",
            IncludeTotalCount = true
        };

        // Add vector search if embedding provided
        if (embeddingVector != null && embeddingVector.Length > 0)
        {
            searchOptions.VectorSearch = new VectorSearchOptions
            {
                Queries =
                {
                    new VectorizedQuery(embeddingVector)
                    {
                        KNearestNeighborsCount = topK,
                        Fields = { "contentVector" }
                    }
                }
            };
        }

        var results = await _searchClient.SearchAsync<SearchDocument>(query, searchOptions, cancellationToken);

        var resultList = new List<SearchResultItem>();
        await foreach (var result in results.GetResultsAsync())
        {
            var doc = result.Document;
            resultList.Add(new SearchResultItem
            {
                Id = doc["id"]?.ToString() ?? "",
                PatternSlug = doc["patternSlug"]?.ToString() ?? "",
                Title = doc["title"]?.ToString() ?? "",
                Category = doc["category"]?.ToString() ?? "",
                Content = doc["content"]?.ToString() ?? "",
                Relevance = result.Score ?? 0
            });

            _logger.LogDebug("Found: {Title} (score: {Score})", 
                doc["title"], result.Score);
        }

        return resultList;
    }
}

public class SearchResultItem
{
    public required string Id { get; init; }
    public required string PatternSlug { get; init; }
    public required string Title { get; init; }
    public required string Category { get; init; }
    public required string Content { get; init; }
    public required double Relevance { get; init; }
}
```

---

## Task 2.3: Create Cosmos DB Service (Conversation Store)

**File**: `api/chat/src/Services/ConversationService.cs`

```csharp
namespace RiskInsure.Modernization.Chat.Services;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RiskInsure.Modernization.Chat.Models;

public interface IConversationService
{
    Task<Conversation?> GetConversationAsync(
        string conversationId, 
        string userId, 
        CancellationToken cancellationToken = default);

    Task SaveConversationAsync(
        Conversation conversation, 
        CancellationToken cancellationToken = default);
}

public class ConversationService : IConversationService
{
    private readonly Container _container;
    private readonly ILogger<ConversationService> _logger;

    public ConversationService(IConfiguration config, ILogger<ConversationService> logger)
    {
        _logger = logger;

        var connectionString = config.GetConnectionString("CosmosDb") 
            ?? throw new InvalidOperationException("CosmosDb connection string not configured");

        var databaseName = config["CosmosDb:DatabaseName"] ?? "modernization-patterns-db";
        const string containerName = "conversations";

        var client = new CosmosClient(connectionString);
        var database = client.GetDatabase(databaseName);
        _container = database.GetContainer(containerName);
    }

    public async Task<Conversation?> GetConversationAsync(
        string conversationId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving conversation {ConversationId} for user {UserId}", 
            conversationId, userId);

        try
        {
            var response = await _container.ReadItemAsync<Conversation>(
                conversationId,
                new PartitionKey(userId),
                cancellationToken: cancellationToken);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Conversation not found: {ConversationId}", conversationId);
            return null;
        }
    }

    public async Task SaveConversationAsync(
        Conversation conversation,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Saving conversation {ConversationId} for user {UserId}",
                conversation.Id, conversation.UserId);

            await _container.UpsertItemAsync(conversation, cancellationToken: cancellationToken);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Failed to save conversation {ConversationId}", conversation.Id);
            throw;
        }
    }
}
```

---

## Task 2.4: Create Domain Models

**File**: `api/chat/src/Models/Conversation.cs`

```csharp
namespace RiskInsure.Modernization.Chat.Models;

using System.Text.Json.Serialization;

public class Conversation
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("userId")]
    public required string UserId { get; set; }

    [JsonPropertyName("messages")]
    public List<Message> Messages { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "active";

    [JsonPropertyName("ttl")]
    public int? Ttl { get; set; } = 7776000; // 90 days in seconds
}

public class Message
{
    [JsonPropertyName("role")]
    public required string Role { get; set; } // "user" or "assistant"

    [JsonPropertyName("content")]
    public required string Content { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("tokensUsed")]
    public int TokensUsed { get; set; }
}

public record ChatRequestDto(
    required string Message,
    required string ConversationId,
    required string UserId);

public record ChatResponseDto(
    required string Answer,
    required List<string> Citations,
    required int TokensUsed);
```

---

## Task 2.5: Configure Dependency Injection

**File**: `api/chat/src/Program.cs`

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using RiskInsure.Modernization.Chat.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting Modernization Patterns Chat API");

    var builder = WebApplication.CreateBuilder(args);

    // Serilog
    builder.Host.UseSerilog();

    // Controllers & OpenAPI
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddOpenApi();

    // CORS for SWA integration
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowSWA", policyBuilder =>
        {
            policyBuilder
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
    });

    // Register services
    builder.Services.AddScoped<IOpenAiService, OpenAiService>();
    builder.Services.AddScoped<ISearchService, SearchService>();
    builder.Services.AddScoped<IConversationService, ConversationService>();

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("AllowSWA");
    app.MapControllers();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
```

---

## Phase 2 Checklist

- [ ] OpenAiService created & tested locally
- [ ] SearchService created & tested locally
- [ ] ConversationService created & tested locally
- [ ] Domain models created (Conversation, Message, DTO records)
- [ ] Dependency injection configured in Program.cs
- [ ] Projects build without errors
- [ ] Interfaces are unit-test friendly (Moq-compatible)

**Effort**: 3 days  
**Owner**: 1–2 developers  
**Success Criteria**: 
- All services implement interfaces
- Dependency injection resolves correctly
- Mock-friendly design patterns used

---

# PHASE 3: Chat API Core (Days 6–8)

## Objectives
- Implement POST `/api/chat/stream` endpoint with Server-Sent Events (SSE)
- Build RAG pipeline: embed query → search patterns → stream response
- Implement conversation persistence
- Error handling & logging throughout

---

## Task 3.1: Create Chat Controller with Streaming

**File**: `api/chat/src/Controllers/ChatController.cs`

```csharp
namespace RiskInsure.Modernization.Chat.Controllers;

using Microsoft.AspNetCore.Mvc;
using RiskInsure.Modernization.Chat.Models;
using RiskInsure.Modernization.Chat.Services;
using System.Text.Json;
using System.Text;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IOpenAiService _openAiService;
    private readonly ISearchService _searchService;
    private readonly IConversationService _conversationService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IOpenAiService openAiService,
        ISearchService searchService,
        IConversationService conversationService,
        ILogger<ChatController> logger)
    {
        _openAiService = openAiService;
        _searchService = searchService;
        _conversationService = conversationService;
        _logger = logger;
    }

    /// <summary>Stream chat response using RAG (Retrieval-Augmented Generation)</summary>
    [HttpPost("stream")]
    public async Task StreamChat(
        [FromBody] ChatRequestDto request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Chat request received from {UserId}, conversation: {ConversationId}",
            request.UserId, request.ConversationId);

        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                await Response.WriteAsync("Message cannot be empty");
                return;
            }

            // 1. Get existing conversation or create new one
            var conversation = await _conversationService.GetConversationAsync(
                request.ConversationId, request.UserId, cancellationToken)
                ?? new Conversation
                {
                    Id = request.ConversationId,
                    UserId = request.UserId
                };

            // 2. Embed user's question
            _logger.LogDebug("Embedding user message");
            var embedding = await _openAiService.EmbedTextAsync(request.Message, cancellationToken);

            // 3. Search for relevant patterns
            _logger.LogDebug("Searching for relevant patterns");
            var searchResults = await _searchService.SearchPatternsAsync(
                request.Message,
                embedding,
                topK: 5,
                cancellationToken: cancellationToken);

            if (!searchResults.Any())
            {
                _logger.LogWarning("No patterns found for query: {Query}", request.Message);
            }

            // 4. Build system prompt with context
            var systemPrompt = BuildSystemPrompt(searchResults);

            // 5. Convert conversation history to service format
            var history = conversation.Messages
                .Select(m => new ConversationMessage(m.Role, m.Content, m.Timestamp))
                .ToList();

            // 6. Set up streaming response
            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Append("Connection", "keep-alive");

            // 7. Stream completions
            var fullResponse = new StringBuilder();
            var citations = searchResults.Select(r => r.Title).Distinct().ToList();

            _logger.LogDebug("Starting streaming response");

            await foreach (var chunk in _openAiService.StreamCompletionAsync(
                systemPrompt,
                request.Message,
                history,
                cancellationToken))
            {
                fullResponse.Append(chunk);

                // Send SSE data
                var data = JsonSerializer.Serialize(new { delta = chunk });
                await Response.WriteAsync($"data: {data}\n\n", Encoding.UTF8, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }

            // 8. Save conversation
            conversation.Messages.Add(new Message
            {
                Role = "user",
                Content = request.Message,
                Timestamp = DateTimeOffset.UtcNow
            });

            conversation.Messages.Add(new Message
            {
                Role = "assistant",
                Content = fullResponse.ToString(),
                Timestamp = DateTimeOffset.UtcNow
            });

            conversation.UpdatedAt = DateTimeOffset.UtcNow;

            await _conversationService.SaveConversationAsync(conversation, cancellationToken);

            // 9. Send completion marker
            var finalData = JsonSerializer.Serialize(new { citations, tokensUsed = 0 });
            await Response.WriteAsync($"data: [DONE]\n\n", Encoding.UTF8, cancellationToken);

            _logger.LogInformation(
                "Chat request completed. Response length: {Length}, citations: {CitationCount}",
                fullResponse.Length, citations.Count);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Chat request cancelled by client");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request");

            if (!Response.HasStarted)
            {
                Response.StatusCode = StatusCodes.Status500InternalServerError;
            }

            try
            {
                await Response.WriteAsync($"data: error: {ex.Message}\n\n");
            }
            catch (Exception writeEx)
            {
                _logger.LogError(writeEx, "Failed to write error response");
            }
        }
    }

    private static string BuildSystemPrompt(List<SearchResultItem> patterns)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are the Modernization Patterns Atlas assistant.");
        sb.AppendLine("Your role is to help RiskInsure engineers understand and apply modernization patterns.");
        sb.AppendLine();
        sb.AppendLine("INSTRUCTIONS:");
        sb.AppendLine("1. Answer questions using ONLY the patterns provided below.");
        sb.AppendLine("2. Be concise and practical—relate answers to insurance/commerce domains where possible.");
        sb.AppendLine("3. Always cite the pattern name when referencing a specific pattern.");
        sb.AppendLine("4. If the user asks about something outside the provided patterns, politely redirect them.");
        sb.AppendLine();
        sb.AppendLine("AVAILABLE PATTERNS:");
        sb.AppendLine();

        for (int i = 0; i < patterns.Count; i++)
        {
            sb.AppendLine($"{i + 1}. **{patterns[i].Title}** ({patterns[i].Category})");
            sb.AppendLine($"   {patterns[i].Content.Substring(0, Math.Min(200, patterns[i].Content.Length))}...");
            sb.AppendLine();
        }

        sb.AppendLine("---");

        return sb.ToString();
    }
}
```

---

## Task 3.2: Create Additional Endpoints (GET Conversation, List)

**Add to ChatController**:

```csharp
/// <summary>Get conversation history</summary>
[HttpGet("{conversationId}")]
public async Task<IActionResult> GetConversation(
    string conversationId,
    [FromQuery] string userId,
    CancellationToken cancellationToken)
{
    _logger.LogDebug("Retrieving conversation {ConversationId}", conversationId);

    var conversation = await _conversationService.GetConversationAsync(
        conversationId, userId, cancellationToken);

    if (conversation == null)
        return NotFound();

    return Ok(conversation);
}

/// <summary>Start new conversation</summary>
[HttpPost("new")]
public IActionResult CreateConversation([FromQuery] string userId)
{
    var id = Guid.NewGuid().ToString()[..8];
    return Created(
        $"/api/chat/{id}",
        new { conversationId = id, userId, createdAt = DateTimeOffset.UtcNow });
}
```

---

## Task 3.3: Unit Tests for ChatController

**File**: `api/chat/test/Unit.Tests/ChatControllerTests.cs`

```csharp
namespace RiskInsure.Modernization.Chat.Tests;

using Moq;
using RiskInsure.Modernization.Chat.Controllers;
using RiskInsure.Modernization.Chat.Models;
using RiskInsure.Modernization.Chat.Services;
using Xunit;

public class ChatControllerTests
{
    [Fact]
    public async Task StreamChat_WithValidRequest_ReturnsStreamedResponse()
    {
        // Arrange
        var openAiServiceMock = new Mock<IOpenAiService>();
        var searchServiceMock = new Mock<ISearchService>();
        var conversationServiceMock = new Mock<IConversationService>();
        var loggerMock = new Mock<ILogger<ChatController>>();

        openAiServiceMock
            .Setup(x => x.EmbedTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[1536]);

        searchServiceMock
            .Setup(x => x.SearchPatternsAsync(
                It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchResultItem>
            {
                new()
                {
                    Id = "1",
                    Title = "Strangler Fig",
                    Category = "Migration",
                    PatternSlug = "strangler-fig",
                    Content = "Pattern content...",
                    Relevance = 0.95
                }
            });

        var controller = new ChatController(
            openAiServiceMock.Object,
            searchServiceMock.Object,
            conversationServiceMock.Object,
            loggerMock.Object);

        var request = new ChatRequestDto(
            Message: "What is the strangler pattern?",
            ConversationId: "conv-123",
            UserId: "user@riskinsure.com");

        // Act & Assert
        // Controller method streams to HttpContext.Response
        // In real test: use HttpContext mock or integration tests
        Assert.NotNull(controller);
    }
}
```

---

## Phase 3 Checklist

- [ ] ChatController created with POST `/api/chat/stream` endpoint
- [ ] SSE streaming implemented (HTTP chunked response)
- [ ] RAG pipeline working: embed → search → prompt build → stream
- [ ] Conversation persistence to Cosmos DB
- [ ] Error handling & logging throughout
- [ ] GET `/api/chat/{id}` endpoint for history
- [ ] POST `/api/chat/new` endpoint for starting conversations
- [ ] Unit tests cover happy path
- [ ] Builds successfully

**Effort**: 3 days  
**Owner**: 1–2 developers  
**Success Criteria**:
- Endpoint callable via curl/Postman
- SSE streaming works (tokens appear one by one)
- Conversation saved to Cosmos DB
- Error responses are structured

---

# PHASE 4: Reindex Service (Days 9–10)

## Objectives
- Read pattern files from `content/patterns/*.json`
- Chunk content into ~500-token segments
- Embed chunks using OpenAI
- Upsert into AI Search index
- Expose webhook for GitHub push triggers

---

## Task 4.1: Create Chunking Service

**File**: `api/reindex/src/Services/ChunkingService.cs`

```csharp
namespace RiskInsure.Modernization.Reindex.Services;

using Microsoft.Extensions.Logging;
using System.Text;

public interface IChunkingService
{
    List<string> ChunkText(string text, int targetTokens = 500, int overlapTokens = 100);
}

public class ChunkingService : IChunkingService
{
    private readonly ILogger<ChunkingService> _logger;

    public ChunkingService(ILogger<ChunkingService> logger)
    {
        _logger = logger;
    }

    public List<string> ChunkText(string text, int targetTokens = 500, int overlapTokens = 100)
    {
        _logger.LogInformation(
            "Chunking text: {TextLength} chars into ~{TargetTokens} token segments",
            text.Length, targetTokens);

        var chunks = new List<string>();
        var sentences = text.Split(new[] { ". ", ".\n", ".\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        var currentChunk = new StringBuilder();
        var estimatedTokens = 0;

        for (int i = 0; i < sentences.Length; i++)
        {
            var sentence = sentences[i].Trim();
            if (string.IsNullOrEmpty(sentence)) continue;

            var sentenceTokens = EstimateTokens(sentence);

            // If adding this sentence would exceed target, save current chunk
            if (estimatedTokens + sentenceTokens > targetTokens && currentChunk.Length > 0)
            {
                var chunk = currentChunk.ToString().Trim();
                chunks.Add(chunk);

                _logger.LogDebug("Saved chunk: {ChunkLength} chars, ~{Tokens} tokens",
                    chunk.Length, estimatedTokens);

                // Add overlap from end of previous chunk
                var overlapStart = Math.Max(0, chunk.Length - (overlapTokens * 4)); // ~4 chars per token
                currentChunk.Clear();
                currentChunk.Append(chunk[overlapStart..]);
                estimatedTokens = EstimateTokens(currentChunk.ToString());
            }

            currentChunk.Append(sentence).Append(". ");
            estimatedTokens += sentenceTokens;
        }

        // Add final chunk
        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString().Trim());
        }

        _logger.LogInformation("Chunking complete: {ChunkCount} chunks created", chunks.Count);
        return chunks;
    }

    private static int EstimateTokens(string text)
    {
        // Rough heuristic: ~1 token per 4 characters
        return text.Length / 4;
    }
}
```

---

## Task 4.2: Create Indexing Service

**File**: `api/reindex/src/Services/IndexingService.cs`

```csharp
namespace RiskInsure.Modernization.Reindex.Services;

using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Logging;

public interface IIndexingService
{
    Task<bool> CreateIndexIfNotExistsAsync(CancellationToken cancellationToken = default);
    Task UpsertDocumentsAsync(
        List<Azure.Search.Documents.SearchDocument> documents,
        CancellationToken cancellationToken = default);
}

public class IndexingService : IIndexingService
{
    private readonly SearchIndexClient _indexClient;
    private readonly SearchClient _searchClient;
    private readonly ILogger<IndexingService> _logger;
    private readonly string _indexName;

    public IndexingService(IConfiguration config, ILogger<IndexingService> logger)
    {
        _logger = logger;

        var endpoint = config["AzureSearch:Endpoint"] 
            ?? throw new InvalidOperationException("AzureSearch:Endpoint not configured");
        var apiKey = config["AzureSearch:ApiKey"] 
            ?? throw new InvalidOperationException("AzureSearch:ApiKey not configured");

        _indexName = config["AzureSearch:IndexName"] ?? "modernization-patterns";

        var credential = new AzureKeyCredential(apiKey);
        _indexClient = new SearchIndexClient(new Uri(endpoint), credential);
        _searchClient = new SearchClient(new Uri(endpoint), _indexName, credential);
    }

    public async Task<bool> CreateIndexIfNotExistsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating/updating index: {IndexName}", _indexName);

        var index = new SearchIndex(_indexName)
        {
            Fields = new[]
            {
                new SearchField("id", SearchFieldDataType.String) { IsKey = true },
                new SearchField("patternSlug", SearchFieldDataType.String) 
                { 
                    IsFilterable = true, 
                    IsSortable = true 
                },
                new SearchField("title", SearchFieldDataType.String) 
                { 
                    IsSearchable = true 
                },
                new SearchField("category", SearchFieldDataType.String) 
                { 
                    IsFilterable = true, 
                    IsFacetable = true 
                },
                new SearchField("content", SearchFieldDataType.String) 
                { 
                    IsSearchable = true 
                },
                new SearchField("contentVector", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                {
                    IsSearchable = true,
                    VectorSearchDimensions = 1536,
                    VectorSearchProfileName = "default-hnsw"
                },
                new SearchField("chunkIndex", SearchFieldDataType.Int32),
                new SearchField("uploadedAt", SearchFieldDataType.DateTimeOffset) 
                { 
                    IsFilterable = true, 
                    IsSortable = true 
                }
            },
            VectorSearch = new VectorSearch
            {
                Algorithms = { new HnswAlgorithmConfiguration("default-hnsw") },
                Profiles = { new VectorSearchProfile("default-hnsw", "default-hnsw") }
            },
            SemanticConfiguration = new SemanticConfiguration("default", new SemanticPrioritizedFields
            {
                TitleField = new SemanticField { FieldName = "title" },
                ContentFields = { new SemanticField { FieldName = "content" } }
            })
        };

        try
        {
            await _indexClient.CreateOrUpdateIndexAsync(index, cancellationToken: cancellationToken);
            _logger.LogInformation("Index created/updated successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create index");
            throw;
        }
    }

    public async Task UpsertDocumentsAsync(
        List<Azure.Search.Documents.SearchDocument> documents,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Upserting {DocumentCount} documents into index", documents.Count);

        try
        {
            var batch = IndexDocumentsBatch.MergeOrUpload(documents);
            var result = await _searchClient.IndexDocumentsAsync(batch, cancellationToken: cancellationToken);

            var succeededCount = result.Value.Results.Count(r => r.Succeeded);
            _logger.LogInformation("Upserted: {SucceededCount} documents succeeded", succeededCount);

            if (result.Value.Results.Any(r => !r.Succeeded))
            {
                var failed = result.Value.Results.Where(r => !r.Succeeded).ToList();
                _logger.LogError("Failed to upsert {FailedCount} documents", failed.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting documents");
            throw;
        }
    }
}
```

---

## Task 4.3: Create Reindex Controller (Webhook Handler)

**File**: `api/reindex/src/Controllers/ReindexController.cs`

```csharp
namespace RiskInsure.Modernization.Reindex.Controllers;

using Microsoft.AspNetCore.Mvc;
using RiskInsure.Modernization.Reindex.Services;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure.Search.Documents;

[ApiController]
[Route("api/[controller]")]
public class ReindexController : ControllerBase
{
    private readonly IOpenAiService _openAiService;
    private readonly IIndexingService _indexingService;
    private readonly IChunkingService _chunkingService;
    private readonly ILogger<ReindexController> _logger;
    private readonly IConfiguration _config;

    public ReindexController(
        IOpenAiService openAiService,
        IIndexingService indexingService,
        IChunkingService chunkingService,
        ILogger<ReindexController> logger,
        IConfiguration config)
    {
        _openAiService = openAiService;
        _indexingService = indexingService;
        _chunkingService = chunkingService;
        _logger = logger;
        _config = config;
    }

    /// <summary>Webhook: triggered by GitHub push to platform/modernizationpatterns/**</summary>
    [HttpPost]
    public async Task<IActionResult> ReindexPatterns(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reindex webhook received");

        try
        {
            // Validate GitHub webhook signature (optional but recommended)
            if (!VerifyGitHubSignature())
            {
                _logger.LogWarning("Invalid GitHub webhook signature");
                return Unauthorized(new { error = "Invalid signature" });
            }

            // Create or update index
            await _indexingService.CreateIndexIfNotExistsAsync(cancellationToken);

            // Read pattern files
            var patternsDir = Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "content", "patterns");

            if (!Directory.Exists(patternsDir))
            {
                _logger.LogError("Patterns directory not found: {Dir}", patternsDir);
                return BadRequest(new { error = "Patterns directory not found" });
            }

            var patternFiles = Directory.GetFiles(patternsDir, "*.json");
            _logger.LogInformation("Found {PatternCount} pattern files", patternFiles.Length);

            var documents = new List<SearchDocument>();
            var totalChunks = 0;

            foreach (var file in patternFiles)
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    _logger.LogInformation("Processing pattern: {FileName}", fileName);

                    var jsonContent = await System.IO.File.ReadAllTextAsync(file, cancellationToken);
                    using var doc = JsonDocument.Parse(jsonContent);
                    var root = doc.RootElement;

                    var title = root.GetProperty("name").GetString() ?? fileName;
                    var category = root.GetProperty("category").GetString() ?? "Uncategorized";
                    var content = root.GetProperty("content").GetString() ?? "";

                    // Chunk content
                    var chunks = _chunkingService.ChunkText(content);

                    // Embed and create documents
                    for (int i = 0; i < chunks.Count; i++)
                    {
                        var chunk = chunks[i];
                        var embedding = await _openAiService.EmbedTextAsync(chunk, cancellationToken);

                        var searchDoc = new SearchDocument
                        {
                            ["id"] = $"{fileName}-chunk-{i}",
                            ["patternSlug"] = fileName,
                            ["title"] = title,
                            ["category"] = category,
                            ["content"] = chunk,
                            ["contentVector"] = embedding,
                            ["chunkIndex"] = i,
                            ["uploadedAt"] = DateTimeOffset.UtcNow
                        };

                        documents.Add(searchDoc);
                        totalChunks++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing pattern file: {File}", file);
                }
            }

            // Upsert all documents
            if (documents.Count > 0)
            {
                await _indexingService.UpsertDocumentsAsync(documents, cancellationToken);
            }

            var response = new
            {
                status = "complete",
                totalPatterns = patternFiles.Length,
                totalChunks,
                documentsIndexed = documents.Count,
                timestamp = DateTime.UtcNow
            };

            _logger.LogInformation("Reindex complete: {Response}", 
                JsonSerializer.Serialize(response));

            return Ok(response);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Reindex cancelled");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, 
                new { error = "Operation cancelled" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reindex operation failed");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = ex.Message });
        }
    }

    private bool VerifyGitHubSignature()
    {
        var secret = _config["GitHub:WebhookSecret"];
        if (string.IsNullOrEmpty(secret))
        {
            _logger.LogWarning("GitHub webhook secret not configured, skipping verification");
            return true; // Allow if not configured
        }

        if (!Request.Headers.TryGetValue("X-Hub-Signature-256", out var signature))
        {
            _logger.LogWarning("Missing X-Hub-Signature-256 header");
            return false;
        }

        // Read request body
        var bodyBytes = Encoding.UTF8.GetBytes(signature.ToString());
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(bodyBytes);
        var computed = "sha256=" + Convert.ToHexString(hash).ToLower();

        return signature.ToString().Equals(computed, StringComparison.OrdinalIgnoreCase);
    }
}
```

---

## Task 4.4: Add OpenAI Service to Reindex

Add duplicate `IOpenAiService` / `OpenAiService` from Phase 2 to reindex project (single-service responsibility).

---

## Phase 4 Checklist

- [ ] ChunkingService created & tested
- [ ] IndexingService created with index schema
- [ ] ReindexController created with POST webhook
- [ ] GitHub webhook signature validation implemented
- [ ] Pattern files successfully chunked & embedded
- [ ] Documents upser ted to AI Search index
- [ ] Local test: POST http://localhost:5000/api/reindex
- [ ] Logs show chunk count & embedding progress

**Effort**: 2 days  
**Owner**: 1 developer  
**Success Criteria**:
- POST to reindex endpoint processes all patterns
- Documents appear in AI Search index within 60 seconds
- Webhook can be called manually for testing

---

# PHASE 5: Infrastructure as Code (Days 11–13)

## Objectives
- Create Terraform modules for all Azure resources
- Configure Container Apps environment
- Set up AI Search, Cosmos DB, ACR
- Create dev/prod parameter files

---

## Task 5.1: Create Terraform Project Structure

```bash
cd platform/modernizationpatterns

mkdir -p infra/{modules/{networking,compute,search,cosmos,registry,keyvault},environments/{dev,prod}}

cd infra
```

---

## Task 5.2: Create Terraform Provider Config

**File**: `infra/providers.tf`

```hcl
terraform {
  required_version = ">= 1.0"
  
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.100"
    }
  }

  # Uncomment when ready for shared state
  # backend "azurerm" {
  #   resource_group_name  = "rg-terraform-state"
  #   storage_account_name = "tfstate12345"
  #   container_name       = "chatbot-state"
  #   key                  = "prod.tfstate"
  # }
}

provider "azurerm" {
  features {
    cognitive_account {
      purge_soft_delete_on_destroy = false
    }
  }
}
```

---

## Task 5.3: Create Root Variables

**File**: `infra/variables.tf`

```hcl
variable "environment" {
  description = "Environment name (dev/prod)"
  type        = string
  default     = "dev"
  
  validation {
    condition     = contains(["dev", "prod"], var.environment)
    error_message = "Environment must be dev or prod"
  }
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "eastus"
}

variable "project_name" {
  description = "Project name (used for resource naming)"
  type        = string
  default     = "modernization-chatbot"
}

variable "chat_api_image" {
  description = "Chat API container image URI"
  type        = string
  example     = "acrchatbot.azurecr.io/chat:latest"
}

variable "reindex_image" {
  description = "Reindex container image URI"
  type        = string
  example     = "acrchatbot.azurecr.io/reindex:latest"
}

variable "openai_endpoint" {
  description = "Azure OpenAI Service endpoint"
  type        = string
  sensitive   = true
}

variable "openai_api_key" {
  description = "Azure OpenAI API key"
  type        = string
  sensitive   = true
}

variable "search_endpoint" {
  description = "Azure AI Search endpoint"
  type        = string
  sensitive   = true
}

variable "search_api_key" {
  description = "Azure AI Search API key"
  type        = string
  sensitive   = true
}

variable "cosmos_connection_string" {
  description = "Cosmos DB connection string"
  type        = string
  sensitive   = true
}

variable "container_app_min_replicas" {
  description = "Min replicas for Container Apps"
  type        = number
  default     = 1
}

variable "container_app_max_replicas" {
  description = "Max replicas for Container Apps"
  type        = number
  default     = 10
}

variable "tags" {
  description = "Resource tags"
  type        = map(string)
  default = {
    Project     = "ModernizationPatterns"
    Environment = "Dev"
    ManagedBy   = "Terraform"
  }
}
```

---

## Task 5.4: Create Networking Module

**File**: `infra/modules/networking/main.tf`

```hcl
resource "azurerm_resource_group" "rg" {
  name     = "rg-${var.project_name}-${var.environment}"
  location = var.location
  tags     = var.tags
}

resource "azurerm_virtual_network" "vnet" {
  name                = "vnet-${var.project_name}-${var.environment}"
  address_space       = ["10.0.0.0/16"]
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  tags                = var.tags
}

resource "azurerm_subnet" "container_apps" {
  name                 = "subnet-container-apps"
  resource_group_name  = azurerm_resource_group.rg.name
  virtual_network_name = azurerm_virtual_network.vnet.name
  address_prefixes     = ["10.0.1.0/24"]
}

resource "azurerm_network_security_group" "nsg" {
  name                = "nsg-${var.project_name}"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  tags                = var.tags
}

output "resource_group_name" {
  value = azurerm_resource_group.rg.name
}

output "resource_group_id" {
  value = azurerm_resource_group.rg.id
}

output "vnet_id" {
  value = azurerm_virtual_network.vnet.id
}

output "container_apps_subnet_id" {
  value = azurerm_subnet.container_apps.id
}
```

**File**: `infra/modules/networking/variables.tf`

```hcl
variable "project_name" {
  type = string
}

variable "environment" {
  type = string
}

variable "location" {
  type = string
}

variable "tags" {
  type = map(string)
}
```

---

## Task 5.5: Create Compute Module (Container Apps)

**File**: `infra/modules/compute/main.tf`

```hcl
resource "azurerm_container_app_environment" "cae" {
  name                = "cae-${var.project_name}-${var.environment}"
  location            = var.location
  resource_group_name = var.resource_group_name
  tags                = var.tags

  infrastructure_subnet_id = var.container_apps_subnet_id
}

resource "azurerm_container_app" "chat" {
  name                         = "ca-chat-${var.environment}"
  container_app_environment_id = azurerm_container_app_environment.cae.id
  resource_group_name          = var.resource_group_name
  revision_mode                = "Single"
  tags                         = var.tags

  template {
    container {
      name   = "chat"
      image  = var.chat_api_image
      cpu    = 0.5
      memory = "1Gi"

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }

      env {
        name  = "AzureOpenAI__Endpoint"
        value = var.openai_endpoint
      }

      env {
        name        = "AzureOpenAI__ApiKey"
        secret_name = "openai-key"
      }

      env {
        name  = "AzureSearch__Endpoint"
        value = var.search_endpoint
      }

      env {
        name        = "AzureSearch__ApiKey"
        secret_name = "search-key"
      }

      env {
        name        = "ConnectionStrings__CosmosDb"
        secret_name = "cosmos-connection"
      }
    }

    scale {
      min_replicas = var.min_replicas
      max_replicas = var.max_replicas

      rules = [
        {
          name             = "http-concurrency"
          custom_rule_type = "http"
          http_concurrency = 100
        }
      ]
    }
  }

  ingress {
    external_enabled = true
    target_port      = 3000

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  secret {
    name  = "openai-key"
    value = var.openai_api_key
  }

  secret {
    name  = "search-key"
    value = var.search_api_key
  }

  secret {
    name  = "cosmos-connection"
    value = var.cosmos_connection
  }
}

resource "azurerm_container_app" "reindex" {
  name                         = "ca-reindex-${var.environment}"
  container_app_environment_id = azurerm_container_app_environment.cae.id
  resource_group_name          = var.resource_group_name
  revision_mode                = "Single"
  tags                         = var.tags

  template {
    container {
      name   = "reindex"
      image  = var.reindex_image
      cpu    = 1.0
      memory = "2Gi"

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = "Production"
      }

      env {
        name  = "AzureOpenAI__Endpoint"
        value = var.openai_endpoint
      }

      env {
        name        = "AzureOpenAI__ApiKey"
        secret_name = "openai-key"
      }

      env {
        name  = "AzureSearch__Endpoint"
        value = var.search_endpoint
      }

      env {
        name        = "AzureSearch__ApiKey"
        secret_name = "search-key"
      }
    }

    scale {
      min_replicas = 0
      max_replicas = 5

      rules = [
        {
          name             = "http-concurrency"
          custom_rule_type = "http"
          http_concurrency = 50
        }
      ]
    }
  }

  ingress {
    external_enabled = true
    target_port      = 3000

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  secret {
    name  = "openai-key"
    value = var.openai_api_key
  }

  secret {
    name  = "search-key"
    value = var.search_api_key
  }
}

output "chat_endpoint" {
  value = azurerm_container_app.chat.latest_revision_fqdn
}

output "reindex_endpoint" {
  value = azurerm_container_app.reindex.latest_revision_fqdn
}
```

**File**: `infra/modules/compute/variables.tf`

```hcl
variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "project_name" {
  type = string
}

variable "environment" {
  type = string
}

variable "container_apps_subnet_id" {
  type = string
}

variable "chat_api_image" {
  type = string
}

variable "reindex_image" {
  type = string
}

variable "openai_endpoint" {
  type      = string
  sensitive = true
}

variable "openai_api_key" {
  type      = string
  sensitive = true
}

variable "search_endpoint" {
  type      = string
  sensitive = true
}

variable "search_api_key" {
  type      = string
  sensitive = true
}

variable "cosmos_connection" {
  type      = string
  sensitive = true
}

variable "min_replicas" {
  type    = number
  default = 1
}

variable "max_replicas" {
  type    = number
  default = 10
}

variable "tags" {
  type = map(string)
}
```

---

## Task 5.6: Create Search Module

**File**: `infra/modules/search/main.tf`

```hcl
resource "azurerm_search_service" "search" {
  name                = "search-${var.project_name}-${var.environment}"
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = var.sku
  tags                = var.tags

  public_network_access_enabled = true
}

output "search_endpoint" {
  value = azurerm_search_service.search.endpoint
}

output "search_name" {
  value = azurerm_search_service.search.name
}

output "search_id" {
  value = azurerm_search_service.search.id
}
```

**File**: `infra/modules/search/variables.tf`

```hcl
variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "project_name" {
  type = string
}

variable "environment" {
  type = string
}

variable "sku" {
  description = "SKU for Azure Search (basic, standard, etc.)"
  type        = string
  default     = "basic"
}

variable "tags" {
  type = map(string)
}
```

---

## Task 5.7: Create Cosmos DB Module

**File**: `infra/modules/cosmos/main.tf`

```hcl
resource "azurerm_cosmosdb_account" "cosmos" {
  name                = "cosmos-${var.project_name}-${var.environment}"
  location            = var.location
  resource_group_name = var.resource_group_name
  offer_type          = "Standard"
  kind                = "GlobalDocumentDB"
  tags                = var.tags

  capabilities {
    name = "EnableServerless"
  }

  consistency_policy {
    consistency_level = "Session"
  }

  geo_location {
    location          = var.location
    failover_priority = 0
  }
}

resource "azurerm_cosmosdb_sql_database" "db" {
  name                = "${var.project_name}-db"
  resource_group_name = azurerm_cosmosdb_account.cosmos.resource_group_name
  account_name        = azurerm_cosmosdb_account.cosmos.name
  throughput          = null # Serverless
}

resource "azurerm_cosmosdb_sql_container" "conversations" {
  name                = "conversations"
  database_name       = azurerm_cosmosdb_sql_database.db.name
  resource_group_name = azurerm_cosmosdb_account.cosmos.resource_group_name
  account_name        = azurerm_cosmosdb_account.cosmos.name
  partition_key_path  = "/userId"
}

output "cosmos_endpoint" {
  value = azurerm_cosmosdb_account.cosmos.endpoint
}

output "cosmos_connection_string" {
  value     = azurerm_cosmosdb_account.cosmos.primary_sql_connection_string
  sensitive = true
}

output "cosmos_id" {
  value = azurerm_cosmosdb_account.cosmos.id
}
```

**File**: `infra/modules/cosmos/variables.tf`

```hcl
variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "project_name" {
  type = string
}

variable "environment" {
  type = string
}

variable "tags" {
  type = map(string)
}
```

---

## Task 5.8: Create Container Registry Module

**File**: `infra/modules/registry/main.tf`

```hcl
resource "azurerm_container_registry" "acr" {
  name                = "acr${replace(var.project_name, "-", "")}${var.environment}"
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = var.sku
  admin_enabled       = true
  tags                = var.tags
}

output "acr_endpoint" {
  value = azurerm_container_registry.acr.login_server
}

output "acr_id" {
  value = azurerm_container_registry.acr.id
}

output "acr_username" {
  value     = azurerm_container_registry.acr.admin_username
  sensitive = true
}

output "acr_password" {
  value     = azurerm_container_registry.acr.admin_password
  sensitive = true
}
```

**File**: `infra/modules/registry/variables.tf`

```hcl
variable "resource_group_name" {
  type = string
}

variable "location" {
  type = string
}

variable "project_name" {
  type = string
}

variable "environment" {
  type = string
}

variable "sku" {
  type    = string
  default = "Basic"
}

variable "tags" {
  type = map(string)
}
```

---

## Task 5.9: Create Root Main.tf (Orchestration)

**File**: `infra/main.tf`

```hcl
module "networking" {
  source = "./modules/networking"

  project_name = var.project_name
  environment  = var.environment
  location     = var.location
  tags         = var.tags
}

module "registry" {
  source = "./modules/registry"

  resource_group_name = module.networking.resource_group_name
  location            = var.location
  project_name        = var.project_name
  environment         = var.environment
  tags                = var.tags
}

module "search" {
  source = "./modules/search"

  resource_group_name = module.networking.resource_group_name
  location            = var.location
  project_name        = var.project_name
  environment         = var.environment
  sku                 = var.environment == "prod" ? "standard" : "basic"
  tags                = var.tags
}

module "cosmos" {
  source = "./modules/cosmos"

  resource_group_name = module.networking.resource_group_name
  location            = var.location
  project_name        = var.project_name
  environment         = var.environment
  tags                = var.tags
}

module "compute" {
  source = "./modules/compute"

  resource_group_name         = module.networking.resource_group_name
  location                    = var.location
  project_name                = var.project_name
  environment                 = var.environment
  container_apps_subnet_id    = module.networking.container_apps_subnet_id
  chat_api_image              = var.chat_api_image
  reindex_image               = var.reindex_image
  openai_endpoint             = var.openai_endpoint
  openai_api_key              = var.openai_api_key
  search_endpoint             = module.search.search_endpoint
  search_api_key              = var.search_api_key
  cosmos_connection           = module.cosmos.cosmos_connection_string
  min_replicas                = var.container_app_min_replicas
  max_replicas                = var.container_app_max_replicas
  tags                        = var.tags

  depends_on = [module.networking]
}

output "chat_api_endpoint" {
  value       = "https://${module.compute.chat_endpoint}"
  description = "Chat API endpoint URL"
}

output "reindex_endpoint" {
  value       = "https://${module.compute.reindex_endpoint}"
  description = "Reindex service endpoint URL"
}

output "search_endpoint" {
  value       = module.search.search_endpoint
  description = "Azure AI Search endpoint"
}

output "resource_group_name" {
  value = module.networking.resource_group_name
}
```

---

## Task 5.10: Create Environment Parameter Files

**File**: `infra/environments/dev/terraform.tfvars`

```hcl
environment  = "dev"
location     = "eastus"
project_name = "modernization-chatbot"

# Container images (will be updated by CI/CD)
chat_api_image = "acrchatbotdev.azurecr.io/chat:latest"
reindex_image  = "acrchatbotdev.azurecr.io/reindex:latest"

# Retrieve these from Azure portal or Azure CLI
openai_endpoint           = "https://YOUR-OPENAI.openai.azure.com/"
openai_api_key            = "YOUR-OPENAI-KEY"
search_api_key            = "YOUR-SEARCH-KEY"
cosmos_connection_string  = "YOUR-COSMOS-CONNECTION"

container_app_min_replicas = 1
container_app_max_replicas = 3

tags = {
  Environment = "Developer"
  Project     = "ModernizationPatterns"
  ManagedBy   = "Terraform"
}
```

**File**: `infra/environments/dev/backend.tf`

```hcl
terraform {
  backend "local" {
    # For development: local state file
    path = "terraform.tfstate"
  }
}

# For production: use Azure Storage
# terraform {
#   backend "azurerm" {
#     resource_group_name  = "rg-terraform-state"
#     storage_account_name = "tfstate12345"
#     container_name       = "chatbot-state"
#     key                  = "dev.tfstate"
#   }
# }
```

---

## Task 5.11: Initialize and Deploy Terraform

```bash
cd infra/environments/dev

# Initialize
terraform init

# Validate
terraform validate

# Plan (review changes)
terraform plan -var-file="terraform.tfvars" -out=tfplan

# Review tfplan carefully ⚠️

# Apply
terraform apply tfplan

# Get outputs
terraform output
```

---

## Phase 5 Checklist

- [ ] All Terraform modules created (networking, compute, search, cosmos, registry)
- [ ] Root main.tf orchestrates all modules
- [ ] Variables defined for all inputs
- [ ] Dev environment parameters created
- [ ] `terraform init` succeeds
- [ ] `terraform validate` succeeds
- [ ] `terraform plan` shows 10-12 resources
- [ ] `terraform apply` succeeds (first time takes ~5-10 mins)
- [ ] Outputs show chat API & reindex endpoints
- [ ] Resources visible in Azure Portal

**Effort**: 3 days  
**Owner**: Infrastructure/DevOps engineer  
**Success Criteria**:
- Terraform plan shows all resources
- No TF errors or validation failures
- Resources deployed to Azure
- Endpoints accessible (may return 503 until images pushed)

---

# PHASE 6: Docker & Local Development (Days 14–15)

## Objectives
- Build Docker images for Chat & Reindex services
- Create docker-compose for local development
- Test locally before pushing to ACR

---

## Task 6.1: Create Dockerfile for Chat API

**File**: `api/chat/Dockerfile`

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder
WORKDIR /build

# Copy project files
COPY ["api/chat/Chat.Api.csproj", "./"]
COPY ["Directory.Build.props", "Directory.Packages.props", "/"]

# Restore
RUN dotnet restore

# Copy source
COPY ["api/chat/src/", "./src/"]

# Publish
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy from builder
COPY --from=builder /app/publish .

EXPOSE 3000
ENV ASPNETCORE_URLS="http://+:3000"
ENV ASPNETCORE_ENVIRONMENT="Production"

ENTRYPOINT ["dotnet", "Chat.Api.dll"]
```

---

## Task 6.2: Create Dockerfile for Reindex Service

**File**: `api/reindex/Dockerfile`

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder
WORKDIR /build

# Copy project files
COPY ["api/reindex/Reindex.Endpoint.In.csproj", "./"]
COPY ["Directory.Build.props", "Directory.Packages.props", "/"]

# Restore
RUN dotnet restore

# Copy source + content directory
COPY ["api/reindex/src/", "./src/"]
COPY ["content/", "/app/content/"]

# Publish
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy from builder
COPY --from=builder /app/publish .
COPY --from=builder /app/content /app/content

EXPOSE 3000
ENV ASPNETCORE_URLS="http://+:3000"
ENV ASPNETCORE_ENVIRONMENT="Production"

ENTRYPOINT ["dotnet", "Reindex.Endpoint.In.dll"]
```

---

## Task 6.3: Create docker-compose for Local Development

**File**: `docker-compose.yml` (root)

```yaml
version: '3.8'

services:

  cosmos-emulator:
    image: mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest
    ports:
      - "8081:8081"
    environment:
      AZURE_COSMOS_EMULATOR_PARTITION_COUNT: 10
    volumes:
      - cosmos-data:/data/db
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8081/status"]
      interval: 5s
      timeout: 3s
      retries: 5
    networks:
      - chatbot

  chat-api:
    build:
      context: .
      dockerfile: platform/modernizationpatterns/api/chat/Dockerfile
    ports:
      - "3000:3000"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__CosmosDb: "AccountEndpoint=https://cosmos-emulator:8081/;AccountKey=C2y6yDjf5/D+4vIrQnrC1+gXK9YdJFAqkZDyqkJQW8=..."
      AzureOpenAI__Endpoint: "${AZURE_OPENAI_ENDPOINT}"
      AzureOpenAI__ApiKey: "${AZURE_OPENAI_API_KEY}"
      AzureOpenAI__DeploymentName: "gpt-4o"
      AzureSearch__Endpoint: "${AZURE_SEARCH_ENDPOINT}"
      AzureSearch__ApiKey: "${AZURE_SEARCH_API_KEY}"
      AzureSearch__IndexName: "modernization-patterns"
      Logging__LogLevel__Default: "Information"
    depends_on:
      cosmos-emulator:
        condition: service_healthy
    networks:
      - chatbot
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:3000/health"]
      interval: 10s
      timeout: 5s
      retries: 3

  reindex-service:
    build:
      context: .
      dockerfile: platform/modernizationpatterns/api/reindex/Dockerfile
    ports:
      - "3001:3000"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      AzureOpenAI__Endpoint: "${AZURE_OPENAI_ENDPOINT}"
      AzureOpenAI__ApiKey: "${AZURE_OPENAI_API_KEY}"
      AzureOpenAI__DeploymentName: "gpt-4o"
      AzureSearch__Endpoint: "${AZURE_SEARCH_ENDPOINT}"
      AzureSearch__ApiKey: "${AZURE_SEARCH_API_KEY}"
      AzureSearch__IndexName: "modernization-patterns"
      Logging__LogLevel__Default: "Information"
    depends_on:
      - chat-api
    networks:
      - chatbot

volumes:
  cosmos-data:

networks:
  chatbot:
    driver: bridge
```

---

## Task 6.4: Create .env.example for Local Development

**File**: `.env.example`

```bash
# Azure OpenAI  
export AZURE_OPENAI_ENDPOINT=https://YOUR-RESOURCE.openai.azure.com/
export AZURE_OPENAI_API_KEY=YOUR_KEY_HERE

# Azure AI Search
export AZURE_SEARCH_ENDPOINT=https://YOUR-SEARCH.search.windows.net/
export AZURE_SEARCH_API_KEY=YOUR_KEY_HERE

# GitHub Webhook (optional)
export GITHUB_WEBHOOK_SECRET=YOUR_SECRET_HERE
```

**Usage**: 
```bash
cp .env.example .env
# Edit .env with your values
source .env
docker-compose up
```

---

## Task 6.5: Build & Test Locally

```bash
# Copy env template
cp .env.example .env
# Edit .env with your Azure credentials

# Build images
docker-compose build

# Start services (Cosmos, Chat, Reindex)
docker-compose up

# In another terminal: Test endpoints
curl -X POST http://localhost:3000/api/chat/stream \
  -H "Content-Type: application/json" \
  -d '{
    "message": "What is the strangler pattern?",
    "conversationId": "conv-test-123",
    "userId": "test@example.com"
  }'

# Test reindex endpoint
curl -X POST http://localhost:3001/api/reindex

# Check logs
docker-compose logs -f chat-api
docker-compose logs -f reindex-service
```

---

## Phase 6 Checklist

- [ ] Dockerfile created for Chat API
- [ ] Dockerfile created for Reindex service
- [ ] docker-compose.yml created with all services
- [ ] .env.example created with required variables
- [ ] `docker-compose build` succeeds
- [ ] `docker-compose up` starts all services
- [ ] Chat endpoint responds to POST requests
- [ ] Reindex endpoint responds to webhooks
- [ ] Cosmos Emulator running locally
- [ ] Logs show no critical errors

**Effort**: 2 days  
**Owner**: 1 developer  
**Success Criteria**:
- All containers start without errors
- Chat & Reindex endpoints respond
- Data persists in Cosmos Emulator

---

# PHASE 7: CI/CD Pipeline (Days 16–17)

## Objectives
- Create GitHub Actions workflow
- Build & push images to ACR on every push
- Deploy to Container Apps automatically

---

## Task 7.1: Create GitHub Actions Workflow

**File**: `.github/workflows/chatbot-build-deploy.yml`

```yaml
name: Chatbot Build & Deploy

on:
  push:
    branches: [ main, develop ]
    paths:
      - 'platform/modernizationpatterns/api/**'
      - 'platform/modernizationpatterns/content/**'
      - '.github/workflows/chatbot-build-deploy.yml'

env:
  ACR_REGISTRY: acrchatbot
  CHAT_IMAGE_NAME: chat
  REINDEX_IMAGE_NAME: reindex
  AZURE_RG: rg-modernization-chatbot-prod

jobs:

  build-and-push:
    runs-on: ubuntu-latest
    
    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Set up .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore dependencies
        run: dotnet restore RiskInsure.slnx

      - name: Build Chat API
        run: dotnet build -c Release --no-restore \
          platform/modernizationpatterns/api/chat/Chat.Api.csproj

      - name: Build Reindex Service
        run: dotnet build -c Release --no-restore \
          platform/modernizationpatterns/api/reindex/Reindex.Endpoint.In.csproj

      - name: Run tests
        run: dotnet test --no-build --verbosity normal || true

      - name: Login to ACR
        uses: azure/docker-login@v1
        with:
          login-server: ${{ env.ACR_REGISTRY }}.azurecr.io
          username: ${{ secrets.ACR_USERNAME }}
          password: ${{ secrets.ACR_PASSWORD }}

      - name: Build Chat image
        run: |
          docker build \
            -f platform/modernizationpatterns/api/chat/Dockerfile \
            -t ${{ env.ACR_REGISTRY }}.azurecr.io/${{ env.CHAT_IMAGE_NAME }}:${{ github.sha }} \
            -t ${{ env.ACR_REGISTRY }}.azurecr.io/${{ env.CHAT_IMAGE_NAME }}:latest \
            .

      - name: Push Chat image
        run: |
          docker push ${{ env.ACR_REGISTRY }}.azurecr.io/${{ env.CHAT_IMAGE_NAME }}:${{ github.sha }}
          docker push ${{ env.ACR_REGISTRY }}.azurecr.io/${{ env.CHAT_IMAGE_NAME }}:latest

      - name: Build Reindex image
        run: |
          docker build \
            -f platform/modernizationpatterns/api/reindex/Dockerfile \
            -t ${{ env.ACR_REGISTRY }}.azurecr.io/${{ env.REINDEX_IMAGE_NAME }}:${{ github.sha }} \
            -t ${{ env.ACR_REGISTRY }}.azurecr.io/${{ env.REINDEX_IMAGE_NAME }}:latest \
            .

      - name: Push Reindex image
        run: |
          docker push ${{ env.ACR_REGISTRY }}.azurecr.io/${{ env.REINDEX_IMAGE_NAME }}:${{ github.sha }}
          docker push ${{ env.ACR_REGISTRY }}.azurecr.io/${{ env.REINDEX_IMAGE_NAME }}:latest

  deploy:
    needs: build-and-push
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'

    steps:
      - name: Azure Login
        uses: azure/login@v1
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}

      - name: Update Chat Container App
        uses: azure/CLI@v1
        with:
          inlineScript: |
            az containerapp update \
              -n ca-chat-prod \
              -g ${{ env.AZURE_RG }} \
              --image ${{ env.ACR_REGISTRY }}.azurecr.io/${{ env.CHAT_IMAGE_NAME }}:${{ github.sha }}

      - name: Update Reindex Container App
        uses: azure/CLI@v1
        with:
          inlineScript: |
            az containerapp update \
              -n ca-reindex-prod \
              -g ${{ env.AZURE_RG }} \
              --image ${{ env.ACR_REGISTRY }}.azurecr.io/${{ env.REINDEX_IMAGE_NAME }}:${{ github.sha }}

  test-deployment:
    needs: deploy
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Wait for Container Apps to be ready
        run: sleep 30

      - name: Test Chat endpoint
        run: |
          CHAT_ENDPOINT=$(az containerapp show \
            -n ca-chat-prod \
            -g ${{ env.AZURE_RG }} \
            --query properties.configuration.ingress.fqdn -o tsv)
          
          curl -X POST https://${CHAT_ENDPOINT}/api/chat/new \
            -H "Content-Type: application/json" \
            --fail --retry 3 || exit 1
```

---

## Task 7.2: Set GitHub Repository Secrets

Go to repository settings and add:
- `ACR_USERNAME` — Container Registry username
- `ACR_PASSWORD` — Container Registry password
- `AZURE_CREDENTIALS` — Service Principal credentials (JSON format)

---

## Phase 7 Checklist

- [ ] GitHub Actions workflow created
- [ ] Secrets configured in repository
- [ ] Workflow triggers on push to main
- [ ] Build & push to ACR succeeds
- [ ] Container Apps update automatically
- [ ] Post-deployment tests pass

**Effort**: 2 days  
**Owner**: DevOps engineer  
**Success Criteria**:
- Workflow runs on every push to main
- Images pushed to ACR within 5 minutes
- Container Apps updated automatically
- Zero-downtime deployment

---

# PHASE 8: Frontend Integration (Days 18–19)

## Objectives
- Add ChatWidget to React SPA
- Implement SSE streaming UI
- Integrate with Auth/JWT tokens
- Handle error states

---

## Task 8.1: Create ChatWidget Component

**File**: `platform/modernizationpatterns/src/components/ChatWidget.jsx`

```jsx
import { useState, useRef, useEffect } from 'react';
import '../styles/chat-widget.css';

export function ChatWidget() {
  const [isOpen, setIsOpen] = useState(false);
  const [messages, setMessages] = useState([]);
  const [input, setInput] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [conversationId] = useState(generateId());
  const [userId] = useState('user@riskinsure.com'); // From auth context
  const messagesEndRef = useRef(null);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  useEffect(() => {
    scrollToBottom();
  }, [messages]);

  const handleSendMessage = async (e) => {
    e.preventDefault();
    if (!input.trim() || isLoading) return;

    const userMessage = { role: 'user', content: input };
    setMessages(prev => [...prev, userMessage]);
    setInput('');
    setIsLoading(true);

    try {
      const response = await fetch('/api/chat/stream', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          message: input,
          conversationId,
          userId
        })
      });

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let fullContent = '';
      const assistantMessage = { role: 'assistant', content: '' };

      setMessages(prev => [...prev, assistantMessage]);

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        const chunk = decoder.decode(value);
        const lines = chunk.split('\n');

        for (const line of lines) {
          if (line.startsWith('data: ')) {
            const data = line.substring(6);
            if (data === '[DONE]') break;

            try {
              const parsed = JSON.parse(data);
              if (parsed.delta) {
                fullContent += parsed.delta;
                setMessages(prev => {
                  const updated = [...prev];
                  updated[updated.length - 1].content = fullContent;
                  return updated;
                });
              } else if (parsed.citations) {
                // Add citations to last message
                setMessages(prev => {
                  const updated = [...prev];
                  updated[updated.length - 1].citations = parsed.citations;
                  return updated;
                });
              }
            } catch (e) {
              console.debug('Parse error (expected)', e);
            }
          }
        }
      }
    } catch (error) {
      console.error('Chat error:', error);
      setMessages(prev => [...prev, { 
        role: 'system', 
        content: `Error: ${error.message}`,
        isError: true 
      }]);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="chat-widget">
      {/* Floating Button */}
      <button 
        className="chat-toggle-btn"
        onClick={() => setIsOpen(!isOpen)}
        title="Chat with Patterns Assistant"
      >
        💬
      </button>

      {/* Chat Panel */}
      {isOpen && (
        <div className="chat-panel">
          <div className="chat-header">
            <h3>Modernization Patterns Assistant</h3>
            <button 
              className="close-btn"
              onClick={() => setIsOpen(false)}
            >
              ×
            </button>
          </div>

          <div className="chat-messages">
            {messages.length === 0 && (
              <div className="empty-state">
                <p>Ask me anything about modernization patterns!</p>
              </div>
            )}
            
            {messages.map((msg, idx) => (
              <div key={idx} className={`message message-${msg.role}`}>
                <div className="message-content">
                  {msg.content}
                </div>
                {msg.citations && (
                  <div className="citations">
                    <strong>Sources:</strong>
                    {msg.citations.map((c, i) => (
                      <a key={i} href={`/pattern/${c}`}>
                        {c}
                      </a>
                    ))}
                  </div>
                )}
              </div>
            ))}
            
            {isLoading && (
              <div className="message message-assistant loading">
                <span></span><span></span><span></span>
              </div>
            )}

            <div ref={messagesEndRef} />
          </div>

          <form className="chat-input-form" onSubmit={handleSendMessage}>
            <input
              type="text"
              value={input}
              onChange={(e) => setInput(e.target.value)}
              placeholder="Ask about patterns..."
              disabled={isLoading}
              autoFocus
            />
            <button type="submit" disabled={isLoading || !input.trim()}>
              Send
            </button>
          </form>
        </div>
      )}
    </div>
  );
}

function generateId() {
  return 'conv-' + Math.random().toString(36).substr(2, 9);
}
```

---

## Task 8.2: Create ChatWidget Styles

**File**: `platform/modernizationpatterns/src/styles/chat-widget.css`

```css
.chat-widget {
  position: fixed;
  bottom: 20px;
  right: 20px;
  z-index: 9999;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
}

.chat-toggle-btn {
  width: 60px;
  height: 60px;
  border-radius: 50%;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  border: none;
  color: white;
  font-size: 24px;
  cursor: pointer;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  transition: all 0.3s ease;
}

.chat-toggle-btn:hover {
  transform: scale(1.1);
  box-shadow: 0 6px 20px rgba(0, 0, 0, 0.2);
}

.chat-panel {
  position: absolute;
  bottom: 80px;
  right: 0;
  width: 400px;
  height: 600px;
  background: white;
  border-radius: 12px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.1);
  display: flex;
  flex-direction: column;
  animation: slideUp 0.3s ease;
}

@keyframes slideUp {
  from {
    opacity: 0;
    transform: translateY(20px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.chat-header {
  padding: 16px;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  border-radius: 12px 12px 0 0;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.chat-header h3 {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
}

.close-btn {
  background: none;
  border: none;
  color: white;
  font-size: 24px;
  cursor: pointer;
  padding: 0;
  width: 24px;
  height: 24px;
}

.chat-messages {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.empty-state {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: #999;
  text-align: center;
}

.message {
  display: flex;
  flex-direction: column;
  gap: 8px;
  animation: fadeIn 0.2s ease;
}

@keyframes fadeIn {
  from {
    opacity: 0;
  }
  to {
    opacity: 1;
  }
}

.message-user {
  align-self: flex-end;
}

.message-user .message-content {
  background: #667eea;
  color: white;
  padding: 12px 16px;
  border-radius: 12px;
  max-width: 80%;
  word-wrap: break-word;
}

.message-assistant .message-content {
  background: #f0f0f0;
  color: #333;
  padding: 12px 16px;
  border-radius: 12px;
  max-width: 80%;
  word-wrap: break-word;
  line-height: 1.5;
}

.message-system {
  align-self: center;
}

.message-system .message-content {
  background: #ffe0e0;
  color: #d00;
  padding: 8px 12px;
  border-radius: 6px;
  font-size: 14px;
}

.message.loading .message-content {
  display: flex;
  gap: 4px;
  align-items: center;
}

.message.loading span {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #667eea;
  animation: pulse 1.4s infinite;
}

.message.loading span:nth-child(2) {
  animation-delay: 0.2s;
}

.message.loading span:nth-child(3) {
  animation-delay: 0.4s;
}

@keyframes pulse {
  0%, 60%, 100% {
    opacity: 0.5;
  }
  30% {
    opacity: 1;
  }
}

.citations {
  font-size: 12px;
  color: #666;
  border-top: 1px solid #ddd;
  padding-top: 8px;
  margin-top: 8px;
}

.citations strong {
  display: block;
  margin-bottom: 4px;
}

.citations a {
  display: inline-block;
  margin-right: 8px;
  color: #667eea;
  text-decoration: none;
}

.citations a:hover {
  text-decoration: underline;
}

.chat-input-form {
  display: flex;
  gap: 8px;
  padding: 16px;
  border-top: 1px solid #e0e0e0;
}

.chat-input-form input {
  flex: 1;
  border: 1px solid #ddd;
  border-radius: 6px;
  padding: 10px 12px;
  font-size: 14px;
  font-family: inherit;
}

.chat-input-form input:focus {
  outline: none;
  border-color: #667eea;
  box-shadow: 0 0 0 2px rgba(102, 126, 234, 0.1);
}

.chat-input-form button {
  background: #667eea;
  color: white;
  border: none;
  border-radius: 6px;
  padding: 10px 16px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}

.chat-input-form button:hover:not(:disabled) {
  background: #764ba2;
}

.chat-input-form button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

@media (max-width: 600px) {
  .chat-panel {
    width: 90vw;
    height: 70vh;
    bottom: 70px;
    right: 5vw;
  }

  .message-user .message-content,
  .message-assistant .message-content {
    max-width: 100%;
  }
}
```

---

## Task 8.3: Integrate ChatWidget into App

**File**: `platform/modernizationpatterns/src/App.jsx`

```jsx
import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import { ChatWidget } from './components/ChatWidget';
import { HomePage } from './routes/Home';
import { PatternDetail } from './routes/PatternDetail';
import './styles/app.css';

function App() {
  return (
    <Router>
      <div className="app">
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/pattern/:slug" element={<PatternDetail />} />
        </Routes>
        
        {/* Add ChatWidget to all pages */}
        <ChatWidget />
      </div>
    </Router>
  );
}

export default App;
```

---

## Phase 8 Checklist

- [ ] ChatWidget component created
- [ ] CSS styles complete (responsive, animations)
- [ ] SSE streaming works in UI
- [ ] Error handling displays properly
- [ ] Citations render with links
- [ ] Mobile-friendly layout
- [ ] Accessibility features (focus, keyboard nav)
- [ ] Integrated into App.jsx
- [ ] Frontend build succeeds (`npm run build`)

**Effort**: 2 days  
**Owner**: Frontend developer  
**Success Criteria**:
- ChatWidget appears on every page
- Sends messages to `/api/chat/stream`
- SSE chunks display as they arrive
- Responsive on mobile

---

# PHASE 9: Testing, Monitoring, & Go-Live (Days 20–21)

## Objectives
- End-to-end integration testing
- Load testing
- Monitoring & alerting setup
- Production hardening

---

## Task 9.1: Create Integration Tests

**File**: `api/chat/test/Integration.Tests/ChatIntegrationTests.cs`

```csharp
namespace RiskInsure.Modernization.Chat.Tests.Integration;

using Xunit;

public class ChatIntegrationTests : IAsyncLifetime
{
    private HttpClient _client = null!;
    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();

        // Wait for services to be healthy
        await Task.Delay(2000);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task ChatEndpoint_WithValidRequest_ReturnsStreamingResponse()
    {
        // Arrange
        var request = new
        {
            message = "What is the strangler pattern?",
            conversationId = "test-conv-123",
            userId = "test@example.com"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat/stream", request);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task NewConversation_CreatesConversation_WithId()
    {
        // Act
        var response = await _client.PostAsync("/api/chat/new?userId=test@example.com", null);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
    }
}
```

Run tests:
```bash
dotnet test api/chat/test/Unit.Tests/
dotnet test api/chat/test/Integration.Tests/
```

---

## Task 9.2: Create Monitoring & Logging

Add to `Program.cs` (Chat API):

```csharp
// Application Insights
var appInsightsKey = builder.Configuration["ApplicationInsights:InstrumentationKey"];
if (!string.IsNullOrEmpty(appInsightsKey))
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.InstrumentationKey = appInsightsKey;
    });
}
```

---

## Task 9.3: Load Testing Script

**File**: `scripts/load-test.js`

```javascript
import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  stages: [
    { duration: '2m', target: 50 },
    { duration: '5m', target: 100 },
    { duration: '2m', target: 0 },
  ],
  thresholds: {
    http_req_duration: ['p(95)<2000', 'p(99)<3000'],
    http_req_failed: ['rate<0.01'],
  },
};

const BASE_URL = __ENV.BASE_URL || 'http://localhost:3000';

export default function () {
  const payload = JSON.stringify({
    message: 'What is the strangler pattern?',
    conversationId: `conv-${Math.random().toString(36).substr(2)}`,
    userId: 'load-test@example.com',
  });

  const params = {
    headers: {
      'Content-Type': 'application/json',
    },
  };

  const res = http.post(`${BASE_URL}/api/chat/stream`, payload, params);

  check(res, {
    'status is 200': (r) => r.status === 200,
    'response time < 5s': (r) => r.timings.duration < 5000,
    'content type is event-stream': (r) => r.headers['Content-Type'].includes('text/event-stream'),
  });

  sleep(1);
}
```

Run: `k6 run scripts/load-test.js --vus 50 --duration 30s`

---

## Task 9.4: Deployment Checklist

## Phase 9 Checklist

- [ ] Unit tests pass (>80% coverage)
- [ ] Integration tests pass
- [ ] Load test completed (100+ concurrent users)
- [ ] No errors in logs
- [ ] Response times < 2s p95
- [ ] Error rate < 1%
- [ ] Monitoring dashboards created
- [ ] Alert rules configured
- [ ] Runbook documentation complete
- [ ] Team trained on procedures
- [ ] Soft launch with pilot users
- [ ] Full production rollout

**Effort**: 2 days  
**Owner**: QA + DevOps  
**Success Criteria**:
- All tests pass
- Zero errors on sustained 100 qps load
- 99.9% uptime

---

# Deployment Checklist

Before going live, verify:

## Code Readiness
- [ ] All projects build without errors
- [ ] Code review approved
- [ ] Security scan passed (OWASP, dependencies)
- [ ] Secrets not in code (all in Key Vault)

## Infrastructure Ready
- [ ] Terraform validated & applied
- [ ] All Azure resources created
- [ ] Managed Identity configured
- [ ] Network policies applied

## Testing Complete
- [ ] Unit tests 85%+ coverage
- [ ] Integration tests pass
- [ ] Load test: 100 qps, p95 < 2s
- [ ] Chat response makes sense
- [ ] Error handling works

## Monitoring Enabled
- [ ] Application Insights connected
- [ ] Alerts configured (error rate, latency)
- [ ] Dashboards created
- [ ] Log queries saved

## Documentation Done
- [ ] Runbook created (incident response)
- [ ] API docs generated (Swagger)
- [ ] Architecture doc updated
- [ ] Team trained

---

# Quick Reference

## Repository Structure (Final)

```
platform/modernizationpatterns/
├── api/
│   ├── chat/
│   │   ├── src/
│   │   │   ├── Controllers/ChatController.cs
│   │   │   ├── Services/{OpenAiService, SearchService, ConversationService}
│   │   │   ├── Models/{Conversation, Message}
│   │   │   └── Program.cs
│   │   ├── test/Unit.Tests/
│   │   ├── Chat.Api.csproj
│   │   ├── Dockerfile
│   │   └── appsettings.json
│   │
│   └── reindex/
│       ├── src/
│       │   ├── Controllers/ReindexController.cs
│       │   ├── Services/{ChunkingService, IndexingService, OpenAiService}
│       │   ├── Models/
│       │   └── Program.cs
│       ├── Reindex.Endpoint.In.csproj
│       ├── Dockerfile
│       └── appsettings.json
│
├── infra/
│   ├── modules/
│   │   ├── networking/
│   │   ├── compute/
│   │   ├── search/
│   │   ├── cosmos/
│   │   └── registry/
│   ├── main.tf
│   ├── variables.tf
│   ├── providers.tf
│   └── environments/
│       ├── dev/
│       └── prod/
│
├── src/
│   ├── components/ChatWidget.jsx
│   ├── styles/chat-widget.css
│   └── App.jsx
│
├── content/patterns/*.json
├── docs/
│   ├── ai-chatbot-architecture.md
│   └── RUNBOOK.md
├── docker-compose.yml
├── .github/workflows/chatbot-build-deploy.yml
└── IMPLEMENTATION-PLAN.md (this file)
```

## Key Commands

```bash
# Local development
docker-compose up

# Build & test
dotnet build
dotnet test

# Terraform
cd infra/environments/dev
terraform plan
terraform apply

# Deploy frontend
cd platform/modernizationpatterns
npm run build

# Push images to ACR (via GitHub Actions)
git push origin main  # Triggers workflow automatically
```

## Endpoint Summary

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/chat/stream` | POST | Stream chat responses (SSE) |
| `/api/chat/{id}` | GET | Retrieve conversation history |
| `/api/chat/new` | POST | Start new conversation |
| `/api/reindex` | POST | Webhook to reindex patterns |

## Success Metrics

| Metric | Target |
|--------|--------|
| Chat response latency (p95) | < 2 seconds |
| Reindex completion time | < 5 minutes |
| Error rate | < 0.5% |
| Availability | > 99.5% |
| Cost (dev/test) | < $300/month |

---

## Timeline Summary

| Phase | Days | Deliverable |
|-------|------|-------------|
| 1. Setup | 2 | Projects, NuGet, structure |
| 2. Architecture | 3 | Services, models, DI |
| 3. Chat API | 3 | Controller, streaming |
| 4. Reindex | 2 | Chunking, indexing, webhooks |
| 5. Terraform | 3 | All modules, environments |
| 6. Docker | 2 | Images, docker-compose |
| 7. CI/CD | 2 | GitHub Actions, deployment |
| 8. Frontend | 2 | ChatWidget, integration |
| 9. Testing | 2 | Tests, monitoring, hardening |
| **TOTAL** | **21 days** | **Production ready** |

---

**Start Date**: [Today]  
**Go-Live Date**: [Today + 21 days]  
**Status**: Ready to begin Phase 1

Next Steps:
1. Review this plan with team
2. Create GitHub branch `feature/chatbot-implementation`
3. Start Phase 1 Day 1
4. Daily standups tracking progress

Good luck! 🚀
