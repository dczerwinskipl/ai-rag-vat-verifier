using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OllamaSharp;
using VatVerifier.Api.Ai;
using VatVerifier.Api.Classification;
using VatVerifier.Api.Contracts;
using VatVerifier.Api.Embeddings;
using VatVerifier.Api.Evaluation;
using VatVerifier.Api.Startup;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("Ai"));
builder.Services.Configure<EvaluationOptions>(builder.Configuration.GetSection("Evaluation"));

builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<AiOptions>>().Value;
    return new OllamaApiClient(new Uri(opts.Ollama.Endpoint), opts.Ollama.EmbeddingModel);
});

builder.Services.AddSingleton<ICategoryEmbeddingStore, InMemoryCategoryEmbeddingStore>();
builder.Services.AddSingleton<ICategoryClassifier, CosineSimilarityClassifier>();
builder.Services.AddSingleton<IVatEvaluationEngine, EmbeddingVatEvaluationEngine>();
builder.Services.AddHostedService<CategoryEmbeddingWarmupService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/invoice-lines/evaluate", async (
    EvaluateInvoiceLineRequest request,
    IVatEvaluationEngine engine,
    CancellationToken cancellationToken) =>
{
    var result = await engine.EvaluateAsync(request, cancellationToken);
    return Results.Ok(result);
});

app.Run();

public partial class Program;
