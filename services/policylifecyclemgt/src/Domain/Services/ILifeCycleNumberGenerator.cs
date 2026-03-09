namespace RiskInsure.PolicyLifeCycleMgt.Domain.Services;

public interface ILifeCycleNumberGenerator
{
    Task<string> GenerateAsync();
}
