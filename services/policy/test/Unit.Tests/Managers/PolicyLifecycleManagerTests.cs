namespace RiskInsure.Policy.Test.Unit.Tests.Managers;

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RiskInsure.Policy.Domain.Contracts.Commands;
using RiskInsure.Policy.Domain.Managers;
using RiskInsure.Policy.Domain.Models;
using RiskInsure.Policy.Domain.Repositories;
using Xunit;

public class PolicyLifecycleManagerTests
{
    private readonly Mock<IPolicyLifecycleTermStateRepository> _repositoryMock;
    private readonly Mock<ILogger<PolicyLifecycleManager>> _loggerMock;
    private readonly PolicyLifecycleManager _manager;

    public PolicyLifecycleManagerTests()
    {
        _repositoryMock = new Mock<IPolicyLifecycleTermStateRepository>();
        _loggerMock = new Mock<ILogger<PolicyLifecycleManager>>();
        _manager = new PolicyLifecycleManager(_repositoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task StartLifecycleAsync_NewPolicyTerm_CreatesLifecycleState()
    {
        var command = new StartPolicyLifecycle(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            IdempotencyKey: "start-1",
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

        _repositoryMock.Setup(r => r.GetByPolicyTermIdAsync("term-1"))
            .ReturnsAsync((PolicyLifecycleTermState?)null);
        _repositoryMock.Setup(r => r.CreateAsync(It.IsAny<PolicyLifecycleTermState>()))
            .ReturnsAsync((PolicyLifecycleTermState s) => s);

        var state = await _manager.StartLifecycleAsync(command);

        state.PolicyTermId.Should().Be("term-1");
        state.CurrentStatus.Should().Be("Active");
        _repositoryMock.Verify(r => r.CreateAsync(It.IsAny<PolicyLifecycleTermState>()), Times.Once);
    }

    [Fact]
    public async Task ApplyMilestoneAsync_TermEnd_SetsExpiredCompletion()
    {
        var state = new PolicyLifecycleTermState
        {
            Id = "term-1",
            PolicyId = "policy-1",
            PolicyTermId = "term-1",
            CurrentStatus = "Active"
        };

        var command = new ApplyPolicyLifecycleMilestone(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            IdempotencyKey: "milestone-1",
            PolicyId: "policy-1",
            PolicyTermId: "term-1",
            MilestoneType: "TermEnd",
            MilestoneUtc: DateTimeOffset.UtcNow);

        _repositoryMock.Setup(r => r.GetByPolicyTermIdAsync("term-1")).ReturnsAsync(state);
        _repositoryMock.Setup(r => r.UpdateAsync(It.IsAny<PolicyLifecycleTermState>()))
            .ReturnsAsync((PolicyLifecycleTermState s) => s);

        var result = await _manager.ApplyMilestoneAsync(command);

        result.Should().NotBeNull();
        result!.CurrentStatus.Should().Be("Expired");
        result.CompletionStatus.Should().Be("Expired");
        result.CompletedUtc.Should().NotBeNull();
    }
}
