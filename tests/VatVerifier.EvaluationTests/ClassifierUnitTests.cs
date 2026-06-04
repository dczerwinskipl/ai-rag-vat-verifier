using FluentAssertions;
using Microsoft.Extensions.Options;
using VatVerifier.Api.Contracts;
using VatVerifier.Api.Evaluation;
using Xunit;

namespace VatVerifier.EvaluationTests;

[Trait("Category", "Unit")]
public sealed class CosineSimilarityClassifierTests
{
    private static readonly EvaluateInvoiceLineRequest AnyRequest = new(
        "line-x", "Some description", "Some supplier", null, 23m);

    private static ICategoryClassifier CreateClassifier(
        double strongThreshold = 0.85,
        double ambiguousThreshold = 0.75,
        double marginThreshold = 0.10,
        int maxCandidates = 5)
    {
        var opts = Options.Create(new EvaluationOptions
        {
            StrongCandidateThreshold = strongThreshold,
            AmbiguousCandidateThreshold = ambiguousThreshold,
            CandidateMarginThreshold = marginThreshold,
            MaxCandidates = maxCandidates
        });
        return new CosineSimilarityClassifier(opts);
    }

    [Fact]
    public void Classify_EmptyCandidates_ReturnsNotMatched()
    {
        var result = CreateClassifier().Classify([], AnyRequest);

        result.Status.Should().Be(CategoryMatchStatus.NotMatched);
        result.TopCandidates.Should().BeEmpty();
    }

    [Fact]
    public void Classify_SingleStrongCandidate_ReturnsMatched()
    {
        var candidates = new List<ScoredCategory>
        {
            new("cat-1", "Software", 0.92, 23m)
        };

        var result = CreateClassifier().Classify(candidates, AnyRequest);

        result.Status.Should().Be(CategoryMatchStatus.Matched);
        result.TopCandidates.Should().HaveCount(1);
    }

    [Fact]
    public void Classify_TwoCloseHighScoreCandidates_ReturnsAmbiguous()
    {
        var candidates = new List<ScoredCategory>
        {
            new("cat-1", "Alcohol", 0.88, 23m),
            new("cat-2", "Books", 0.85, 5m)
        };

        // margin = 0.03 < threshold 0.10 → not Matched → both above ambiguous threshold → Ambiguous
        var result = CreateClassifier().Classify(candidates, AnyRequest);

        result.Status.Should().Be(CategoryMatchStatus.Ambiguous);
    }

    [Fact]
    public void Classify_TwoHighScoreCandidatesWithSufficientMargin_ReturnsMatched()
    {
        var candidates = new List<ScoredCategory>
        {
            new("cat-1", "Software", 0.92, 23m),
            new("cat-2", "Books", 0.80, 5m)
        };

        // margin = 0.12 >= threshold 0.10 and top >= 0.85 → Matched
        var result = CreateClassifier().Classify(candidates, AnyRequest);

        result.Status.Should().Be(CategoryMatchStatus.Matched);
    }

    [Fact]
    public void Classify_AllCandidatesBelowAmbiguousThreshold_ReturnsNotMatched()
    {
        var candidates = new List<ScoredCategory>
        {
            new("cat-1", "Software", 0.60, 23m),
            new("cat-2", "Books", 0.55, 5m)
        };

        var result = CreateClassifier().Classify(candidates, AnyRequest);

        result.Status.Should().Be(CategoryMatchStatus.NotMatched);
    }

    [Fact]
    public void Classify_RespectsMaxCandidatesLimit()
    {
        var candidates = Enumerable.Range(1, 10)
            .Select(i => new ScoredCategory($"cat-{i}", $"Cat {i}", 0.5 - i * 0.01, 23m))
            .ToList();

        var result = CreateClassifier(maxCandidates: 3).Classify(candidates, AnyRequest);

        result.TopCandidates.Should().HaveCount(3);
    }
}

[Trait("Category", "Unit")]
public sealed class InMemoryCategoryEmbeddingStoreTests
{
    [Fact]
    public async Task ReadyAsync_CompletesAfterMarkReady()
    {
        var store = new InMemoryCategoryEmbeddingStore();

        store.MarkReady();

        await store.ReadyAsync.WaitAsync(TimeSpan.FromMilliseconds(100));
        store.ReadyAsync.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task ReadyAsync_FaultsAfterMarkFailed()
    {
        var store = new InMemoryCategoryEmbeddingStore();
        var ex = new InvalidOperationException("Ollama unavailable");

        store.MarkFailed(ex);

        var act = async () => await store.ReadyAsync.WaitAsync(TimeSpan.FromMilliseconds(100));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Ollama unavailable");
    }

    [Fact]
    public void StoreAndGetAll_ReturnStoredCategories()
    {
        var store = new InMemoryCategoryEmbeddingStore();

        store.Store("cat-1", [1f, 0f], "Software", 23m);
        store.Store("cat-2", [0f, 1f], "Books", 5m);

        var all = store.GetAll();
        all.Should().HaveCount(2);
        all[0].CategoryId.Should().Be("cat-1");
        all[1].CategoryId.Should().Be("cat-2");
    }
}
