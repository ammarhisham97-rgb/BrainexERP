# Resume–Job Matching — Model Artifacts

## Model: `bert-base-uncased`

This service uses the **BERT base (uncased)** transformer model from HuggingFace for
generating semantic embeddings of resumes and job descriptions.

### Why no .pkl / .pt file here?

The model weights (~440 MB) are **automatically downloaded from HuggingFace Hub** on
first launch and cached locally at:

```
~/.cache/huggingface/hub/models--bert-base-uncased/
```

### Pre-downloading for air-gapped / offline deployment

If the production server has no internet access, pre-download the model:

```bash
python -c "from transformers import AutoModel, AutoTokenizer; AutoModel.from_pretrained('bert-base-uncased'); AutoTokenizer.from_pretrained('bert-base-uncased')"
```

Then copy the cache folder (`~/.cache/huggingface/`) to the target machine.

### Model Details

| Property | Value |
|----------|-------|
| Source | `huggingface.co/bert-base-uncased` |
| Parameters | 110M |
| Embedding dim | 768 |
| Max sequence length | 512 tokens |
| Usage | Cosine similarity between job description and resume embeddings |
