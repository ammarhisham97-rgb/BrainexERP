# HR Chatbot (RAG) — Model Artifacts

## Models Used

This service uses **two** models, both downloaded automatically on first launch:

### 1. Embedding Model: `sentence-transformers/all-MiniLM-L6-v2`

Used to convert document chunks and user queries into vector embeddings for
FAISS similarity search.

- **Downloaded from:** HuggingFace Hub
- **Cached at:** `~/.cache/huggingface/hub/`
- **Embedding dim:** 384
- **Size:** ~80 MB

### 2. LLM: Served via LM Studio (local)

The answer generation is handled by a local LLM served through **LM Studio**
(default model: `llama-3.2-3b-instruct`).

- **Not bundled here** — must be installed separately via LM Studio
- **API endpoint:** `http://localhost:1234/v1` (configurable in `.env`)

### 3. FAISS Vector Index

The FAISS index is **built at runtime** from the knowledge-base PDF:
`api_code/hr_chatbot/Egyptian_HR_Policy_Manual_Complete.pdf`

No pre-built index file is shipped — it is reconstructed on each server start
(takes ~10-30 seconds depending on PDF size).

### Pre-downloading for offline deployment

```bash
python -c "from langchain_community.embeddings import HuggingFaceEmbeddings; HuggingFaceEmbeddings(model_name='sentence-transformers/all-MiniLM-L6-v2')"
```
