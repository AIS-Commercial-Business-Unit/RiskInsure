namespace RiskInsure.Policy.Test.Unit.Tests.Sagas;

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RiskInsure.Policy.Domain.Contracts.Commands;
using RiskInsure.Policy.Domain.Managers;
using RiskInsure.Policy.Domain.Models;
using RiskInsure.Policy.Domain.Repositories;
using Xunit;

public class PolicyLifecycleSagaIdempotencyTests
{
    [Fact]
    public async Task StartLifecycleAsync_DuplicatePolicyTerm_ReturnsExistingState()
    {
        var repository = new Mock<IPolicyLifecycleTermStateRepository>();
        var logger = new Mock<ILogger<PolicyLifecycleManager>>();
        var manager = new PolicyLifecycleManager(repository.Object, logger.Object);

        var existing = new PolicyLifecycleTermState
        {
            Id = "term-1",
            PolicyId = "policy-1",
            PolicyTermId = "term-1",
            CurrentStatus = "Active"
        };

        repository.Setup(r => r.GetByPolicyTermIdAsync("term-1")).ReturnsAsync(existing);

        var command = new StartPolicyLifecycle(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            IdempotencyKey: "start-term-1",
            PolicyId: "policy-1",
            PolicyTermId: "term-1",
            TermTicks: 12,
            EffectiveDate: DateTimeOffset.UtcNow,
            ExpirationDate: DateTimeOffset.UtcNow.AddMonths(12),
            RenewalOpenPercent: 66,
            RenewalReminderPercent: 83,
            TermEndPercent: 100,
            CancellationThresholdPercentage: -20,
            GraceWindowPercent: 10);

        var result = await manager.StartLifecycleAsync(command);

        result.Should().BeSameAs(existing);
        repository.Verify(r => r.CreateAsync(It.IsAny<PolicyLifecycleTermState>()), Times.Never);
    }
}
