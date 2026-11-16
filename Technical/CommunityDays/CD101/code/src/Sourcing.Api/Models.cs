using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sourcing.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SourcingRequest
{
    [Required, RegularExpression("^CloudInfrastructure$")]
    [DefaultValue("CloudInfrastructure")]
    public string Category { get; init; } = "CloudInfrastructure";

    [Range(typeof(decimal), "1", "1000000000")]
    [DefaultValue(500000)]
    public decimal EstimatedAnnualSpend { get; init; } = 500000;

    [Required, RegularExpression("^USD$")]
    [DefaultValue("USD")]
    public string Currency { get; init; } = "USD";

    [Required, StringLength(4000, MinimumLength = 10)]
    [DefaultValue("Managed cloud infrastructure with 24/7 support and disaster recovery.")]
    public string Requirements { get; init; } =
        "Managed cloud infrastructure with 24/7 support and disaster recovery.";
}

public sealed record Supplier(string SupplierId, string Name, string[] Capabilities,
    decimal MinimumAnnualSpend, decimal MaximumAnnualSpend);

public sealed record Recommendation(string SupplierId, int Rank, string Explanation);
public sealed record AgentOutput(Guid RequestId, Recommendation[] Recommendations);
public sealed record AgentReply(string ResponseId, string Json);
public sealed record WorkflowResult(Guid RequestId, string Status, string TraceId, string FoundryResponseId,
    Recommendation[] Recommendations);
public sealed record StoredRequest(Guid RequestId, string Status, string? FoundryResponseId,
    Recommendation[] Recommendations);
public sealed record AuditEvent(Guid EventId, Guid RequestId, string EventType, DateTimeOffset Timestamp,
    string Actor, string[] SupplierIds, string Outcome, string? FailureDetails,
    string? TraceId = null, string? SpanId = null, string? FoundryResponseId = null);

public sealed class RecommendationValidationException(string message) : Exception(message);

public static class RecommendationValidator
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 16
    };

    public static Recommendation[] Validate(string json, Guid requestId, IReadOnlyList<Supplier> suppliers)
    {
        if (json.Length > 32000)
            throw new RecommendationValidationException("Agent output exceeded the allowed size.");

        AgentOutput? output;
        try
        {
            output = JsonSerializer.Deserialize<AgentOutput>(json, Options);
        }
        catch (JsonException)
        {
            throw new RecommendationValidationException("Agent output did not match the required JSON schema.");
        }

        if (output is null || output.RequestId != requestId || output.Recommendations is null
            || output.Recommendations.Length > 3)
            throw new RecommendationValidationException("Invalid request ID or recommendation count.");

        var known = suppliers.Select(s => s.SupplierId).ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var recommendation in output.Recommendations)
        {
            if (recommendation is null || string.IsNullOrWhiteSpace(recommendation.SupplierId)
                || !known.Contains(recommendation.SupplierId) || !seen.Add(recommendation.SupplierId)
                || string.IsNullOrWhiteSpace(recommendation.Explanation) || recommendation.Explanation.Length > 2000)
                throw new RecommendationValidationException("Invalid supplier, duplicate supplier, or explanation.");
        }

        var ordered = output.Recommendations.OrderBy(r => r.Rank).ToArray();
        if (ordered.Where((r, index) => r.Rank != index + 1).Any())
            throw new RecommendationValidationException("Ranks must be consecutive, starting at one.");
        return ordered;
    }
}
