using Microsoft.Extensions.Logging;

namespace RiskInsure.FundTransferMgt.Domain.Services;

/// <summary>
/// Mock payment gateway for development and testing
/// Simulates successful/failed payment authorizations
/// Can be configured to simulate different failure scenarios
/// </summary>
public class MockPaymentGateway : IPaymentGateway
{
    private readonly ILogger<MockPaymentGateway> _logger;
    private readonly MockGatewayConfiguration _config;

    public MockPaymentGateway(
        ILogger<MockPaymentGateway> logger,
        MockGatewayConfiguration? config = null)
    {
        _logger = logger;
        _config = config ?? new MockGatewayConfiguration();
    }

    public async Task<AuthorizationResult> AuthorizeCardPaymentAsync(
        string cardToken,
        decimal amount,
        string customerId,
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Mock: Authorizing card payment - Amount: {Amount}, Customer: {CustomerId}, Transaction: {TransactionId}",
            amount, customerId, transactionId);

        await Task.Delay(_config.SimulatedDelayMs, cancellationToken);

        // Simulate failure based on configuration
        if (ShouldSimulateFailure())
        {
            return new AuthorizationResult
            {
                IsSuccess = false,
                FailureReason = _config.FailureReason ?? "Insufficient funds",
                ErrorCode = _config.FailureErrorCode ?? "INSUFFICIENT_FUNDS",
                ProcessedUtc = DateTimeOffset.UtcNow
            };
        }

        return new AuthorizationResult
        {
            IsSuccess = true,
            GatewayTransactionId = $"MOCK-TXN-{Guid.NewGuid():N}",
            ProcessedUtc = DateTimeOffset.UtcNow
        };
    }

    public async Task<AuthorizationResult> AuthorizeAchPaymentAsync(
        string accountToken,
        decimal amount,
        string customerId,
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Mock: Authorizing ACH payment - Amount: {Amount}, Customer: {CustomerId}, Transaction: {TransactionId}",
            amount, customerId, transactionId);

        await Task.Delay(_config.SimulatedDelayMs, cancellationToken);

        if (ShouldSimulateFailure())
        {
            return new AuthorizationResult
            {
                IsSuccess = false,
                FailureReason = _config.FailureReason ?? "Invalid account",
                ErrorCode = _config.FailureErrorCode ?? "INVALID_ACCOUNT",
                ProcessedUtc = DateTimeOffset.UtcNow
            };
        }

        return new AuthorizationResult
        {
            IsSuccess = true,
            GatewayTransactionId = $"MOCK-ACH-{Guid.NewGuid():N}",
            ProcessedUtc = DateTimeOffset.UtcNow
        };
    }

    public async Task<AuthorizationResult> AuthorizeWalletPaymentAsync(
        string walletToken,
        decimal amount,
        string customerId,
        string transactionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Mock: Authorizing wallet payment - Amount: {Amount}, Customer: {CustomerId}, Transaction: {TransactionId}",
            amount, customerId, transactionId);

        await Task.Delay(_config.SimulatedDelayMs, cancellationToken);

        if (ShouldSimulateFailure())
        {
            return new AuthorizationResult
            {
                IsSuccess = false,
                FailureReason = _config.FailureReason ?? "Wallet unavailable",
                ErrorCode = _config.FailureErrorCode ?? "WALLET_UNAVAILABLE",
                ProcessedUtc = DateTimeOffset.UtcNow
            };
        }

        return new AuthorizationResult
        {
            IsSuccess = true,
            GatewayTransactionId = $"MOCK-WALLET-{Guid.NewGuid():N}",
            ProcessedUtc = DateTimeOffset.UtcNow
        };
    }

    public async Task<RefundResult> ProcessRefundAsync(
        string originalGatewayTransactionId,
        decimal amount,
        string customerId,
        string refundId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Mock: Processing refund - Amount: {Amount}, OriginalTxn: {OriginalTxn}, Refund: {RefundId}",
            amount, originalGatewayTransactionId, refundId);

        await Task.Delay(_config.SimulatedDelayMs, cancellationToken);

        if (ShouldSimulateFailure())
        {
            return new RefundResult
            {
                IsSuccess = false,
                FailureReason = _config.FailureReason ?? "Original transaction not found",
                ErrorCode = _config.FailureErrorCode ?? "ORIGINAL_TXN_NOT_FOUND",
                ProcessedUtc = DateTimeOffset.UtcNow
            };
        }

        return new RefundResult
        {
            IsSuccess = true,
            GatewayRefundId = $"MOCK-REFUND-{Guid.NewGuid():N}",
            ProcessedUtc = DateTimeOffset.UtcNow
        };
    }

    public async Task<string> TokenizeCardAsync(
        string cardNumber,
        int expirationMonth,
        int expirationYear,
        string cvv,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Mock: Tokenizing card - Last4: {Last4}, Exp: {Month}/{Year}",
            cardNumber.Length >= 4 ? cardNumber.Substring(cardNumber.Length - 4) : "****",
            expirationMonth,
            expirationYear);

        await Task.Delay(_config.SimulatedDelayMs / 2, cancellationToken);

        return $"tok_card_{Guid.NewGuid():N}";
    }

    public async Task<string> TokenizeAchAccountAsync(
        string routingNumber,
        string accountNumber,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Mock: Tokenizing ACH - Routing: {Routing}, Last4: {Last4}",
            routingNumber,
            accountNumber.Length >= 4 ? accountNumber.Substring(accountNumber.Length - 4) : "****");

        await Task.Delay(_config.SimulatedDelayMs / 2, cancellationToken);

        return $"tok_ach_{Guid.NewGuid():N}";
    }

    private bool ShouldSimulateFailure()
    {
        if (_config.AlwaysSucceed)
            return false;

        if (_config.AlwaysFail)
            return true;

        // Random failure based on configured failure rate
        var random = new Random();
        return random.NextDouble() < _config.FailureRate;
    }
}

/// <summary>
/// Configuration for mock payment gateway behavior
/// </summary>
public class MockGatewayConfiguration
{
    /// <summary>
    /// Always return success (default for development)
    /// </summary>
    public bool AlwaysSucceed { get; set; } = true;

    /// <summary>
    /// Always return failure (for testing error handling)
    /// </summary>
    public bool AlwaysFail { get; set; } = false;

    /// <summary>
    /// Failure rate (0.0 - 1.0) when not always succeed/fail
    /// </summary>
    public double FailureRate { get; set; } = 0.0;

    /// <summary>
    /// Simulated gateway processing delay in milliseconds
    /// </summary>
    public int SimulatedDelayMs { get; set; } = 100;

    /// <summary>
    /// Failure reason to return on simulated failures
    /// </summary>
    public string? FailureReason { get; set; }

    /// <summary>
    /// Error code to return on simulated failures
    /// </summary>
    public string? FailureErrorCode { get; set; }
}
