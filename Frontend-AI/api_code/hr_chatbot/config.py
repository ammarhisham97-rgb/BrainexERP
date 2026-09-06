"""
Unified Configuration Module for HR Chatbot
Loads settings from .env environment variables
"""

from __future__ import annotations
import os
from pathlib import Path
from dotenv import load_dotenv
from pydantic import BaseModel, Field, ConfigDict

# Load environment variables
load_dotenv()

# Get the directory where this script is located and compute PDF path
SCRIPT_DIR = Path(__file__).parent.absolute()
PDF_PATH = str(SCRIPT_DIR / "Egyptian_HR_Policy_Manual_Complete.pdf")

class LMStudioConfig(BaseModel):
    """LM Studio API Configuration"""
    model_config = ConfigDict(validate_assignment=True)
    
    base_url: str = os.getenv("LM_STUDIO_BASE_URL", "http://localhost:1234/v1")
    api_key: str = os.getenv("LM_STUDIO_API_KEY", "lm-studio")
    model: str = os.getenv("LM_STUDIO_MODEL", "llama-3.2-3b-instruct")
    temperature: float = float(os.getenv("TEMPERATURE", "0.3"))
    timeout: float = 90.0

class DocumentConfig(BaseModel):
    """Document Processing Configuration"""
    model_config = ConfigDict(validate_assignment=True)
    
    file_path: str = PDF_PATH
    chunk_size: int = int(os.getenv("CHUNK_SIZE", "600"))
    chunk_overlap: int = int(os.getenv("CHUNK_OVERLAP", "100"))

class EmbeddingConfig(BaseModel):
    """Embedding Model Configuration"""
    model_config = ConfigDict(validate_assignment=True)
    
    model: str = os.getenv("EMBEDDING_MODEL", "sentence-transformers/all-MiniLM-L6-v2")

class RetrievalConfig(BaseModel):
    """Document Retrieval Configuration"""
    model_config = ConfigDict(validate_assignment=True)
    
    similarity_k: int = int(os.getenv("SIMILARITY_K", "10"))

class FlaskConfig(BaseModel):
    """Flask Application Configuration"""
    model_config = ConfigDict(validate_assignment=True)
    
    host: str = os.getenv("FLASK_HOST", "0.0.0.0")
    port: int = int(os.getenv("FLASK_PORT", "5004"))
    debug: bool = os.getenv("FLASK_DEBUG", "False").lower() == "true"
    threaded: bool = True

class LoggingConfig(BaseModel):
    """Logging Configuration"""
    model_config = ConfigDict(validate_assignment=True)
    
    level: str = os.getenv("LOG_LEVEL", "INFO")
    log_file: str = "hr_chatbot.log"

class SystemConfig(BaseModel):
    """Master Configuration"""
    model_config = ConfigDict(validate_assignment=True)
    
    lm_studio: LMStudioConfig = Field(default_factory=LMStudioConfig)
    document: DocumentConfig = Field(default_factory=DocumentConfig)
    embedding: EmbeddingConfig = Field(default_factory=EmbeddingConfig)
    retrieval: RetrievalConfig = Field(default_factory=RetrievalConfig)
    flask: FlaskConfig = Field(default_factory=FlaskConfig)
    logging: LoggingConfig = Field(default_factory=LoggingConfig)
    
    def validate_pdf_exists(self):
        """Validate that the PDF file exists"""
        if not Path(self.document.file_path).exists():
            raise FileNotFoundError(f"PDF not found: {self.document.file_path}")
        return True

# Create singleton instance
config = SystemConfig()
