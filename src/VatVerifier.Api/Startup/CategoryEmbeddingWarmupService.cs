using System.Text.Json;
using Microsoft.Extensions.AI;
using VatVerifier.Api.Data;
using VatVerifier.Api.Embeddings;

namespace VatVerifier.Api.Startup;

public sealed class CategoryEmbeddingWarmupService(
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    ICategoryEmbeddingStore store,
    IWebHostEnvironment env,
    ILogger<CategoryEmbeddingWarmupService> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var seedPath = ResolveSeedPath();
            logger.LogInformation("Loading category seed from {Path}", seedPath);

            var entries = await LoadSeedAsync(seedPath, stoppingToken);
            logger.LogInformation("Embedding {Count} categories (3 channels: positive, negative×N, supplier)", entries.Count);

            var positiveTexts = entries.Select(BuildPositiveText).ToList();
            var supplierTexts = entries.Select(e => string.Join(", ", e.TypicalSuppliers)).ToList();

            var positiveEmbeddings = await embeddingGenerator.GenerateAsync(positiveTexts, cancellationToken: stoppingToken);
            var supplierEmbeddings = await embeddingGenerator.GenerateAsync(supplierTexts, cancellationToken: stoppingToken);

            // Negative embeddings: each example is embedded individually so max-similarity can be used
            // at classification time, avoiding centroid dilution across unrelated examples.
            var negativeVectorSets = await BuildNegativeVectorsAsync(entries, stoppingToken);

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                store.Store(
                    entry.CategoryId,
                    positiveEmbeddings[i].Vector.ToArray(),
                    negativeVectorSets[i],
                    supplierEmbeddings[i].Vector.ToArray(),
                    entry.Name.En,
                    entry.ExpectedVatRate,
                    entry.GtuCodes,
                    entry.RateVariants?.Select(v => v.Rate).ToList());
            }

            store.MarkReady();
            logger.LogInformation("Category embeddings ready");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Category embedding warmup failed — evaluation engine will return degraded responses");
            store.MarkFailed(ex);
        }
    }

    private async Task<float[][][]> BuildNegativeVectorsAsync(
        List<CategorySeedEntry> entries, CancellationToken ct)
    {
        var result = new float[entries.Count][][];

        // Collect all individual negative texts with their category index
        var allTexts = new List<string>();
        var indexMap = new List<int>(); // category index for each text

        for (var i = 0; i < entries.Count; i++)
        {
            foreach (var neg in entries[i].NegativeExamples)
            {
                allTexts.Add(neg);
                indexMap.Add(i);
            }
        }

        if (allTexts.Count == 0)
        {
            for (var i = 0; i < entries.Count; i++)
                result[i] = [];
            return result;
        }

        logger.LogInformation("Embedding {Count} individual negative examples across {Categories} categories",
            allTexts.Count, entries.Count);

        var embeddings = await embeddingGenerator.GenerateAsync(allTexts, cancellationToken: ct);

        // Group resulting vectors back by category
        var grouped = new List<List<float[]>>(entries.Count);
        for (var i = 0; i < entries.Count; i++)
            grouped.Add([]);

        for (var j = 0; j < embeddings.Count; j++)
            grouped[indexMap[j]].Add(embeddings[j].Vector.ToArray());

        for (var i = 0; i < entries.Count; i++)
            result[i] = [.. grouped[i]];

        return result;
    }

    private string ResolveSeedPath()
    {
        var fromContentRoot = Path.Combine(env.ContentRootPath, "Data", "vat-categories.seed.json");
        if (File.Exists(fromContentRoot))
            return fromContentRoot;

        var fromBaseDir = Path.Combine(AppContext.BaseDirectory, "Data", "vat-categories.seed.json");
        if (File.Exists(fromBaseDir))
            return fromBaseDir;

        throw new FileNotFoundException("vat-categories.seed.json not found in Data/ directory", fromContentRoot);
    }

    private static async Task<List<CategorySeedEntry>> LoadSeedAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<List<CategorySeedEntry>>(stream, JsonOptions, ct) ?? [];
    }

    private static string BuildPositiveText(CategorySeedEntry entry) =>
        $"{entry.Name.En}: {entry.Description.En}\n\n" +
        $"Examples: {string.Join(", ", entry.PositiveExamples)}";
}
