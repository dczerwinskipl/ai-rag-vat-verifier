using System.Text.Json;
using System.Text.Json.Serialization;

namespace VatVerifier.EvaluationTests.Infrastructure;

public static class DatasetLoader
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<IReadOnlyCollection<EvaluationCase>> LoadAsync(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Datasets", fileName);
        await using var stream = File.OpenRead(path);
        var cases = await JsonSerializer.DeserializeAsync<List<EvaluationCase>>(stream, Options);
        return cases ?? [];
    }
}
