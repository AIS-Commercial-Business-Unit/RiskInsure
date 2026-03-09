namespace RiskInsure.CustomerRelationshipsMgt.Domain.Repositories;

using RiskInsure.CustomerRelationshipsMgt.Domain.Models;

public interface IRelationshipRepository
{
    Task<Relationship?> GetByIdAsync(string relationshipId);
    Task<Relationship?> GetByEmailAsync(string email);
    Task<Relationship> CreateAsync(Relationship relationship);
    Task<Relationship> UpdateAsync(Relationship relationship);
    Task DeleteAsync(string relationshipId);
    Task<IEnumerable<Relationship>> GetByStatusAsync(string status);
}
