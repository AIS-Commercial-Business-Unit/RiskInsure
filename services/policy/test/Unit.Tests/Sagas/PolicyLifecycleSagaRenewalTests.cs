namespace RiskInsure.Policy.Test.Unit.Tests.Sagas;

using Microsoft.Extensions.Logging;
using Moq;
using NServiceBus;
using RiskInsure.Policy.Domain.Contracts.Commands;
using RiskInsure.Policy.Domain.Contracts.Events;
using RiskInsure.Policy.Domain.Managers;
using RiskInsure.Policy.Domain.Models;
using RiskInsure.Policy.Endpoint.In.Sagas;
using Xunit;

public class PolicyLifecycleSagaRenewalTests
{
    [Fact]
    public async Task Handle_PolicyRenewalAccepted_PublishesCompletionAndStartsNextTerm()
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
            CurrentStatus = "Renewed",
            CompletionStatus = "Renewed",
            CompletedUtc = DateTimeOffset.UtcNow,
            TermTicks = 12,
            RenewalOpenPercent = 66,
            RenewalReminderPercent = 83,
            TermEndPercent = 100,
            CancellationThresholdPercentage = -20,
            GraceWindowPercent = 10
        };

        manager.Setup(m => m.HandleRenewalAcceptedAsync(It.IsAny<PolicyRenewalAccepted>()))
            .ReturnsAsync(state);

        context.Setup(c => c.Publish(It.IsAny<object>(), It.IsAny<PublishOptions>()))
            .Returns(Task.CompletedTask);
        context.Setup(c => c.SendLocal(It.IsAny<object>(), It.IsAny<SendOptions>()))
            .Returns(Task.CompletedTask);

        var message = new PolicyRenewalAccepted(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            PolicyId: "policy-1",
            CurrentPolicyTermId: "term-1",
            NextPolicyTermId: "term-2",
            NextTermEffectiveDate: DateTimeOffset.UtcNow.AddDays(1),
            NextTermExpirationDate: DateTimeOffset.UtcNow.AddYears(1),
            IdempotencyKey: "renew-2");

        await saga.Handle(message, context.Object);

        context.Verify(c => c.Publish(It.IsAny<object>(), It.IsAny<PublishOptions>()), Times.Once);
        context.Verify(c => c.SendLocal(It.IsAny<object>(), It.IsAny<SendOptions>()), Times.Once);
    }
}
