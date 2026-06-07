using Microsoft.Extensions.Logging;

namespace VatVerifier.EvaluationTests.Infrastructure;

/// <summary>
/// Captures all log messages during a test run so they can be included in diagnostic reports.
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<string> _entries = [];
    private readonly object _lock = new();

    public IReadOnlyList<string> Entries
    {
        get { lock (_lock) return [.. _entries]; }
    }

    public void Clear()
    {
        lock (_lock) _entries.Clear();
    }

    public ILogger CreateLogger(string categoryName) =>
        new CapturingLogger(categoryName, _entries, _lock);

    public void Dispose() { }

    private sealed class CapturingLogger(string category, List<string> entries, object lk) : ILogger
    {
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var msg = formatter(state, exception);
            // Only capture messages relevant to the evaluation pipeline
            if (!category.Contains("EmbeddingClassificationStep") &&
                !category.Contains("CosineSimilarityClassifier") &&
                !category.Contains("EmbeddingVatEvaluationEngine"))
                return;

            lock (lk) entries.Add($"[{logLevel}] {msg}");
        }
    }
}
