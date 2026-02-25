namespace RiskInsure.Modernization.Chat.Controllers;

using Microsoft.AspNetCore.Mvc;
using RiskInsure.Modernization.Chat.Models;
using RiskInsure.Modernization.Chat.Services;
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
    /// <remarks>
    /// Implements RAG pipeline:
    /// 1. Embed user query
    /// 2. Search knowledge base for relevant patterns
    /// 3. Build system prompt with context
    /// 4. Stream GPT response token-by-token
    /// 5. Persist conversation to Cosmos DB
    /// </remarks>
    [HttpPost("stream")]
    [Produces("text/event-stream")]
    public async Task StreamChat(
        [FromBody] ChatRequestDto request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Chat request received from {UserId}, conversation: {ConversationId}, message: {Message}",
            request.UserId, request.ConversationId, request.Message);

        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                Response.StatusCode = 400;
                await WriteSseEvent("error", "Message cannot be empty");
                return;
            }

            if (string.IsNullOrWhiteSpace(request.UserId))
            {
                Response.StatusCode = 400;
                await WriteSseEvent("error", "UserId is required");
                return;
            }

            // Setup SSE response
            Response.ContentType = "text/event-stream";
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("Connection", "keep-alive");

            // Step 1: Embed user query for semantic search
            _logger.LogDebug("Step 1: Embedding user query");
            float[]? embedding = null;
            try
            {
                embedding = await _openAiService.EmbedTextAsync(request.Message, cancellationToken);
                await WriteSseEvent("embedding_complete", "Query embedded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Embedding failed, continuing with keyword search");
                await WriteSseEvent("embedding_skipped", "Continuing with keyword search");
            }

            // Step 2: Search knowledge base
            _logger.LogDebug("Step 2: Searching patterns for query");
            var patterns = await _searchService.SearchPatternsAsync(
                request.Message,
                embedding,
                topK: 5,
                cancellationToken);

            await WriteSseEvent("search_complete", $"Found {patterns.Count} relevant patterns");
            _logger.LogInformation("Found {PatternCount} patterns for query", patterns.Count);

            // Step 3: Load conversation history
            _logger.LogDebug("Step 3: Loading conversation history");
            var conversation = await _conversationService.GetConversationAsync(
                request.ConversationId,
                request.UserId,
                cancellationToken);

            var history = conversation?.Messages
                .Select(m => new ConversationMessage(m.Role, m.Content, m.Timestamp))
                .ToList() ?? new List<ConversationMessage>();

            // Step 4: Build system prompt with context
            _logger.LogDebug("Step 4: Building system prompt with context");
            var systemPrompt = BuildSystemPrompt(patterns);

            // Step 5: Stream completion
            _logger.LogDebug("Step 5: Streaming GPT response");
            var completionText = new StringBuilder();
            var completionResponse = await _openAiService.GetCompletionAsync(
                systemPrompt,
                request.Message,
                history,
                cancellationToken);

            completionText.Append(completionResponse);

            // Stream response in chunks (simulate token streaming)
            foreach (var chunk in ChunkedString(completionResponse, chunkSize: 20))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                await WriteSseEvent("token", chunk);
                await Response.Body.FlushAsync(cancellationToken);
            }

            await WriteSseEvent("completion_done", "Response streaming complete");

            // Step 6: Build citations
            var citations = patterns
                .Take(3)
                .Select(p => $"{p.Title} ({p.Category})")
                .ToList();

            var responseDto = new ChatResponseDto(
                Answer: completionText.ToString(),
                Citations: citations,
                TokensUsed: completionText.Length / 4 // Rough estimate
            );

            await WriteSseEvent("response_metadata",
                $"Citations: {string.Join("; ", responseDto.Citations)}");

            // Step 7: Persist conversation
            _logger.LogDebug("Step 7: Persisting conversation");
            if (conversation == null)
            {
                conversation = new Conversation
                {
                    Id = request.ConversationId,
                    UserId = request.UserId,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    Status = "active"
                };
            }

            // Add user message
            conversation.Messages.Add(new Message
            {
                Role = "user",
                Content = request.Message,
                Timestamp = DateTimeOffset.UtcNow,
                TokensUsed = request.Message.Length / 4
            });

            // Add assistant response
            conversation.Messages.Add(new Message
            {
                Role = "assistant",
                Content = completionText.ToString(),
                Timestamp = DateTimeOffset.UtcNow,
                TokensUsed = responseDto.TokensUsed
            });

            conversation.UpdatedAt = DateTimeOffset.UtcNow;

            await _conversationService.SaveConversationAsync(conversation, cancellationToken);
            _logger.LogInformation("Conversation {ConversationId} persisted", request.ConversationId);

            await WriteSseEvent("done", "Chat exchange complete");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Chat request cancelled by client");
            await WriteSseEvent("error", "Request cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in chat streaming");
            Response.StatusCode = 500;
            await WriteSseEvent("error", $"Server error: {ex.Message}");
        }
    }

    /// <summary>Get conversation history</summary>
    [HttpGet("{conversationId}")]
    public async Task<IActionResult> GetConversation(
        string conversationId,
        [FromQuery] string userId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Retrieving conversation {ConversationId} for user {UserId}",
            conversationId, userId);

        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("UserId query parameter is required");

        var conversation = await _conversationService.GetConversationAsync(
            conversationId,
            userId,
            cancellationToken);

        if (conversation == null)
            return NotFound($"Conversation {conversationId} not found");

        return Ok(conversation);
    }

    /// <summary>Create new conversation</summary>
    [HttpPost("new")]
    public IActionResult CreateConversation([FromQuery] string userId)
    {
        _logger.LogInformation("Creating new conversation for user {UserId}", userId);

        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("UserId query parameter is required");

        var conversationId = Guid.NewGuid().ToString()[..8];
        var conversation = new Conversation
        {
            Id = conversationId,
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Status = "active",
            Messages = new List<Message>(),
            Ttl = 7776000 // 90 days
        };

        return Ok(new
        {
            conversationId = conversation.Id,
            userId = conversation.UserId,
            createdAt = conversation.CreatedAt,
            message = "Conversation created. Use POST /api/chat/stream to start chatting."
        });
    }

    /// <summary>List conversations for a user</summary>
    [HttpGet("user/{userId}/conversations")]
    public IActionResult ListConversations(string userId)
    {
        _logger.LogDebug("Listing conversations for user {UserId}", userId);

        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("UserId is required");

        // Note: In production, implement Cosmos DB query for user's conversations
        // For now, return empty list (requires UI to track locally)
        return Ok(new
        {
            userId = userId,
            conversations = new List<object>(),
            message = "Use POST /api/chat/new to create conversations"
        });
    }

    /// <summary>Delete conversation</summary>
    [HttpDelete("{conversationId}")]
    public async Task<IActionResult> DeleteConversation(
        string conversationId,
        [FromQuery] string userId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting conversation {ConversationId} for user {UserId}",
            conversationId, userId);

        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("UserId query parameter is required");

        var conversation = await _conversationService.GetConversationAsync(
            conversationId,
            userId,
            cancellationToken);

        if (conversation == null)
            return NotFound();

        // Mark as deleted (soft delete)
        conversation.Status = "deleted";
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        await _conversationService.SaveConversationAsync(conversation, cancellationToken);

        return Ok(new { message = "Conversation deleted" });
    }

    private string BuildSystemPrompt(List<SearchResultItem> patterns)
    {
        var sb = new StringBuilder();
        sb.Append("You are a knowledgeable assistant helping users understand RiskInsure patterns and best practices.\n\n");

        if (patterns.Count > 0)
        {
            sb.Append("Here are relevant reference patterns from the knowledge base:\n\n");

            foreach (var pattern in patterns.Take(3))
            {
                sb.Append($"## {pattern.Title}\n");
                sb.Append($"Category: {pattern.Category}\n");
                sb.Append($"Content: {pattern.Content[..Math.Min(200, pattern.Content.Length)]}...\n\n");
            }

            sb.Append("Use these patterns as context when answering the user's question.\n");
            sb.Append("Always cite which pattern(s) you're referencing.\n\n");
        }
        else
        {
            sb.Append("No specific patterns found in the knowledge base.\n");
            sb.Append("Provide a helpful response based on general knowledge.\n\n");
        }

        sb.Append("Instructions:\n");
        sb.Append("- Be concise and focused\n");
        sb.Append("- Reference specific patterns when applicable\n");
        sb.Append("- Provide actionable guidance\n");
        sb.Append("- If unsure, acknowledge uncertainty\n");

        return sb.ToString();
    }

    private static IEnumerable<string> ChunkedString(string text, int chunkSize)
    {
        for (int i = 0; i < text.Length; i += chunkSize)
        {
            yield return text.Substring(i, Math.Min(chunkSize, text.Length - i));
        }
    }

    private async Task WriteSseEvent(string eventType, string data)
    {
        var eventLine = $"event: {eventType}\n";
        var dataLine = $"data: {data}\n\n";

        await Response.WriteAsync(eventLine);
        await Response.WriteAsync(dataLine);
    }
}
