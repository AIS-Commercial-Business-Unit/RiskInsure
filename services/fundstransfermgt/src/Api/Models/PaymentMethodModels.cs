namespace RiskInsure.FundTransferMgt.Api.Models;

public record AddCreditCardRequest(
    string PaymentMethodId,
    string CustomerId,
    string CardholderName,
    string CardNumber,
    int ExpirationMonth,
    int ExpirationYear,
    string Cvv,
    AddressDto BillingAddress
);

public record AddAchAccountRequest(
    string PaymentMethodId,
    string CustomerId,
    string AccountHolderName,
    string RoutingNumber,
    string AccountNumber,
    string AccountType
);

public record AddressDto(
    string Street,
    string City,
    string State,
    string PostalCode,
    string Country
);

public record PaymentMethodResponse(
    string PaymentMethodId,
    string CustomerId,
    string Type,
    string Status,
    CardDetailsDto? Card,
    AchDetailsDto? Ach,
    DateTimeOffset CreatedUtc
);

public record CardDetailsDto(
    string CardholderName,
    string Last4,
    string Brand,
    int ExpirationMonth,
    int ExpirationYear
);

public record AchDetailsDto(
    string AccountHolderName,
    string RoutingNumber,
    string Last4,
    string AccountType
);
