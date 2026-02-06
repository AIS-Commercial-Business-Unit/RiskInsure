namespace RiskInsure.Policy.Domain.Services;

public interface IPolicyNumberGenerator
{
    Task<string> GenerateAsync();
}
