$OLLAMA_HOST = if ($env:OLLAMA_HOST) { $env:OLLAMA_HOST } else { "http://localhost:11434" }

# Models to pull. Add new models here as the project grows.
# qwen3-embedding:0.6b requires Ollama >= 0.6 — update the container first if on an older version.
$MODELS = @(
    "nomic-embed-text-v2-moe",  # primary embedding model (works on Ollama 0.5+)
    "qwen3-embedding:0.6b"      # target embedding model (requires Ollama 0.6+)
)

if ($env:MODEL) {
    $MODELS = @($env:MODEL)
}

foreach ($model in $MODELS) {
    Write-Host "Pulling $model ..."
    $body = ConvertTo-Json @{ name = $model }
    try {
        Invoke-RestMethod -Uri "$OLLAMA_HOST/api/pull" -Method Post -ContentType "application/json" -Body $body
        Write-Host "  Done: $model"
    } catch {
        Write-Warning "  Failed to pull $model`: $_"
    }
}
