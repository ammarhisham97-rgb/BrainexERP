# =============================================================================
# NovaERP AI Microservices — Multi-Stage Dockerfile
# =============================================================================
# Build:  docker build -t novaerp-ai .
# Run:    docker run -p 5001:5001 -p 5002:5002 -p 5003:5003 -p 5004:5004 -p 5005:5005 novaerp-ai
# =============================================================================

FROM python:3.11-slim

WORKDIR /app

# Install system dependencies
RUN apt-get update && apt-get install -y gcc g++ curl && rm -rf /var/lib/apt/lists/*

# Copy and install Python dependencies
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy all service code and models
COPY . .

# Expose ports for each microservice
# 5001 = HR Attrition | 5002 = Performance | 5003 = Fraud Detection
# 5004 = HR Chatbot   | 5005 = CS Chatbot  | 5006 = Resume Matching
EXPOSE 5001 5002 5003 5004 5005 5006

# Default entrypoint — override per service
CMD ["python", "-m", "flask", "run", "--host=0.0.0.0"]
