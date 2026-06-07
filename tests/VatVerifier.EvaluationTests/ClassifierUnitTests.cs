using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VatVerifier.Api.Classification;
using VatVerifier.Api.Contracts;
using VatVerifier.Api.Embeddings;
using VatVerifier.Api.Evaluation;
using VatVerifier.Api.Evaluation.Pipeline;
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

        store.Store("cat-1", [1f, 0f], [], [], "Software", 23m, gtuCodes: null, rateVariantRates: null);
        store.Store("cat-2", [0f, 1f], [], [], "Books", 5m, gtuCodes: null, rateVariantRates: null);

        var all = store.GetAll();
        all.Should().HaveCount(2);
        all[0].CategoryId.Should().Be("cat-1");
        all[1].CategoryId.Should().Be("cat-2");
    }

    [Fact]
    public void FindByGtuCode_ReturnsMatchingCategory()
    {
        var store = new InMemoryCategoryEmbeddingStore();
        store.Store("alcohol_spirits_23", [1f, 0f], [], [], "Spirits", 23m, gtuCodes: ["GTU_01"], rateVariantRates: null);
        store.Store("books_5", [0f, 1f], [], [], "Books", 5m, gtuCodes: null, rateVariantRates: null);

        var result = store.FindByGtuCode("GTU_01");

        result.Should().NotBeNull();
        result!.CategoryId.Should().Be("alcohol_spirits_23");
    }

    [Fact]
    public void FindByGtuCode_ReturnsNullForUnknownCode()
    {
        var store = new InMemoryCategoryEmbeddingStore();
        store.Store("books_5", [0f, 1f], [], [], "Books", 5m, gtuCodes: null, rateVariantRates: null);

        store.FindByGtuCode("GTU_99").Should().BeNull();
    }

    [Fact]
    public void FindByCategoryId_ReturnsMatchingCategory()
    {
        var store = new InMemoryCategoryEmbeddingStore();
        store.Store("software_it_23", [1f, 0f], [], [], "Software", 23m, gtuCodes: ["GTU_12"], rateVariantRates: null);
        store.Store("books_5", [0f, 1f], [], [], "Books", 5m, gtuCodes: null, rateVariantRates: null);

        var result = store.FindByCategoryId("books_5");

        result.Should().NotBeNull();
        result!.CategoryId.Should().Be("books_5");
    }

    [Fact]
    public void FindByCategoryId_ReturnsNullForUnknownId()
    {
        var store = new InMemoryCategoryEmbeddingStore();
        store.Store("books_5", [0f, 1f], [], [], "Books", 5m, gtuCodes: null, rateVariantRates: null);

        store.FindByCategoryId("nonexistent").Should().BeNull();
    }
}

[Trait("Category", "Unit")]
public sealed class EvaluationResponseFactoryTests
{
    private static readonly EvaluateInvoiceLineRequest RequestAt23 = new("line-x", "desc", "supplier", null, 23m);
    private static readonly EvaluateInvoiceLineRequest RequestAt8 = new("line-x", "desc", "supplier", null, 8m);
    private static readonly EvaluateInvoiceLineRequest RequestWithGtu10 = new("line-x", "desc", "supplier", null, 23m, GtuCode: "GTU_10");

    [Fact]
    public void ForClassification_Matched_VatMatch_ReturnsOk()
    {
        var result = EvaluationResponseFactory.ForClassification(
            new ClassificationResult(CategoryMatchStatus.Matched, [new("cat", "Cat", 0.9, 23m)]),
            RequestAt23);

        result.Severity.Should().Be(EvaluationSeverity.Ok);
        result.ReasonCode.Should().Be(EvaluationReasonCode.VatMatched);
        result.VatValidationStatus.Should().Be(VatValidationStatus.Match);
    }

    [Fact]
    public void ForClassification_Matched_VatMismatch_ReturnsCritical()
    {
        var result = EvaluationResponseFactory.ForClassification(
            new ClassificationResult(CategoryMatchStatus.Matched, [new("cat", "Cat", 0.9, 23m)]),
            RequestAt8);

        result.Severity.Should().Be(EvaluationSeverity.Critical);
        result.ReasonCode.Should().Be(EvaluationReasonCode.VatMismatch);
        result.VatValidationStatus.Should().Be(VatValidationStatus.Mismatch);
    }

    [Fact]
    public void ForClassification_Ambiguous_AllSameRate_VatMatch_ReturnsWarning()
    {
        var result = EvaluationResponseFactory.ForClassification(
            new ClassificationResult(CategoryMatchStatus.Ambiguous,
                [new("a", "A", 0.8, 23m), new("b", "B", 0.75, 23m)]),
            RequestAt23);

        result.Severity.Should().Be(EvaluationSeverity.Warning);
        result.ReasonCode.Should().Be(EvaluationReasonCode.CategoryAmbiguousButVatConsistent);
    }

    [Fact]
    public void ForClassification_Ambiguous_DifferentRates_ReturnsAlert()
    {
        var result = EvaluationResponseFactory.ForClassification(
            new ClassificationResult(CategoryMatchStatus.Ambiguous,
                [new("a", "A", 0.8, 23m), new("b", "B", 0.75, 5m)]),
            RequestAt23);

        result.Severity.Should().Be(EvaluationSeverity.Alert);
        result.ReasonCode.Should().Be(EvaluationReasonCode.CategoryAmbiguousWithDifferentVatRates);
    }

    [Fact]
    public void ForClassification_NotMatched_ReturnsAlert()
    {
        var result = EvaluationResponseFactory.ForClassification(
            new ClassificationResult(CategoryMatchStatus.NotMatched, []),
            RequestAt23);

        result.Severity.Should().Be(EvaluationSeverity.Alert);
        result.ReasonCode.Should().Be(EvaluationReasonCode.CategoryNotMatched);
    }

    [Fact]
    public void ForReverseChargeMissing_ReturnsCritical()
    {
        var result = EvaluationResponseFactory.ForReverseChargeMissing(RequestWithGtu10);

        result.Severity.Should().Be(EvaluationSeverity.Critical);
        result.CategoryMatchStatus.Should().Be(CategoryMatchStatus.Matched);
        result.ReasonCode.Should().Be(EvaluationReasonCode.ReverseChargeMissing);
        result.VatValidationStatus.Should().Be(VatValidationStatus.Mismatch);
    }

    [Fact]
    public void ForReverseChargeUnexpected_ReturnsCritical()
    {
        var result = EvaluationResponseFactory.ForReverseChargeUnexpected(RequestAt23);

        result.Severity.Should().Be(EvaluationSeverity.Critical);
        result.ReasonCode.Should().Be(EvaluationReasonCode.ReverseChargeUnexpected);
    }

    [Fact]
    public void ForRateVariantDegradation_TakesMatchedResult_ReturnsAmbiguousAlert()
    {
        var matched = EvaluationResponseFactory.ForClassification(
            new ClassificationResult(CategoryMatchStatus.Matched, [new("construction", "Construction", 1.0, 23m)]),
            RequestAt8);

        var degraded = EvaluationResponseFactory.ForRateVariantDegradation(matched, [8m, 23m]);

        degraded.CategoryMatchStatus.Should().Be(CategoryMatchStatus.Ambiguous);
        degraded.Severity.Should().Be(EvaluationSeverity.Alert);
        degraded.ReasonCode.Should().Be(EvaluationReasonCode.CategoryAmbiguousWithDifferentVatRates);
        degraded.ExpectedVatRates.Should().BeEquivalentTo(new[] { 8m, 23m });
        degraded.CategoryCandidates.Should().HaveCount(1);
        degraded.CategoryCandidates.First().CategoryId.Should().Be("construction");
    }
}

[Trait("Category", "Unit")]
public sealed class EmbeddingVatEvaluationEngineGtuTests
{
    private static InMemoryCategoryEmbeddingStore BuildStore(
        string categoryId, string name, decimal vatRate,
        string[] gtuCodes, IReadOnlyList<decimal>? rateVariantRates = null)
    {
        var store = new InMemoryCategoryEmbeddingStore();
        store.Store(categoryId, [1f, 0f], [], [], name, vatRate, gtuCodes, rateVariantRates);
        store.MarkReady();
        return store;
    }

    private static EmbeddingVatEvaluationEngine BuildEngine(ICategoryEmbeddingStore store) =>
        new(new ThrowingEmbeddingGenerator(), store,
            new CosineSimilarityClassifier(Options.Create(new EvaluationOptions())),
            Options.Create(new EvaluationOptions()),
            NullLogger<EmbeddingVatEvaluationEngine>.Instance,
            NullLoggerFactory.Instance);

    [Fact]
    public async Task EvaluateAsync_GtuResolvesCategory_SkipsEmbeddingAndReturnsCriticalOnMismatch()
    {
        var store = BuildStore("alcohol_spirits_23", "Spirits and strong alcoholic beverages", 23m, ["GTU_01"]);
        var engine = BuildEngine(store);
        var request = new EvaluateInvoiceLineRequest(
            "line-004", "Napoje alkoholowe assorted", "Dystrybutor", null, 8m,
            GtuCode: "GTU_01");

        // ThrowingEmbeddingGenerator throws if called — test passes only if embedding is bypassed
        var response = await engine.EvaluateAsync(request, CancellationToken.None);

        response.CategoryMatchStatus.Should().Be(CategoryMatchStatus.Matched);
        response.Severity.Should().Be(EvaluationSeverity.Critical);
        response.ReasonCode.Should().Be(EvaluationReasonCode.VatMismatch);
    }

    [Fact]
    public async Task EvaluateAsync_GtuResolvesCategory_ReturnsOkWhenVatMatches()
    {
        var store = BuildStore("alcohol_spirits_23", "Spirits", 23m, ["GTU_01"]);
        var engine = BuildEngine(store);
        var request = new EvaluateInvoiceLineRequest(
            "line-x", "Wódka 0,7l", "Hurtownia", null, 23m,
            GtuCode: "GTU_01");

        var response = await engine.EvaluateAsync(request, CancellationToken.None);

        response.CategoryMatchStatus.Should().Be(CategoryMatchStatus.Matched);
        response.Severity.Should().Be(EvaluationSeverity.Ok);
        response.ReasonCode.Should().Be(EvaluationReasonCode.VatMatched);
    }

    [Fact]
    public async Task EvaluateAsync_GtuWithRateVariants_ForcesAmbiguous()
    {
        var store = BuildStore("construction_services", "Construction services", 23m, ["GTU_10"],
            rateVariantRates: [8m, 23m]);
        var engine = BuildEngine(store);
        var request = new EvaluateInvoiceLineRequest(
            "line-x", "Remont lokalu", "Budmax", null, 8m,
            GtuCode: "GTU_10");

        var response = await engine.EvaluateAsync(request, CancellationToken.None);

        response.CategoryMatchStatus.Should().Be(CategoryMatchStatus.Ambiguous);
        response.ExpectedVatRates.Should().BeEquivalentTo(new[] { 8m, 23m });
    }

    [Fact]
    public async Task EvaluateAsync_ReverseChargeMissing_ReturnsCriticalWithoutEmbedding()
    {
        var store = BuildStore("construction_services", "Construction services", 23m, ["GTU_10"]);
        var engine = BuildEngine(store);
        var request = new EvaluateInvoiceLineRequest(
            "line-005", "Roboty budowlane – podwykonawstwo", "Alfa Budownictwo", null, 23m,
            GtuCode: "GTU_10", ReverseChargeApplied: false);

        var response = await engine.EvaluateAsync(request, CancellationToken.None);

        response.Severity.Should().Be(EvaluationSeverity.Critical);
        response.ReasonCode.Should().Be(EvaluationReasonCode.ReverseChargeMissing);
    }

    private sealed class ThrowingEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        public EmbeddingGeneratorMetadata Metadata { get; } = new("throwing", null, null, null);

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Embedding generator must not be called when GTU resolves.");

        public TService? GetService<TService>(object? serviceKey = null) where TService : class => null;

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}

[Trait("Category", "Unit")]
public sealed class EmbeddingClassificationStepTests
{
    // Stub that returns a pre-registered vector for each input text.
    private sealed class StubEmbeddingGenerator(Dictionary<string, float[]> vectors)
        : IEmbeddingGenerator<string, Embedding<float>>
    {
        public EmbeddingGeneratorMetadata Metadata { get; } = new("stub", null, null, null);

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var embeddings = values
                .Select(v => new Embedding<float>(
                    vectors.TryGetValue(v, out var vec) ? new ReadOnlyMemory<float>(vec) : ReadOnlyMemory<float>.Empty))
                .ToList();
            return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(embeddings));
        }

        public TService? GetService<TService>(object? serviceKey = null) where TService : class => null;
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private static EvaluationOptions DefaultOpts() => new()
    {
        StrongCandidateThreshold = 0.50,
        AmbiguousCandidateThreshold = 0.00,
        CandidateMarginThreshold = 0.05,
        MaxCandidates = 5,
        NegativePenaltyWeight = 0.30,
        DescriptionChannelWeight = 0.70,
        SupplierChannelWeight = 0.30,
        RrfK = 60
    };

    [Fact]
    public async Task NegativePenalty_ReducesScoreForNegativeSimilarCategory()
    {
        // Category A: positive = [1,0], negative = [1,0] (same as query → penalized)
        // Category B: positive = [1,0], negative = [0,1] (orthogonal → no penalty)
        // Expected: B ranks above A because it suffers no negative penalty
        var store = new InMemoryCategoryEmbeddingStore();
        store.Store("cat-a", [1f, 0f], [[1f, 0f]], [0f, 1f], "Category A", 23m, null, null);
        store.Store("cat-b", [1f, 0f], [[0f, 1f]], [0f, 1f], "Category B", 23m, null, null);
        store.MarkReady();

        var gen = new StubEmbeddingGenerator(new Dictionary<string, float[]>
        {
            ["widget"] = [1f, 0f]
        });

        var classifier = new CosineSimilarityClassifier(Options.Create(DefaultOpts()));
        var step = new EmbeddingClassificationStep(gen, store, classifier, Options.Create(DefaultOpts()), NullLogger<EmbeddingClassificationStep>.Instance);

        var request = new EvaluateInvoiceLineRequest("line-1", "widget", "Supplier", null, 23m);
        var response = await step.EvaluateAsync(request, CancellationToken.None);

        // Both are above threshold after penalty is applied correctly;
        // B's candidate should rank first and have a higher score than A's
        response.Should().NotBeNull();
        var candidates = response!.CategoryCandidates.ToList();
        candidates.Should().HaveCount(2);
        candidates[0].CategoryId.Should().Be("cat-b");
        candidates[0].Score.Should().BeGreaterThan(candidates[1].Score);
    }

    [Fact]
    public async Task NoPenalty_WhenNegativeVectorIsEmpty()
    {
        // Category with empty negative vector should not receive a penalty;
        // adj_score should equal pos_sim
        var store = new InMemoryCategoryEmbeddingStore();
        store.Store("cat-a", [1f, 0f], [], [0f, 1f], "Category A", 23m, null, null);
        store.MarkReady();

        var gen = new StubEmbeddingGenerator(new Dictionary<string, float[]>
        {
            ["item"] = [1f, 0f]
        });

        var opts = new EvaluationOptions
        {
            StrongCandidateThreshold = 0.90, AmbiguousCandidateThreshold = 0.00,
            CandidateMarginThreshold = 0.05, MaxCandidates = 5,
            NegativePenaltyWeight = 0.30, DescriptionChannelWeight = 0.70,
            SupplierChannelWeight = 0.30, RrfK = 60
        };
        var classifier = new CosineSimilarityClassifier(Options.Create(opts));
        var step = new EmbeddingClassificationStep(gen, store, classifier, Options.Create(opts), NullLogger<EmbeddingClassificationStep>.Instance);

        var request = new EvaluateInvoiceLineRequest("line-1", "item", "Supplier", null, 23m);
        var response = await step.EvaluateAsync(request, CancellationToken.None);

        // With cos_sim = 1.0 and no penalty, score stays 1.0 — above the 0.90 strong threshold → Matched
        response!.CategoryMatchStatus.Should().Be(CategoryMatchStatus.Matched);
        response.CategoryCandidates.First().Score.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public async Task DescriptionChannel_DominatesWhenSupplierMisleads()
    {
        // Description query [1,0] → matches cat-alcohol strongly
        // Supplier query [0,1] → matches cat-books strongly
        // With wDesc=0.70 > wSupp=0.30, cat-alcohol (desc match) should win
        var store = new InMemoryCategoryEmbeddingStore();
        store.Store("cat-alcohol", [1f, 0f], [], [1f, 0f], "Alcohol", 23m, null, null);
        store.Store("cat-books",   [0f, 1f], [], [0f, 1f], "Books",   5m,  null, null);
        store.MarkReady();

        var gen = new StubEmbeddingGenerator(new Dictionary<string, float[]>
        {
            ["Wódka Chopin 0,7l"] = [1f, 0f],
            ["Empik SA | bookstore"] = [0f, 1f]
        });

        var opts = new EvaluationOptions
        {
            StrongCandidateThreshold = 0.50, AmbiguousCandidateThreshold = 0.00,
            CandidateMarginThreshold = 0.01, MaxCandidates = 5,
            NegativePenaltyWeight = 0.30, DescriptionChannelWeight = 0.70,
            SupplierChannelWeight = 0.30, RrfK = 60
        };
        var classifier = new CosineSimilarityClassifier(Options.Create(opts));
        var step = new EmbeddingClassificationStep(gen, store, classifier, Options.Create(opts), NullLogger<EmbeddingClassificationStep>.Instance);

        var request = new EvaluateInvoiceLineRequest("line-1", "Wódka Chopin 0,7l", "Empik SA", "bookstore", 23m);
        var response = await step.EvaluateAsync(request, CancellationToken.None);

        response!.CategoryCandidates.First().CategoryId.Should().Be("cat-alcohol");
    }

    [Fact]
    public async Task EmptySupplier_AssignsNeutralRankToAllCategories()
    {
        // When supplier text is empty, all categories get equal supplier rank (last rank).
        // Only description channel determines the winner.
        var store = new InMemoryCategoryEmbeddingStore();
        store.Store("cat-a", [1f, 0f], [], [0f, 1f], "Category A", 23m, null, null);
        store.Store("cat-b", [0f, 1f], [], [1f, 0f], "Category B", 23m, null, null);
        store.MarkReady();

        // Only description vector registered; supplier won't be called (empty)
        var gen = new StubEmbeddingGenerator(new Dictionary<string, float[]>
        {
            ["desc-a"] = [1f, 0f]
        });

        var classifier = new CosineSimilarityClassifier(Options.Create(DefaultOpts()));
        var step = new EmbeddingClassificationStep(gen, store, classifier, Options.Create(DefaultOpts()), NullLogger<EmbeddingClassificationStep>.Instance);

        // SupplierName and SupplierIndustry both null/empty → supplier text is empty
        var request = new EvaluateInvoiceLineRequest("line-1", "desc-a", "", null, 23m);
        var response = await step.EvaluateAsync(request, CancellationToken.None);

        // cat-a should rank first (its positive vector matches [1,0]);
        // supplier tie-break doesn't change the outcome
        response!.CategoryCandidates.First().CategoryId.Should().Be("cat-a");
    }
}
