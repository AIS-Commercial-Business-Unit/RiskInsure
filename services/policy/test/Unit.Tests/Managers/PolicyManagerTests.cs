namespace RiskInsure.Policy.Test.Unit.Tests.Managers;

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NServiceBus;
using RiskInsure.Policy.Domain.Contracts.Events;
using RiskInsure.Policy.Domain.Managers;
using RiskInsure.Policy.Domain.Models;
using RiskInsure.Policy.Domain.Repositories;
using RiskInsure.Policy.Domain.Services;
using RiskInsure.PublicContracts.Events;
using Xunit;

public class PolicyManagerTests
{
    private readonly Mock<IPolicyRepository> _repositoryMock;
    private readonly Mock<IPolicyNumberGenerator> _numberGeneratorMock;
    private readonly Mock<IMessageSession> _messageSessionMock;
    private readonly Mock<ILogger<PolicyManager>> _loggerMock;
    private readonly PolicyManager _manager;

    public PolicyManagerTests()
    {
        _repositoryMock = new Mock<IPolicyRepository>();
        _numberGeneratorMock = new Mock<IPolicyNumberGenerator>();
        _messageSessionMock = new Mock<IMessageSession>();
        _loggerMock = new Mock<ILogger<PolicyManager>>();

        _manager = new PolicyManager(
            _repositoryMock.Object,
            _numberGeneratorMock.Object,
            _messageSessionMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task CreateFromQuoteAsync_NewQuote_CreatesPolicyAndPublishesEvent()
    {
        // Arrange
        var quoteId = Guid.NewGuid().ToString();
        var customerId = "CUST-123";
        
        var quoteAccepted = new QuoteAccepted(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            QuoteId: quoteId,
            CustomerId: customerId,
            StructureCoverageLimit: 300000m,
            StructureDeductible: 2500m,
            ContentsCoverageLimit: 150000m,
            ContentsDeductible: 1000m,
            TermMonths: 12,
            EffectiveDate: DateTimeOffset.UtcNow.AddDays(7),
            Premium: 1200m,
            IdempotencyKey: "QUOTE-123-ACCEPTED"
        );

        _repositoryMock.Setup(r => r.GetByQuoteIdAsync(quoteId))
            .ReturnsAsync((Policy?)null);

        _numberGeneratorMock.Setup(g => g.GenerateAsync())
            .ReturnsAsync("KWG-2026-000001");

        _repositoryMock.Setup(r => r.CreateAsync(It.IsAny<Policy>()))
            .ReturnsAsync((Policy p) => p);

        // Act
        var result = await _manager.CreateFromQuoteAsync(quoteAccepted);

        // Assert
        result.Should().NotBeNull();
        result.QuoteId.Should().Be(quoteId);
        result.CustomerId.Should().Be(customerId);
        result.PolicyNumber.Should().Be("KWG-2026-000001");
        result.Status.Should().Be("Bound");
        result.Premium.Should().Be(quoteAccepted.Premium);

        _repositoryMock.Verify(r => r.CreateAsync(It.IsAny<Policy>()), Times.Once);
        _messageSessionMock.Verify(m => m.Publish(
            It.Is<PolicyBound>(e => e.QuoteId == quoteId),
            It.IsAny<PublishOptions>()), Times.Once);
    }

    [Fact]
    public async Task CreateFromQuoteAsync_DuplicateQuote_ReturnsExistingPolicy()
    {
        // Arrange
        var quoteId = Guid.NewGuid().ToString();
        var policyId = Guid.NewGuid().ToString();
        
        var existingPolicy = new Policy
        {
            Id = policyId,
            PolicyId = policyId,
            QuoteId = quoteId,
            PolicyNumber = "KWG-2026-000001",
            CustomerId = "CUST-123",
            Status = "Bound",
            StructureCoverageLimit = 300000m,
            StructureDeductible = 2500m,
            ContentsCoverageLimit = 150000m,
            ContentsDeductible = 1000m,
            TermMonths = 12,
            EffectiveDate = DateTimeOffset.UtcNow.AddDays(7),
            ExpirationDate = DateTimeOffset.UtcNow.AddDays(7).AddMonths(12),
            BoundDate = DateTimeOffset.UtcNow,
            Premium = 1200m
        };

        var quoteAccepted = new QuoteAccepted(
            MessageId: Guid.NewGuid(),
            OccurredUtc: DateTimeOffset.UtcNow,
            QuoteId: quoteId,
            CustomerId: "CUST-123",
            StructureCoverageLimit: 300000m,
            StructureDeductible: 2500m,
            ContentsCoverageLimit: 150000m,
            ContentsDeductible: 1000m,
            TermMonths: 12,
            EffectiveDate: DateTimeOffset.UtcNow.AddDays(7),
            Premium: 1200m,
            IdempotencyKey: "QUOTE-123-ACCEPTED"
        );

        _repositoryMock.Setup(r => r.GetByQuoteIdAsync(quoteId))
            .ReturnsAsync(existingPolicy);

        // Act
        var result = await _manager.CreateFromQuoteAsync(quoteAccepted);

        // Assert
        result.Should().Be(existingPolicy);
        _repositoryMock.Verify(r => r.CreateAsync(It.IsAny<Policy>()), Times.Never);
        _messageSessionMock.Verify(m => m.Publish(It.IsAny<PolicyBound>(), It.IsAny<PublishOptions>()), Times.Never);
    }

    [Fact]
    public async Task GetPolicyAsync_ExistingPolicy_ReturnsPolicy()
    {
        // Arrange
        var policyId = Guid.NewGuid().ToString();
        var policy = new Policy
        {
            Id = policyId,
            PolicyId = policyId,
            PolicyNumber = "KWG-2026-000001",
            Status = "Active",
            QuoteId = Guid.NewGuid().ToString(),
            CustomerId = "CUST-123",
            StructureCoverageLimit = 300000m,
            StructureDeductible = 2500m,
            ContentsCoverageLimit = 150000m,
            ContentsDeductible = 1000m,
            TermMonths = 12,
            EffectiveDate = DateTimeOffset.UtcNow,
            ExpirationDate = DateTimeOffset.UtcNow.AddMonths(12),
            BoundDate = DateTimeOffset.UtcNow,
            Premium = 1200m
        };

        _repositoryMock.Setup(r => r.GetByIdAsync(policyId))
            .ReturnsAsync(policy);

        // Act
        var result = await _manager.GetPolicyAsync(policyId);

        // Assert
        result.Should().Be(policy);
    }

    [Fact]
    public async Task GetCustomerPoliciesAsync_ExistingPolicies_ReturnsAllPolicies()
    {
        // Arrange
        var customerId = "CUST-123";
        var policyId1 = Guid.NewGuid().ToString();
        var policyId2 = Guid.NewGuid().ToString();
        
        var policies = new List<Policy>
        {
            new() 
            { 
                Id = policyId1,
                PolicyId = policyId1,
                PolicyNumber = "KWG-2026-000001",
                CustomerId = customerId,
                QuoteId = Guid.NewGuid().ToString(),
                Status = "Active",
                StructureCoverageLimit = 300000m,
                StructureDeductible = 2500m,
                ContentsCoverageLimit = 150000m,
                ContentsDeductible = 1000m,
                TermMonths = 12,
                EffectiveDate = DateTimeOffset.UtcNow,
                ExpirationDate = DateTimeOffset.UtcNow.AddMonths(12),
                BoundDate = DateTimeOffset.UtcNow,
                Premium = 1200m
            },
            new() 
            { 
                Id = policyId2,
                PolicyId = policyId2,
                PolicyNumber = "KWG-2026-000002",
                CustomerId = customerId,
                QuoteId = Guid.NewGuid().ToString(),
                Status = "Cancelled",
                StructureCoverageLimit = 300000m,
                StructureDeductible = 2500m,
                ContentsCoverageLimit = 150000m,
                ContentsDeductible = 1000m,
                TermMonths = 12,
                EffectiveDate = DateTimeOffset.UtcNow.AddMonths(-6),
                ExpirationDate = DateTimeOffset.UtcNow.AddMonths(6),
                BoundDate = DateTimeOffset.UtcNow.AddMonths(-6),
                Premium = 1200m
            }
        };

        _repositoryMock.Setup(r => r.GetByCustomerIdAsync(customerId))
            .ReturnsAsync(policies);

        // Act
        var result = await _manager.GetCustomerPoliciesAsync(customerId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(policies[0]);
        result.Should().Contain(policies[1]);
    }
}

