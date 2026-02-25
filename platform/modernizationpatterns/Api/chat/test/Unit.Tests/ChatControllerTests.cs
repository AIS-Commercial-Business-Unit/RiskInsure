namespace RiskInsure.Modernization.Chat.Tests;

using Moq;
using RiskInsure.Modernization.Chat.Controllers;
using RiskInsure.Modernization.Chat.Models;
using RiskInsure.Modernization.Chat.Services;
using Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

public class ChatControllerTests
{
    private readonly Mock<IOpenAiService> _mockOpenAiService;
    private readonly Mock<ISearchService> _mockSearchService;
    private readonly Mock<IConversationService> _mockConversationService;
    private readonly Mock<ILogger<ChatController>> _mockLogger;
    private readonly ChatController _controller;

    public ChatControllerTests()
    {
        _mockOpenAiService = new Mock<IOpenAiService>();
        _mockSearchService = new Mock<ISearchService>();
        _mockConversationService = new Mock<IConversationService>();
        _mockLogger = new Mock<ILogger<ChatController>>();

        _controller = new ChatController(
            _mockOpenAiService.Object,
            _mockSearchService.Object,
            _mockConversationService.Object,
            _mockLogger.Object);

        // Setup default HttpContext for SSE response
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    #region CreateConversation Tests

    [Fact]
    public void CreateConversation_WithValidUserId_ReturnsCreatedConversation()
    {
        // Arrange
        var userId = "test-user-123";

        // Act
        var result = _controller.CreateConversation(userId) as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);

        var value = result.Value as dynamic;
        Assert.NotNull(value);
        Assert.Equal(userId, (string)value.userId);
        Assert.NotNull((string)value.conversationId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateConversation_WithMissingUserId_ReturnsBadRequest(string userId)
    {
        // Act
        var result = _controller.CreateConversation(userId) as BadRequestObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(400, result.StatusCode);
    }

    #endregion

    #region GetConversation Tests

    [Fact]
    public async Task GetConversation_WithExistingConversation_ReturnsConversation()
    {
        // Arrange
        var conversationId = "conv-123";
        var userId = "user-123";
        var expectedConversation = new Conversation
        {
            Id = conversationId,
            UserId = userId,
            Messages = new List<Message>()
        };

        _mockConversationService
            .Setup(s => s.GetConversationAsync(conversationId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedConversation);

        // Act
        var result = await _controller.GetConversation(conversationId, userId, CancellationToken.None) as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        var conversation = result.Value as Conversation;
        Assert.NotNull(conversation);
        Assert.Equal(conversationId, conversation.Id);
    }

    [Fact]
    public async Task GetConversation_WithMissingConversation_ReturnsNotFound()
    {
        // Arrange
        var conversationId = "missing-conv";
        var userId = "user-123";

        _mockConversationService
            .Setup(s => s.GetConversationAsync(conversationId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        // Act
        var result = await _controller.GetConversation(conversationId, userId, CancellationToken.None) as NotFoundObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(404, result.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GetConversation_WithMissingUserId_ReturnsBadRequest(string userId)
    {
        // Act
        var result = await _controller.GetConversation("conv-123", userId, CancellationToken.None) as BadRequestObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(400, result.StatusCode);
    }

    #endregion

    #region DeleteConversation Tests

    [Fact]
    public async Task DeleteConversation_WithValidIds_ReturnsSuccess()
    {
        // Arrange
        var conversationId = "conv-123";
        var userId = "user-123";
        var conversation = new Conversation { Id = conversationId, UserId = userId };

        _mockConversationService
            .Setup(s => s.GetConversationAsync(conversationId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        _mockConversationService
            .Setup(s => s.SaveConversationAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeleteConversation(conversationId, userId, CancellationToken.None) as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);

        _mockConversationService.Verify(
            s => s.SaveConversationAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteConversation_WithMissingConversation_ReturnsNotFound()
    {
        // Arrange
        var conversationId = "missing-conv";
        var userId = "user-123";

        _mockConversationService
            .Setup(s => s.GetConversationAsync(conversationId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        // Act
        var result = await _controller.DeleteConversation(conversationId, userId, CancellationToken.None) as NotFoundResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(404, result.StatusCode);
    }

    #endregion

    #region ListConversations Tests

    [Fact]
    public void ListConversations_WithValidUserId_ReturnsOk()
    {
        // Arrange
        var userId = "user-123";

        // Act
        var result = _controller.ListConversations(userId) as OkObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ListConversations_WithMissingUserId_ReturnsBadRequest(string userId)
    {
        // Act
        var result = _controller.ListConversations(userId) as BadRequestObjectResult;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(400, result.StatusCode);
    }

    #endregion

    #region StreamChat Tests

    [Fact]
    public async Task StreamChat_WithValidRequest_CallsAllServices()
    {
        // Arrange
        var request = new ChatRequestDto(
            Message: "What is a pattern?",
            ConversationId: "conv-123",
            UserId: "user-123");

        var patterns = new List<SearchResultItem>();

        _mockSearchService
            .Setup(s => s.SearchPatternsAsync(request.Message, null, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(patterns);

        _mockConversationService
            .Setup(s => s.GetConversationAsync(request.ConversationId, request.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation?)null);

        _mockOpenAiService
            .Setup(s => s.GetCompletionAsync(It.IsAny<string>(), request.Message, It.IsAny<List<ConversationMessage>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("This is a helpful response about patterns.");

        _mockConversationService
            .Setup(s => s.SaveConversationAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.StreamChat(request, CancellationToken.None);

        // Assert
        _mockSearchService.Verify(
            s => s.SearchPatternsAsync(request.Message, null, 5, It.IsAny<CancellationToken>()),
            Times.Once);

        _mockConversationService.Verify(
            s => s.SaveConversationAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task StreamChat_WithMissingMessage_ReturnsBadRequest(string message)
    {
        // Arrange
        var request = new ChatRequestDto(
            Message: message,
            ConversationId: "conv-123",
            UserId: "user-123");

        // Act
        await _controller.StreamChat(request, CancellationToken.None);

        // Assert
        Assert.Equal(400, _controller.Response.StatusCode);
    }

    [Fact]
    public async Task StreamChat_WithMissingUserId_ReturnsBadRequest()
    {
        // Arrange
        var request = new ChatRequestDto(
            Message: "Hello",
            ConversationId: "conv-123",
            UserId: "");

        // Act
        await _controller.StreamChat(request, CancellationToken.None);

        // Assert
        Assert.Equal(400, _controller.Response.StatusCode);
    }

    #endregion
}
