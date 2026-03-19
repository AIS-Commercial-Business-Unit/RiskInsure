using FileProcessing.Contracts.Commands;
using FileProcessing.Contracts.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NServiceBus;
using RiskInsure.FileProcessing.Application.MessageHandlers;
using RiskInsure.FileProcessing.Application.Services;
using RiskInsure.FileProcessing.Domain.Entities;
using RiskInsure.FileProcessing.Domain.Enums;
using RiskInsure.FileProcessing.Domain.Repositories;
using RiskInsure.FileProcessing.Domain.ValueObjects;

namespace FileProcessing.Application.Tests.MessageHandlers;

public class ParseDiscoveredFileHandlerTests
{
    [Fact]
    public async Task Handle_WithNachaProcessingConfig_PublishesRowEventsAndSendsRowCommands()
    {
        // Arrange
        var nachaContent = string.Join("\r\n", new[]
        {
            BuildNachaEntryRecord("0000001", "021000021", "4520001001", 12345, "JAMES ANDERSON"),
            BuildNachaEntryRecord("0000002", "026009593", "8831002001", 99999, "MARIA GARCIA")
        }) + "\r\n";

        var configurationRepository = new Mock<IFileProcessingConfigurationRepository>();
        var processedRepository = new Mock<IProcessedFileRecordRepository>();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var logger = new Mock<ILogger<ParseDiscoveredFileHandler>>();
        var downloadLogger = new Mock<ILogger<DiscoveredFileContentDownloadService>>();
        var context = new Mock<IMessageHandlerContext>();

        var configuration = CreateConfiguration("NACHA");
        configurationRepository
            .Setup(x => x.GetByIdAsync("client-1", configuration.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);

        processedRepository
            .Setup(x => x.CreateAsync(It.IsAny<ProcessedFileRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessedFileRecord record, CancellationToken _) => record);

        httpClientFactory
            .Setup(x => x.CreateClient("FileProcessingHttpsDownload"))
            .Returns(CreateHttpClient(nachaContent));

        var published = new List<object>();
        context
            .Setup(x => x.Publish(It.IsAny<object>(), It.IsAny<PublishOptions>()))
            .Callback<object, PublishOptions>((message, _) => published.Add(message))
            .Returns(Task.CompletedTask);

        var sent = new List<object>();
        context
            .Setup(x => x.Send(It.IsAny<object>(), It.IsAny<SendOptions>()))
            .Callback<object, SendOptions>((message, _) => sent.Add(message))
            .Returns(Task.CompletedTask);

        var downloadService = new DiscoveredFileContentDownloadService(httpClientFactory.Object, downloadLogger.Object);
        var handler = new ParseDiscoveredFileHandler(
            configurationRepository.Object,
            processedRepository.Object,
            downloadService,
            logger.Object);

        var command = new ParseDiscoveredFile
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = "corr-123",
            OccurredUtc = DateTimeOffset.UtcNow,
            IdempotencyKey = "idem-123",
            ClientId = "client-1",
            ConfigurationId = configuration.Id,
            ExecutionId = Guid.NewGuid(),
            DiscoveredFileId = Guid.NewGuid(),
            FileUrl = "https://example.test/data/test.ach",
            Filename = "test.ach",
            Protocol = "HTTPS",
            DiscoveredAt = DateTimeOffset.UtcNow,
            ConfigurationName = "cfg"
        };

        // Act
        await handler.Handle(command, context.Object);

        // Assert
        var rowEvents = published.OfType<NachaRowDiscovered>().ToList();
        rowEvents.Count.Should().Be(2);
        rowEvents[0].Row.TraceNumber.Should().EndWith("0000001");
        rowEvents[1].Row.TraceNumber.Should().EndWith("0000002");

        published.OfType<DiscoveredFileProcessed>().Should().ContainSingle();

        var rowCommands = sent.OfType<ProcessNachaRow>().ToList();
        rowCommands.Count.Should().Be(2);
        rowCommands[0].Row.AmountCents.Should().Be(12345);
        rowCommands[1].Row.AmountCents.Should().Be(99999);
    }

    [Fact]
    public async Task Handle_WhenProcessedRecordAlreadyExists_DoesNotPublishRowsOrSendCommands()
    {
        // Arrange
        var nachaContent = BuildNachaEntryRecord("0000001", "021000021", "4520001001", 12345, "JAMES ANDERSON") + "\r\n";

        var configurationRepository = new Mock<IFileProcessingConfigurationRepository>();
        var processedRepository = new Mock<IProcessedFileRecordRepository>();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var logger = new Mock<ILogger<ParseDiscoveredFileHandler>>();
        var downloadLogger = new Mock<ILogger<DiscoveredFileContentDownloadService>>();
        var context = new Mock<IMessageHandlerContext>();

        var configuration = CreateConfiguration("NACHA");
        configurationRepository
            .Setup(x => x.GetByIdAsync("client-1", configuration.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);

        processedRepository
            .Setup(x => x.CreateAsync(It.IsAny<ProcessedFileRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessedFileRecord?)null);

        httpClientFactory
            .Setup(x => x.CreateClient("FileProcessingHttpsDownload"))
            .Returns(CreateHttpClient(nachaContent));

        context
            .Setup(x => x.Publish(It.IsAny<object>(), It.IsAny<PublishOptions>()))
            .Returns(Task.CompletedTask);

        context
            .Setup(x => x.Send(It.IsAny<object>(), It.IsAny<SendOptions>()))
            .Returns(Task.CompletedTask);

        var downloadService = new DiscoveredFileContentDownloadService(httpClientFactory.Object, downloadLogger.Object);
        var handler = new ParseDiscoveredFileHandler(
            configurationRepository.Object,
            processedRepository.Object,
            downloadService,
            logger.Object);

        var command = new ParseDiscoveredFile
        {
            MessageId = Guid.NewGuid(),
            CorrelationId = "corr-123",
            OccurredUtc = DateTimeOffset.UtcNow,
            IdempotencyKey = "idem-123",
            ClientId = "client-1",
            ConfigurationId = configuration.Id,
            ExecutionId = Guid.NewGuid(),
            DiscoveredFileId = Guid.NewGuid(),
            FileUrl = "https://example.test/data/test.ach",
            Filename = "test.ach",
            Protocol = "HTTPS",
            DiscoveredAt = DateTimeOffset.UtcNow,
            ConfigurationName = "cfg"
        };

        // Act
        await handler.Handle(command, context.Object);

        // Assert
        context.Verify(x => x.Publish(It.IsAny<object>(), It.IsAny<PublishOptions>()), Times.Never);
        context.Verify(x => x.Send(It.IsAny<object>(), It.IsAny<SendOptions>()), Times.Never);
    }

    private static FileProcessingConfiguration CreateConfiguration(string fileType)
    {
        return new FileProcessingConfiguration
        {
            Id = Guid.NewGuid(),
            ClientId = "client-1",
            Name = "Test Config",
            Protocol = ProtocolType.HTTPS,
            ProtocolSettings = new HttpsProtocolSettings
            {
                BaseUrl = "https://example.test",
                AuthenticationType = AuthType.None,
                ConnectionTimeout = TimeSpan.FromSeconds(30),
                FollowRedirects = true,
                MaxRedirects = 3
            },
            FilePathPattern = "/data",
            FilenamePattern = "*.ach",
            Schedule = new ScheduleDefinition("0 * * * *", "UTC"),
            ProcessingConfig = new FileProcessingDefinition
            {
                FileType = fileType
            },
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "test-user"
        };
    }

    private static HttpClient CreateHttpClient(string body)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(System.Text.Encoding.ASCII.GetBytes(body))
        });

        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test")
        };
    }

    private static string BuildNachaEntryRecord(string traceSequence, string routingNumber, string accountNumber, int amountCents, string individualName)
    {
        var rdfiRouting = routingNumber.Substring(0, 8);
        var checkDigit = routingNumber[8].ToString();
        var accountField = accountNumber.PadLeft(17);
        var amountField = amountCents.ToString().PadLeft(10, '0');
        var individualId = string.Empty.PadRight(15);
        var individualNameField = individualName.PadRight(22);
        var traceNumber = "02100002" + traceSequence.PadLeft(7, '0');

        return "6" +
               "22" +
               rdfiRouting +
               checkDigit +
               accountField +
               amountField +
               individualId +
               individualNameField +
               "  " +
               "0" +
               traceNumber;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}