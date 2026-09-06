# NovaERP AI — Production Handover Package

> **Prepared by:** Data Science Team  
> **Handover date:** 2026-05-04  
> **Audience:** .NET Full-Stack Backend Team  
> **Package root:** `AMMAR/`

---

## 1. Overview

This package contains **six production-ready AI microservices** that power the intelligent features of the NovaERP system. Each service is a standalone Flask API that the .NET backend will call over HTTP.

| # | Service | Domain | Model Type | Default Port |
|---|---------|--------|-----------|-------------|
| 1 | **HR Attrition Prediction** | HR | XGBoost / Scikit-Learn classifier | `5001` |
| 2 | **Employee Performance Analytics** | HR | Random Forest Regressor | `5002` |
| 3 | **Credit Card Fraud Detection** | Finance | Keras Neural Network (.h5) | `5003` |
| 4 | **HR Policy Chatbot (RAG)** | HR | LangChain + FAISS + LM Studio LLM | `5004` |
| 5 | **Customer Support Chatbot (RAG)** | Support | LangChain + FAISS + LM Studio LLM | `5005` |
| 6 | **Resume–Job Matching** | HR | BERT Embeddings + Cosine Similarity | `5006` |

### Feature Summary

- **Resume–Job Matching:** Accepts a job description and a list of candidate resumes, computes BERT-based semantic embeddings, and returns the top-5 matches ranked by cosine similarity.
- **Predictive Analytics (Attrition & Performance):** Classical ML models trained on HR datasets. They accept employee feature vectors and return risk scores / performance ratings.
- **Fraud Detection:** A supervised deep-learning classifier trained on credit-card transaction data. It flags potentially fraudulent transactions in real time.
- **RAG Chatbots (HR & Customer Support):** Retrieval-Augmented Generation pipelines. PDFs are chunked, embedded via `sentence-transformers`, indexed in FAISS, and queried against a local LLM served by LM Studio.

---

## 2. Directory Structure

```
AMMAR/
├── models/
│   ├── hr_attrition/
│   │   ├── model.pkl              # XGBoost attrition classifier
│   │   └── scaler.pkl             # StandardScaler for feature normalization
│   ├── performance/
│   │   ├── random_forest_regressor_model.pkl   # RF regressor
│   │   ├── scaler.pkl             # StandardScaler
│   │   ├── encoder.pkl            # OneHotEncoder for categorical features
│   │   └── feature_metadata.json  # Feature name lists (numerical + categorical)
│   ├── fraud_detection/
│   │   ├── oversample_model.h5    # Keras neural network (SMOTE-oversampled)
│   │   └── rob_scaler.joblib      # RobustScaler for transaction features
│   ├── resume_matching/
│   │   └── MODEL_README.md        # BERT model (auto-downloaded from HuggingFace)
│   ├── hr_chatbot/
│   │   └── MODEL_README.md        # Sentence-transformers + FAISS (runtime)
│   └── cs_chatbot/
│       └── MODEL_README.md        # Sentence-transformers + FAISS (runtime)
│
├── api_code/
│   ├── hr_attrition/
│   │   ├── app.py                 # Flask API — /predict, /health  (port 5001)
│   │   ├── config.py              # Port, model paths, CORS settings
│   │   └── sample_requests.json   # Example JSON payloads
│   ├── performance/
│   │   ├── app.py                 # Flask API — /predict, /predict-batch, /health  (port 5002)
│   │   ├── sample_requests.json   # Example JSON payloads
│   │   └── generate_pickle_files.py  # One-time script to regenerate .pkl files
│   ├── fraud_detection/
│   │   └── app.py                 # Flask API — /predict, /health  (port 5003)
│   ├── hr_chatbot/
│   │   ├── main_app.py            # Flask API — /ask, /health, /config  (port 5004)
│   │   ├── config.py              # Pydantic-based config (reads .env)
│   │   └── Egyptian_HR_Policy_Manual_Complete.pdf  # Knowledge base PDF
│   ├── cs_chatbot/
│   │   ├── main_app.py            # Flask API — /ask, /health, /config  (port 5005)
│   │   ├── config.py              # Pydantic-based config (reads .env)
│   │   └── Egyptian_ERP_System_Comprehensive_Manual.pdf  # Knowledge base PDF
│   └── resume_matching/
│       ├── app.py                 # Flask API entry point  (port 5006)
│       └── resume_matching_service.py  # BERT embedding + cosine similarity logic
│
├── dependencies/
│   ├── requirements.txt           # Unified pip requirements (all services)
│   ├── Dockerfile                 # Production multi-service container
│   ├── Dockerfile.chatbot         # Chatbot-specific Dockerfile
│   └── docker-compose.yml         # Compose file for chatbot services
│
├── config/
│   ├── hr_chatbot/
│   │   └── .env.example           # Environment template (port 5004)
│   ├── cs_chatbot/
│   │   └── .env.example           # Environment template (port 5005)
│   ├── hr_attrition_api_spec.yaml # OpenAPI 3.0 spec — Attrition API
│   └── performance_api_spec.yaml  # OpenAPI 3.0 spec — Performance API
│
└── documentation/
    └── HANDOVER_README.md          # ← You are here
```

---

## 3. Environment Setup

### 3.1 Prerequisites

| Requirement | Version |
|-------------|---------|
| Python | 3.11+ |
| pip | 23.0+ |
| LM Studio | Latest (for RAG chatbots only) |
| CUDA (optional) | 11.8+ (GPU acceleration for BERT / Keras) |

### 3.2 Step-by-Step Installation

```bash
# 1. Navigate to the package root
cd AMMAR

# 2. Create an isolated virtual environment
python -m venv .venv

# 3. Activate it
# Windows PowerShell:
.\.venv\Scripts\Activate.ps1
# Windows CMD:
.\.venv\Scripts\activate.bat
# Linux / macOS:
source .venv/bin/activate

# 4. Install all dependencies
pip install -r dependencies/requirements.txt

# 5. (RAG Chatbots only) Copy and configure .env files
copy config\hr_chatbot\.env.example  api_code\hr_chatbot\.env
copy config\cs_chatbot\.env.example  api_code\cs_chatbot\.env
# Edit the .env files to point to your LM Studio instance
```

### 3.3 LM Studio Setup (Chatbots Only)

1. Download and install [LM Studio](https://lmstudio.ai/).
2. Download a model (recommended: `llama-3.2-3b-instruct`).
3. Start the local server in LM Studio (default: `http://localhost:1234/v1`).
4. Ensure `.env` files reference the correct `LM_STUDIO_BASE_URL`.

---

## 4. Running the Inference APIs

Each service runs independently. Start them on separate ports:

```bash
# Service 1 — HR Attrition (port 5001)
cd api_code/hr_attrition
python app.py
# Or override port: flask run --host=0.0.0.0 --port=5001

# Service 2 — Employee Performance (port 5002)
cd api_code/performance
python app.py

# Service 3 — Fraud Detection (port 5003)
cd api_code/fraud_detection
python app.py

# Service 4 — HR Chatbot (port 5004)
cd api_code/hr_chatbot
python main_app.py

# Service 5 — Customer Support Chatbot (port 5005)
cd api_code/cs_chatbot
python main_app.py

# Service 6 — Resume Matching (port 5006)
cd api_code/resume_matching
python app.py
```

> **Tip:** Each service exposes a `GET /health` endpoint. Use it for readiness probes.

---

## 5. API Endpoint Reference

### 5.1 HR Attrition Prediction

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/predict` | Predict employee attrition risk |
| `GET` | `/health` | Health check |

**Request:**
```json
{
  "features": [41, 5993, 8, 4, 3, 1, 2, 2, 0, 1, 0, 1, 1, 0, 3, 4, 2, 80, 0, 8, 0, 1, 6, 4, 0, 5, 8, 6, 0, 4]
}
```

**Response:**
```json
{
  "prediction": 0,
  "risk_level": "Low Risk",
  "confidence": 94.5,
  "attrition_probability": 5.5
}
```

---

### 5.2 Employee Performance Analytics

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/predict` | Single employee prediction |
| `POST` | `/predict-batch` | Batch prediction (multiple employees) |
| `GET` | `/health` | Health check |

**Request (single — raw employee data):**
```json
{
  "employee": {
    "Age": 30,
    "Years_At_Company": 5,
    "Monthly_Salary": 50000,
    "Work_Hours_Per_Week": 40,
    "Projects_Handled": 10,
    "Overtime_Hours": 5,
    "Sick_Days": 2,
    "Remote_Work_Frequency": 3,
    "Team_Size": 8,
    "Training_Hours": 20,
    "Promotions": 1,
    "Employee_Satisfaction_Score": 4.2,
    "Department": "Engineering",
    "Gender": "Male",
    "Job_Title": "Software Engineer",
    "Education_Level": "Bachelor"
  }
}
```

**Request (single — pre-processed features):**
```json
{
  "features": [30, 50000, 5, 10, 95]
}
```

**Response:**
```json
{
  "performance_score": 4.25,
  "rating": "Very Good"
}
```

**Rating Scale:** `Excellent` (≥4.5) → `Very Good` (≥4.0) → `Good` (≥3.5) → `Satisfactory` (≥3.0) → `Needs Improvement` (<3.0)

**Batch Request:**
```json
{
  "employees": [
    { "Age": 30, "Years_At_Company": 5, "Department": "Engineering", ... },
    { "Age": 45, "Years_At_Company": 12, "Department": "Finance", ... }
  ]
}
```

**Batch Response:**
```json
{
  "predictions": [
    { "performance_score": 4.25, "rating": "Very Good" },
    { "performance_score": 3.80, "rating": "Good" }
  ]
}
```

---

### 5.3 Fraud Detection

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/predict` | Detect fraudulent transaction |
| `GET` | `/health` | Health check |

**Request:**
```json
{
  "features": [-1.36, -0.07, 2.54, 1.38, -0.34, 0.46, 0.24, 0.10, 0.36, 0.09, -0.55, -0.62, -0.99, -0.31, 1.47, -0.47, 0.21, 0.03, 0.40, 0.25, -0.02, 0.28, -0.11, -0.07, 0.13, -0.19, 0.13, -0.02, 149.62, 0.0]
}
```
> 30 features: `V1`–`V28` (PCA-transformed), `Amount`, `Time`

**Response:**
```json
{
  "prediction": 0,
  "label": "Legitimate",
  "fraud_probability": 2.34,
  "confidence": 97.66
}
```

---

### 5.4 RAG Chatbots (HR & Customer Support)

Both chatbots share the same API contract.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/ask` | Ask a question (JSON body) |
| `GET` | `/ask/<question>` | Ask a question (URL-encoded) |
| `GET` | `/health` | Health check + chatbot readiness |
| `GET` | `/config` | Current runtime configuration |
| `GET` | `/api/docs` | Endpoint listing |

**Request:**
```json
{
  "question": "What is the company policy on annual leave?"
}
```

**Response:**
```json
{
  "success": true,
  "answer": "According to the HR Policy Manual, employees are entitled to...",
  "sources": [12, 15, 16],
  "processing_time_ms": 1250.5,
  "documents_retrieved": 4
}
```

---

### 5.5 Resume–Job Matching

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/` | Service status check |
| `POST` | `/match` | Match resumes to a job description |

**Request:**
```json
{
  "job_description": "We are looking for a Senior Python Developer with 5+ years of experience in REST APIs, microservices, and cloud deployment.",
  "resumes": [
    { "ID": 1, "Category": "Software Engineering", "text": "Experienced Python developer with 7 years in backend..." },
    { "ID": 2, "Category": "Data Science", "text": "Machine learning engineer proficient in TensorFlow..." },
    { "ID": 3, "Category": "DevOps", "text": "Cloud infrastructure specialist with Kubernetes..." }
  ]
}
```

**Response:**
```json
{
  "matches": [
    { "jobId": 0, "resumeId": 1, "similarity": 0.89, "domainResume": "Software Engineering", "domainDesc": "N/A" },
    { "jobId": 0, "resumeId": 3, "similarity": 0.72, "domainResume": "DevOps", "domainDesc": "N/A" },
    { "jobId": 0, "resumeId": 2, "similarity": 0.65, "domainResume": "Data Science", "domainDesc": "N/A" }
  ]
}
```

---

## 6. Integration Notes for .NET Backend

### 6.1 Calling from C# / .NET

```csharp
using System.Net.Http;
using System.Text;
using System.Text.Json;

// Example: Call the Attrition API
var client = new HttpClient();
var payload = new { features = new double[] { 41, 5993, 8, 4, 3, ... } };
var content = new StringContent(
    JsonSerializer.Serialize(payload),
    Encoding.UTF8,
    "application/json"
);

var response = await client.PostAsync("http://localhost:5001/predict", content);
var result = await response.Content.ReadAsStringAsync();
```

### 6.2 Health Check Pattern

```csharp
// Startup readiness probe
var health = await client.GetAsync("http://localhost:5001/health");
if (health.IsSuccessStatusCode)
{
    // Service is ready
}
```

### 6.3 Port Assignment (Recommended)

| Service | Port | Base URL |
|---------|------|----------|
| HR Attrition | 5001 | `http://localhost:5001` |
| Performance | 5002 | `http://localhost:5002` |
| Fraud Detection | 5003 | `http://localhost:5003` |
| HR Chatbot | 5004 | `http://localhost:5004` |
| CS Chatbot | 5005 | `http://localhost:5005` |
| Resume Matching | 5006 | `http://localhost:5006` |

### 6.4 Error Handling

All services return errors in a consistent JSON format:

```json
{
  "error": "Description of what went wrong"
}
```

HTTP status codes:
- `200` — Success
- `400` — Bad request (invalid input)
- `500` — Server error
- `503` — Service unavailable (model not loaded)

---

## 7. Excluded Files

The following file types were **intentionally excluded** from this package:

| Category | Examples | Reason |
|----------|----------|--------|
| Raw datasets | `creditcard.csv`, `HR-Employee-Attrition.csv`, `X_train.csv` | Too large for deployment; not needed at inference time |
| Jupyter notebooks | `*.ipynb` | EDA/training artifacts; not production code |
| Training scripts | `train.py`, `test_api.py` | Development-only; models are pre-trained |
| Virtual environments | `venv/`, `__pycache__/` | Must be recreated locally |
| Log files | `*.log` | Runtime artifacts; not distributable |
| Archive files | `archive.zip` | Raw data bundles |

---

## 8. Troubleshooting

| Issue | Solution |
|-------|----------|
| `ModuleNotFoundError` | Re-run `pip install -r dependencies/requirements.txt` |
| Model file not found | Ensure working directory is the service folder, or adjust paths in `config.py` |
| LM Studio connection refused | Start LM Studio server and verify `LM_STUDIO_BASE_URL` in `.env` |
| CUDA errors | Set `CUDA_VISIBLE_DEVICES=""` to force CPU mode |
| Port already in use | Change port in `app.run()` or via `--port` flag |

---

## 9. Contact

For questions about model architecture, training data, or feature engineering, reach out to the **Data Science Team**.

For deployment, containerization, and API gateway configuration, coordinate with **DevOps**.

---

*End of Handover Document*
