namespace RiskInsure.Policy.Test.Unit.Tests.Managers;

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RiskInsure.Policy.Domain.Contracts.Events;
using RiskInsure.Policy.Domain.Managers;
using RiskInsure.Policy.Domain.Models;
using RiskInsure.Policy.Domain.Repositories;
using Xunit;

public class PolicyLifecycleRenewalTests
{
    [Fact]
    public async Task HandleRenewalAcceptedAsync_ExistingTerm_MarksRenewed()
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
            StatusFlags = ["PendingRenewal"],
            EffectiveDateUtc = DateTimeOffset.UtcNow.AddMonths(-6),
            ExpirationDateUtc = DateTimeOffset.UtcNow.AddMonths(6),
            TermTicks = 12,
            RenewalOpenPercent = 66,
            RenewalReminderPercent = 83,
            TermEndPercent = 100,
            CancellationThresholdPercentage = -20,
            GraceWindowPercent = 10
        };

        repository.Setup(r => r.GetByPolicyTermIdAsync("term-1")).ReturnsAsync(state);
        repository.Setup(r => r.UpdateAsync(It.IsAny<PolicyLifecycleTermState>())).ReturnsAsync((PolicyLifecycleTermState s) => s);

        var renewalAccepted = new PolicyRenewalAccepted(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            PolicyId: "policy-1",
            CurrentPolicyTermId: "term-1",
            NextPolicyTermId: "term-2",
            NextTermEffectiveDate: DateTimeOffset.UtcNow.AddMonths(6),
            NextTermExpirationDate: DateTimeOffset.UtcNow.AddMonths(18),
            IdempotencyKey: "renew-1");

        var result = await manager.HandleRenewalAcceptedAsync(renewalAccepted);

        result.Should().NotBeNull();
        result!.CurrentStatus.Should().Be("Renewed");
        result.CompletionStatus.Should().Be("Renewed");
        result.CompletedUtc.Should().NotBeNull();
        result.StatusFlags.Should().NotContain(flag => string.Equals(flag, "PendingRenewal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task HandleRenewalAcceptedAsync_MissingTerm_ReturnsNull()
    {
        var repository = new Mock<IPolicyLifecycleTermStateRepository>();
        var logger = new Mock<ILogger<PolicyLifecycleManager>>();
        var manager = new PolicyLifecycleManager(repository.Object, logger.Object);

        repository.Setup(r => r.GetByPolicyTermIdAsync("missing-term")).ReturnsAsync((PolicyLifecycleTermState?)null);

        var renewalAccepted = new PolicyRenewalAccepted(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            PolicyId: "policy-1",
            CurrentPolicyTermId: "missing-term",
            NextPolicyTermId: "term-2",
            NextTermEffectiveDate: DateTimeOffset.UtcNow,
            NextTermExpirationDate: DateTimeOffset.UtcNow.AddMonths(12),
            IdempotencyKey: "renew-missing");

        var result = await manager.HandleRenewalAcceptedAsync(renewalAccepted);

        result.Should().BeNull();
    }
}
