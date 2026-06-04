# Local Ollama for embeddings

Start Ollama with Docker:

```bash
cd docker/ollama
docker compose up -d
```

Pull the default embedding model:

Mac/Linux:

```bash
./pull-models.sh
```

Windows:

```ps1
./pull-models.ps1
```

Default model:

```text
nomic-embed-text-v2-moe
```

Reason: it is a multilingual embedding model, so it should be a better first choice for mixed Polish and English invoice descriptions than English-only embeddings.

Fallback:

```bash
MODEL=bge-m3 ./pull-models.sh
```

NVIDIA GPU notes:

- Docker must see the NVIDIA runtime.
- On Linux/WSL2 this usually requires NVIDIA Container Toolkit.
- Validate with `nvidia-smi` inside a CUDA test container if Ollama falls back to CPU.
