namespace RiskInsure.FundTransferMgt.Api.Models;

public record InitiateTransferRequest(
    string CustomerId,
    string PaymentMethodId,
    decimal Amount,
    string Purpose
);

public record ProcessRefundRequest(
    string OriginalTransactionId,
    decimal Amount,
    string Reason
);

public record TransferResponse(
    string TransactionId,
    string CustomerId,
    string PaymentMethodId,
    decimal Amount,
    string Direction,
    string Status,
    string Purpose,
    DateTimeOffset InitiatedUtc,
    DateTimeOffset? SettledUtc
);

public record RefundResponse(
    string RefundId,
    string CustomerId,
    string OriginalTransactionId,
    decimal Amount,
    string Status,
    string Reason,
    DateTimeOffset InitiatedUtc,
    DateTimeOffset? ProcessedUtc
);
