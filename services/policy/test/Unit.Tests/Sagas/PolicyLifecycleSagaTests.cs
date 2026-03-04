namespace RiskInsure.Policy.Test.Unit.Tests.Sagas;

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RiskInsure.Policy.Domain.Managers;
using RiskInsure.Policy.Endpoint.In.Sagas;
using Xunit;

public class PolicyLifecycleSagaTests
{
    [Fact]
    public void Constructor_ValidDependencies_CreatesSaga()
    {
        var manager = new Mock<IPolicyLifecycleManager>();
        var logger = new Mock<ILogger<PolicyLifecycleSaga>>();

        var saga = new PolicyLifecycleSaga(manager.Object, logger.Object);

        saga.Should().NotBeNull();
    }
}
