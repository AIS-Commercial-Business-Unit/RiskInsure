namespace RiskInsure.ModernizationPatternsMgt.Api.Controllers;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using RiskInsure.ModernizationPatternsMgt.Domain.Models;
using RiskInsure.ModernizationPatternsMgt.Domain.Services;
using System.Text;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IOpenAiService _openAiService;
    private readonly ISearchService _searchService;
    private readonly IConversationService _conversationService;
    private readonly ILogger<ChatController> _logger;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private string? _systemPromptTemplate;

    public ChatController(
        IOpenAiService openAiService,
        ISearchService searchService,
        IConversationService conversationService,
        ILogger<ChatController> logger,
        IWebHostEnvironment webHostEnvironment)
    {
        _openAiService = openAiService;
        _searchService = searchService;
        _conversationService = conversationService;
        _logger = logger;
        _webHostEnvironment = webHostEnvironment;
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
                await WriteSseEvent("error", "Message cannot be empty", cancellationToken);
                return;
            }

            if (string.IsNullOrWhiteSpace(request.UserId))
            {
                Response.StatusCode = 400;
                await WriteSseEvent("error", "UserId is required", cancellationToken);
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
                await WriteSseEvent("embedding_complete", "Query embedded successfully", cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Embedding failed, continuing with keyword search");
                await WriteSseEvent("embedding_skipped", "Continuing with keyword search", cancellationToken);
            }

            // Step 2: Search knowledge base
            _logger.LogDebug("Step 2: Searching patterns for query");
            var patterns = await _searchService.SearchPatternsAsync(
                request.Message,
                embedding,
                topK: 5,
                cancellationToken);

            await WriteSseEvent("search_complete", $"Found {patterns.Count} relevant patterns", cancellationToken);
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
            foreach (var chunk in ChunkedStringByWords(completionResponse, wordsPerChunk: 3))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                await WriteSseEvent("token", chunk, cancellationToken);
            }

            await WriteSseEvent("completion_done", "Response streaming complete", cancellationToken);

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
                $"Citations: {string.Join("; ", responseDto.Citations)}", cancellationToken);

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
                    Status = "active",
                    Messages = new List<Message>()
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

            await WriteSseEvent("done", "Chat exchange complete", cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Chat request cancelled by client");
            await WriteSseEvent("error", "Request cancelled", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in chat streaming");
            // Cannot set StatusCode after streaming starts; just log and send error event
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
    public async Task<IActionResult> ListConversations(
        string userId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Listing conversations for user {UserId}", userId);

        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("UserId is required");

        var conversations = await _conversationService.GetUserConversationsAsync(userId, cancellationToken);

        return Ok(new
        {
            userId = userId,
            conversations = conversations.Select(c => new
            {
                id = c.Id,
                userId = c.UserId,
                createdAt = c.CreatedAt,
                updatedAt = c.UpdatedAt,
                status = c.Status,
                messages = c.Messages
            }).ToList(),
            message = "Conversations loaded"
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
        // Load prompt template from file (cache after first load)
        if (_systemPromptTemplate == null)
        {
            try
            {
                var promptPath = Path.Combine(
                    _webHostEnvironment.ContentRootPath,
                    "prompts", "system-prompt.txt");

                if (System.IO.File.Exists(promptPath))
                {
                    _systemPromptTemplate = System.IO.File.ReadAllText(promptPath);
                    _logger.LogDebug("Loaded system prompt template from {PromptPath}", promptPath);
                }
                else
                {
                    _logger.LogWarning("Prompt file not found at {PromptPath}, using fallback", promptPath);
                    _systemPromptTemplate = "You are a concise assistant helping users understand RiskInsure patterns.\n\n{REFERENCE_MATERIAL}\n\nProvide direct, practical answers.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading prompt template, using fallback");
                _systemPromptTemplate = "You are a concise assistant helping users understand RiskInsure patterns.\n\n{REFERENCE_MATERIAL}\n\nProvide direct, practical answers.";
            }
        }

        // Build reference material section
        var referenceMaterialSb = new StringBuilder();
        if (patterns.Count > 0)
        {
            foreach (var pattern in patterns.Take(3))
            {
                referenceMaterialSb.Append($"{pattern.Title}:\n{pattern.Content}\n\n");
            }
        }

        // Replace placeholder with actual reference material
        return _systemPromptTemplate.Replace("{REFERENCE_MATERIAL}", referenceMaterialSb.ToString().TrimEnd());
    }

    /// <summary>
    /// Chunks text for streaming while preserving line structure for markdown formatting.
    /// Streams line-by-line first, then chunks long lines by words.
    /// </summary>
    private static IEnumerable<string> ChunkedStringByWords(string text, int wordsPerChunk)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        // Split by newlines first to preserve markdown structure (bullet points, etc.)
        var lines = text.Split('\n');

        for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            var line = lines[lineIdx];
            var isLastLine = lineIdx == lines.Length - 1;

            if (string.IsNullOrEmpty(line))
            {
                // Empty line - just yield the newline if not last
                if (!isLastLine)
                    yield return "\n";
                continue;
            }

            // Split line into words and chunk them
            var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 0)
            {
                if (!isLastLine)
                    yield return "\n";
                continue;
            }

            for (int i = 0; i < words.Length; i += wordsPerChunk)
            {
                var chunk = string.Join(" ", words.Skip(i).Take(wordsPerChunk));
                var isLastChunkOfLine = i + wordsPerChunk >= words.Length;

                // Add trailing space if more words coming in this line
                if (!isLastChunkOfLine)
                {
                    yield return chunk + " ";
                }
                // Add newline at end of line (unless it's the very last line)
                else if (!isLastLine)
                {
                    yield return chunk + "\n";
                }
                else
                {
                    yield return chunk;
                }
            }
        }
    }

    private async Task WriteSseEvent(string eventType, string data, CancellationToken cancellationToken = default)
    {
        // Escape newlines for SSE transport - frontend will decode
        var escapedData = data.Replace("\n", "\\n").Replace("\r", "");

        var eventLine = $"event: {eventType}\n";
        var dataLine = $"data: {escapedData}\n\n";

        await Response.WriteAsync(eventLine, cancellationToken);
        await Response.WriteAsync(dataLine, cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
