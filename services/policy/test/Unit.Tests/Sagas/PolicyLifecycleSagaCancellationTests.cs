namespace RiskInsure.Policy.Test.Unit.Tests.Sagas;

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NServiceBus;
using RiskInsure.Policy.Domain.Contracts.Commands;
using RiskInsure.Policy.Domain.Managers;
using RiskInsure.Policy.Domain.Models;
using RiskInsure.Policy.Endpoint.In.Sagas;
using Xunit;

public class PolicyLifecycleSagaCancellationTests
{
    [Fact]
    public async Task Handle_ProcessPolicyEquityUpdate_PublishesEquityReportedEvent()
    {
        var manager = new Mock<IPolicyLifecycleManager>();
        var logger = new Mock<ILogger<PolicyLifecycleSaga>>();
        var saga = new PolicyLifecycleSaga(manager.Object, logger.Object);
        var context = new Mock<IMessageHandlerContext>();

        var state = new PolicyLifecycleTermState
        {
            Id = "term-1",
            PolicyId = "policy-1",
            PolicyTermId = "term-1",
            CurrentStatus = "PendingCancellation",
            CurrentEquityPercentage = -25m,
            CancellationThresholdPercentage = -20m
        };

        manager.Setup(m => m.ProcessEquityUpdateAsync(It.IsAny<ProcessPolicyEquityUpdate>()))
            .ReturnsAsync(state);

        var message = new ProcessPolicyEquityUpdate(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            IdempotencyKey: "eq-3",
            PolicyId: "policy-1",
            PolicyTermId: "term-1",
            EquityPercentage: -25m,
            CancellationThresholdPercentage: -20m);

        context.Setup(c => c.Publish(It.IsAny<object>(), It.IsAny<PublishOptions>()))
            .Returns(Task.CompletedTask);

        await saga.Handle(message, context.Object);

        context.Verify(c => c.Publish(It.IsAny<object>(), It.IsAny<PublishOptions>()), Times.Once);
    }
}
