namespace RiskInsure.Policy.Test.Unit.Tests.Managers;

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RiskInsure.Policy.Domain.Contracts.Commands;
using RiskInsure.Policy.Domain.Managers;
using RiskInsure.Policy.Domain.Models;
using RiskInsure.Policy.Domain.Repositories;
using Xunit;

public class PolicyLifecycleEquityTests
{
    [Fact]
    public async Task ProcessEquityUpdateAsync_ThresholdBreach_SetsPendingCancellation()
    {
        var repository = new Mock<IPolicyLifecycleTermStateRepository>();
        var logger = new Mock<ILogger<PolicyLifecycleManager>>();
        var manager = new PolicyLifecycleManager(repository.Object, logger.Object);

        var state = new PolicyLifecycleTermState
        {
            Id = "term-1",
            PolicyId = "policy-1",
            PolicyTermId = "term-1",
            CurrentStatus = "Active",
            EffectiveDateUtc = DateTimeOffset.UtcNow.AddMonths(-2),
            ExpirationDateUtc = DateTimeOffset.UtcNow.AddMonths(10),
            GraceWindowPercent = 10m
        };

        repository.Setup(r => r.GetByPolicyTermIdAsync("term-1")).ReturnsAsync(state);
        repository.Setup(r => r.UpdateAsync(It.IsAny<PolicyLifecycleTermState>())).ReturnsAsync((PolicyLifecycleTermState s) => s);

        var command = new ProcessPolicyEquityUpdate(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            IdempotencyKey: "eq-1",
            PolicyId: "policy-1",
            PolicyTermId: "term-1",
            EquityPercentage: -25m,
            CancellationThresholdPercentage: -20m);

        var result = await manager.ProcessEquityUpdateAsync(command);

        result.Should().NotBeNull();
        result!.CurrentStatus.Should().Be("PendingCancellation");
        result.PendingCancellationStartedUtc.Should().NotBeNull();
        result.GraceWindowRecheckUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessEquityUpdateAsync_RecoveredEquity_ReturnsToActive()
    {
        var repository = new Mock<IPolicyLifecycleTermStateRepository>();
        var logger = new Mock<ILogger<PolicyLifecycleManager>>();
        var manager = new PolicyLifecycleManager(repository.Object, logger.Object);

        var state = new PolicyLifecycleTermState
        {
            Id = "term-1",
            PolicyId = "policy-1",
            PolicyTermId = "term-1",
            CurrentStatus = "PendingCancellation",
            PendingCancellationStartedUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            GraceWindowRecheckUtc = DateTimeOffset.UtcNow.AddMinutes(10),
            EffectiveDateUtc = DateTimeOffset.UtcNow.AddMonths(-2),
            ExpirationDateUtc = DateTimeOffset.UtcNow.AddMonths(10),
            GraceWindowPercent = 10m
        };

        repository.Setup(r => r.GetByPolicyTermIdAsync("term-1")).ReturnsAsync(state);
        repository.Setup(r => r.UpdateAsync(It.IsAny<PolicyLifecycleTermState>())).ReturnsAsync((PolicyLifecycleTermState s) => s);

        var command = new ProcessPolicyEquityUpdate(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            IdempotencyKey: "eq-2",
            PolicyId: "policy-1",
            PolicyTermId: "term-1",
            EquityPercentage: -10m,
            CancellationThresholdPercentage: -20m);

        var result = await manager.ProcessEquityUpdateAsync(command);

        result.Should().NotBeNull();
        result!.CurrentStatus.Should().Be("Active");
        result.PendingCancellationStartedUtc.Should().BeNull();
        result.GraceWindowRecheckUtc.Should().BeNull();
    }
}
