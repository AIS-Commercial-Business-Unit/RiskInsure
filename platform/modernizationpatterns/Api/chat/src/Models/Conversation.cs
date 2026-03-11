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
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("tokensUsed")]
    public int TokensUsed { get; set; }
}

public record ChatRequestDto(
    string Message,
    string ConversationId,
    string UserId);

public record ChatResponseDto(
    string Answer,
    List<string> Citations,
    int TokensUsed);
