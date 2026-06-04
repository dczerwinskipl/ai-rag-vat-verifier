#!/usr/bin/env bash
set -euo pipefail

OLLAMA_HOST="${OLLAMA_HOST:-http://localhost:11434}"

# Models to pull. nomic-embed-text-v2-moe works on Ollama 0.5+.
# qwen3-embedding:0.6b requires Ollama 0.6+ — update the container first if on an older version.
MODELS=(
  "nomic-embed-text-v2-moe"
  "qwen3-embedding:0.6b"
)

if [[ -n "${MODEL:-}" ]]; then
  MODELS=("$MODEL")
fi

for model in "${MODELS[@]}"; do
  echo "Pulling $model ..."
  curl -fsS "$OLLAMA_HOST/api/pull" \
    -H "Content-Type: application/json" \
    -d "{\"name\":\"$model\"}" || echo "  Warning: failed to pull $model"
  echo "  Done: $model"
done
