namespace RiskInsure.Policy.Domain.Services;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json.Serialization;

public class PolicyNumberGenerator : IPolicyNumberGenerator
{
    private readonly Container _container;
    private readonly ILogger<PolicyNumberGenerator> _logger;

    public PolicyNumberGenerator(Container container, ILogger<PolicyNumberGenerator> logger)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> GenerateAsync()
    {
        var year = DateTime.UtcNow.Year;
        var sequence = await GetNextSequenceAsync(year);
        return $"KWG-{year}-{sequence:D6}";  // KWG-2026-000001
    }

    private async Task<int> GetNextSequenceAsync(int year)
    {
        const int maxRetries = 5;
        var retryCount = 0;

        while (retryCount < maxRetries)
        {
            try
            {
                var counter = await GetOrCreateCounterAsync(year);
                counter.CurrentSequence++;
                
                var requestOptions = new ItemRequestOptions
                {
                    IfMatchEtag = counter.ETag
                };

                var response = await _container.ReplaceItemAsync(
                    counter,
                    counter.Id,
                    new PartitionKey(counter.Id),
                    requestOptions);

                _logger.LogInformation(
                    "Generated policy number sequence {Sequence} for year {Year}",
                    counter.CurrentSequence, year);

                return counter.CurrentSequence;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                retryCount++;
                _logger.LogWarning(
                    "Policy number sequence conflict (attempt {Attempt}), retrying...",
                    retryCount);
                
                // Brief delay before retry
                await Task.Delay(TimeSpan.FromMilliseconds(50 * retryCount));
            }
        }

        throw new InvalidOperationException("Failed to generate policy number after maximum retries");
    }

    private async Task<PolicyNumberCounter> GetOrCreateCounterAsync(int year)
    {
        var id = $"policy-counter-{year}";

        try
        {
            var response = await _container.ReadItemAsync<PolicyNumberCounter>(
                id,
                new PartitionKey(id));

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Create new counter for the year
            var counter = new PolicyNumberCounter
            {
                Id = id,
                PolicyId = id,  // Partition key must match id
                Year = year,
                CurrentSequence = 0
            };

            var response = await _container.CreateItemAsync(
                counter,
                new PartitionKey(id));

            _logger.LogInformation("Created new policy number counter for year {Year}", year);

            return response.Resource;
        }
    }

    private class PolicyNumberCounter
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }  // "policy-counter-2026"

        [JsonPropertyName("policyId")]  // Partition key (must match id)
        public required string PolicyId { get; set; }  // "policy-counter-2026"

        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("currentSequence")]
        public int CurrentSequence { get; set; }

        [JsonPropertyName("_etag")]
        public string? ETag { get; set; }
    }
}
