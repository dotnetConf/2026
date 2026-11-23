using System.Text.Json;
using Sourcing.Api;

namespace SourcingObservabilityLab.Tests;

public sealed class RecommendationValidatorTests
{
    private static readonly Guid RequestId = Guid.NewGuid();
    private static readonly Supplier[] Suppliers =
    [
        new("SYN-001", "Fictional supplier", ["Managed cloud"], 100000, 900000)
    ];

    [Fact]
    public void AcceptsCandidateRecommendation()
    {
        var json = JsonSerializer.Serialize(new AgentOutput(RequestId, [new("SYN-001", 1, "Matches supplied capability.")]),
            JsonSerializerOptions.Web);
        Assert.Single(RecommendationValidator.Validate(json, RequestId, Suppliers));
    }

    [Fact]
    public void ReturnsRecommendationsInRankOrder()
    {
        Supplier[] suppliers =
        [
            Suppliers[0],
            new("SYN-002", "Another fictional supplier", ["Managed cloud"], 100000, 900000)
        ];
        var json = JsonSerializer.Serialize(new AgentOutput(RequestId,
            [new("SYN-002", 2, "Second choice."), new("SYN-001", 1, "First choice.")]));

        var result = RecommendationValidator.Validate(json, RequestId, suppliers);

        Assert.Equal(["SYN-001", "SYN-002"], result.Select(r => r.SupplierId));
        Assert.Equal([1, 2], result.Select(r => r.Rank));
    }

    [Theory]
    [InlineData("unknown", 1, "reason")]
    [InlineData("SYN-001", 0, "reason")]
    [InlineData("SYN-001", 2, "reason")]
    [InlineData("SYN-001", 1, "")]
    public void RejectsInvalidRecommendation(string supplier, int rank, string reason)
    {
        var json = JsonSerializer.Serialize(new AgentOutput(RequestId, [new(supplier, rank, reason)]));
        Assert.Throws<RecommendationValidationException>(() => RecommendationValidator.Validate(json, RequestId, Suppliers));
    }

    [Fact]
    public void RejectsWrongRequestAndDuplicateSuppliers()
    {
        var wrongRequest = JsonSerializer.Serialize(new AgentOutput(Guid.NewGuid(), []));
        var duplicates = JsonSerializer.Serialize(new AgentOutput(RequestId, [new("SYN-001", 1, "reason"), new("SYN-001", 2, "reason")]));
        Assert.Throws<RecommendationValidationException>(() => RecommendationValidator.Validate(wrongRequest, RequestId, Suppliers));
        Assert.Throws<RecommendationValidationException>(() => RecommendationValidator.Validate(duplicates, RequestId, Suppliers));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("```json\n{}\n```")]
    [InlineData("{\"requestId\":\"not-a-guid\",\"recommendations\":[]}")]
    public void RejectsMalformedOrIncompleteJson(string json) =>
        Assert.Throws<RecommendationValidationException>(() => RecommendationValidator.Validate(json, RequestId, Suppliers));

    [Fact]
    public void AcceptsEmptyRecommendationsWhenNoCandidateFits()
    {
        var json = JsonSerializer.Serialize(new AgentOutput(RequestId, []));
        Assert.Empty(RecommendationValidator.Validate(json, RequestId, Suppliers));
    }
}
