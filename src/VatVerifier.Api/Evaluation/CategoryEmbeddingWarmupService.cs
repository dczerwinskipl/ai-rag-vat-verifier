using System.Text.Json;
using Microsoft.Extensions.AI;
using VatVerifier.Api.Data;

namespace VatVerifier.Api.Evaluation;

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
            logger.LogInformation("Embedding {Count} categories", entries.Count);

            var texts = entries.Select(BuildCategoryText).ToList();
            var embeddings = await embeddingGenerator.GenerateAsync(texts, cancellationToken: stoppingToken);

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var vector = embeddings[i].Vector.ToArray();
                store.Store(entry.CategoryId, vector, entry.Name.En, entry.ExpectedVatRate);
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

    private static string BuildCategoryText(CategorySeedEntry entry) =>
        $"{entry.Name.En}: {entry.Description.En}\n\n" +
        $"Examples: {string.Join(", ", entry.PositiveExamples)}\n" +
        $"Not this category: {string.Join(", ", entry.NegativeExamples)}";
}
