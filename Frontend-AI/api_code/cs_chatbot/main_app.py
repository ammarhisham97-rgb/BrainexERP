#!/usr/bin/env python3
"""
CUSTOMER SUPPORT CHATBOT - PRODUCTION FLASK API v2.0
Enterprise-grade REST API for Customer Support Question Answering
"""

from __future__ import annotations
import os
import sys
import logging
import time
from typing import Optional, Dict, Any
from datetime import datetime
from pathlib import Path

# Framework
from flask import Flask, request, jsonify
from flask_cors import CORS

# Config
from config import SystemConfig

# Document Processing
from langchain_community.document_loaders import PyPDFLoader
from langchain_text_splitters import RecursiveCharacterTextSplitter
from langchain_community.embeddings import HuggingFaceEmbeddings
from langchain_community.vectorstores import FAISS
from openai import OpenAI as OpenAIClient

# Utils
from dotenv import load_dotenv
load_dotenv()

# ============================================================================
# LOGGING
# ============================================================================
class SafeStreamHandler(logging.StreamHandler):
    """Windows-safe logging handler"""
    def emit(self, record):
        try:
            msg = self.format(record)
            stream = self.stream
            try:
                stream.write(msg + self.terminator)
            except UnicodeEncodeError:
                stream.write(msg.encode('ascii', 'replace').decode('ascii') + self.terminator)
            self.flush()
        except Exception:
            self.handleError(record)

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler('cs_chatbot.log', encoding='utf-8'),
        SafeStreamHandler()
    ]
)
logger = logging.getLogger(__name__)

# ============================================================================
# LOCAL LLM CLIENT
# ============================================================================
class LocalLLM:
    """LM Studio LLM Client"""
    def __init__(self, base_url: str, api_key: str, model: str, temperature: float = 0.3):
        import httpx
        # Create client without proxies to avoid compatibility issues
        http_client = httpx.Client(timeout=90.0)
        self.client = OpenAIClient(base_url=base_url, api_key=api_key, http_client=http_client)
        self.model = model
        self.temperature = temperature

    def predict(self, prompt: str) -> str:
        """Generate response from LLM"""
        messages = [
            {"role": "system", "content": "You are a professional Customer Support Assistant. Answer based strictly on the provided context."},
            {"role": "user", "content": prompt}
        ]
        try:
            completion = self.client.chat.completions.create(
                model=self.model,
                messages=messages,
                temperature=self.temperature,
                timeout=90.0
            )
            return completion.choices[0].message.content or ""
        except Exception as e:
            logger.error(f"LLM Error: {e}")
            return f"Error: {str(e)}"

# ============================================================================
# CHATBOT CORE
# ============================================================================
class EnterpriseCustomerSupportChatbot:
    """Main Customer Support Chatbot"""
    def __init__(self, config: SystemConfig):
        self.config = config
        self.vector_db = None
        self.embeddings = None
        self.llm = None
        self.initialized = False
        
        logger.info("🤖 Initializing Customer Support Chatbot...")
        try:
            self._ingest_documents()
            self._init_llm()
            self.initialized = True
            logger.info("✅ Chatbot initialized successfully!")
        except Exception as e:
            logger.error(f"❌ Initialization failed: {e}")
            raise

    def _ingest_documents(self):
        """Load and index documents"""
        try:
            file_path = self.config.document.file_path
            
            if not Path(file_path).exists():
                raise FileNotFoundError(f"PDF not found: {file_path}")

            logger.info(f"📄 Loading PDF: {file_path}")
            loader = PyPDFLoader(file_path)
            docs = loader.load()
            logger.info(f"   Loaded {len(docs)} pages")
            
            # Filter empty pages
            docs = [d for d in docs if len(d.page_content) > 50]
            
            # Create chunks
            logger.info("✂️  Creating chunks...")
            splitter = RecursiveCharacterTextSplitter(
                chunk_size=self.config.document.chunk_size,
                chunk_overlap=self.config.document.chunk_overlap
            )
            chunks = splitter.split_documents(docs)
            logger.info(f"   Created {len(chunks)} chunks")
            
            # Build vector index
            logger.info("🔍 Building vector index...")
            self.embeddings = HuggingFaceEmbeddings(
                model_name=self.config.embedding.model
            )
            self.vector_db = FAISS.from_documents(chunks, self.embeddings)
            logger.info("   Index ready!")
            
        except Exception as e:
            logger.error(f"Document ingestion failed: {e}")
            raise

    def _init_llm(self):
        """Initialize LLM client"""
        logger.info("🤖 Initializing LLM client...")
        self.llm = LocalLLM(
            base_url=self.config.lm_studio.base_url,
            api_key=self.config.lm_studio.api_key,
            model=self.config.lm_studio.model,
            temperature=self.config.lm_studio.temperature
        )
        logger.info(f"   Model: {self.config.lm_studio.model}")

    def query(self, question: str) -> Dict[str, Any]:
        """Process a query"""
        if not self.initialized:
            return {"success": False, "error": "Chatbot not initialized", "answer": None}
        
        try:
            start_time = time.time()
            
            # Retrieve
            docs = self.vector_db.similarity_search(
                question,
                k=self.config.retrieval.similarity_k
            )
            
            # Build context
            context = "\n".join([
                f"[Page {d.metadata.get('page', 0) + 1}] {d.page_content}"
                for d in docs
            ])
            
            # Build prompt
            prompt = f"""You are a helpful Customer Support Assistant. Answer the question based strictly on the context provided.

FORMATTING GUIDELINES:
1. Start with a direct answer
2. Use **bold** for important terms
3. Use bullet points for lists
4. Keep paragraphs concise

CONTEXT:
{context}

QUESTION: {question}

ANSWER:"""
            
            # Generate
            answer = self.llm.predict(prompt)
            
            # Sources
            sources = sorted(list(set([d.metadata.get("page", 0) + 1 for d in docs])))
            
            elapsed = time.time() - start_time
            
            return {
                "success": True,
                "answer": answer,
                "sources": sources,
                "processingTimeMs": round(elapsed * 1000, 2),
                "documentsRetrieved": len(docs)
            }
            
        except Exception as e:
            logger.error(f"Query failed: {e}")
            return {"success": False, "error": str(e), "answer": None}

# ============================================================================
# FLASK APPLICATION
# ============================================================================
def create_app(config: Optional[SystemConfig] = None) -> Flask:
    """Create and configure Flask app"""
    logger.info("📦 create_app() called - creating Flask instance")
    app = Flask(__name__)
    CORS(app)
    
    if config is None:
        config = SystemConfig()
    
    # Initialize chatbot
    logger.info("🔄 Attempting to initialize chatbot...")
    try:
        chatbot = EnterpriseCustomerSupportChatbot(config)
        logger.info(f"✅ create_app(): Chatbot object created: {chatbot}")
        logger.info(f"✅ create_app(): chatbot.initialized = {chatbot.initialized}")
    except Exception as e:
        logger.error(f"❌ create_app(): Failed to initialize chatbot: {e}")
        import traceback
        logger.error(traceback.format_exc())
        chatbot = None
    
    logger.info(f"📦 create_app(): Setting app.config['chatbot'] = {chatbot}")
    app.config['chatbot'] = chatbot
    app.config['system_config'] = config
    
    # ====================================================================
    # ROUTES
    # ====================================================================
    
    @app.route('/health', methods=['GET'])
    def health():
        """Health check"""
        cb = app.config.get('chatbot')
        is_ready = cb is not None and getattr(cb, 'initialized', False)
        logger.info(f"🏥 Health check: chatbot={cb is not None}, initialized={getattr(cb, 'initialized', 'N/A')}, ready={is_ready}")
        return jsonify({
            "status": "ok",
            "timestamp": datetime.now().isoformat(),
            "chatbotReady": is_ready
        })
    
    @app.route('/config', methods=['GET'])
    def get_config():
        """Get configuration"""
        return jsonify({
            "embeddingModel": config.embedding.model,
            "chunkSize": config.document.chunk_size,
            "retrievalK": config.retrieval.similarity_k,
            "llmModel": config.lm_studio.model,
            "temperature": config.lm_studio.temperature,
            "pdfFile": config.document.file_path
        })
    
    @app.route('/ask', methods=['POST'])
    def ask_post():
        """Ask a question (POST)"""
        try:
            cb = app.config.get('chatbot')
            if not cb or not getattr(cb, 'initialized', False):
                return jsonify({"success": False, "error": "Chatbot not initialized"}), 503
            
            data = request.get_json()
            if not data or 'question' not in data:
                return jsonify({"error": "Missing 'question' field"}), 400
            
            question = data['question'].strip()
            if not question:
                return jsonify({"error": "Question cannot be empty"}), 400
            
            logger.info(f"📥 Query: {question}")
            result = cb.query(question)
            
            return jsonify(result), 200 if result['success'] else 500
            
        except Exception as e:
            logger.error(f"API Error: {e}")
            return jsonify({"success": False, "error": str(e)}), 500
    
    @app.route('/ask/<path:question>', methods=['GET'])
    def ask_get(question):
        """Ask a question (GET)"""
        try:
            cb = app.config.get('chatbot')
            if not cb or not getattr(cb, 'initialized', False):
                return jsonify({"success": False, "error": "Chatbot not initialized"}), 503
            
            if not question.strip():
                return jsonify({"error": "Question cannot be empty"}), 400
            
            logger.info(f"📥 Query (GET): {question}")
            result = cb.query(question)
            
            return jsonify(result), 200 if result['success'] else 500
            
        except Exception as e:
            logger.error(f"API Error: {e}")
            return jsonify({"success": False, "error": str(e)}), 500
    
    @app.route('/api/docs', methods=['GET'])
    def api_docs():
        """API Documentation"""
        return jsonify({
            "title": "Customer Support Chatbot API",
            "version": "2.0",
            "endpoints": {
                "GET /health": "Health check",
                "GET /config": "Get configuration",
                "POST /ask": "Ask question (JSON body with 'question' field)",
                "GET /ask/<question>": "Ask question (URL-encoded)"
            }
        })
    
    @app.errorhandler(404)
    def not_found(error):
        return jsonify({"error": "Not found"}), 404
    
    @app.errorhandler(500)
    def internal_error(error):
        return jsonify({"error": "Server error"}), 500
    
    return app

# ============================================================================
# MAIN
# ============================================================================
if __name__ == '__main__':
    try:
        config = SystemConfig()
        app = create_app(config)
        
        logger.info("🚀 Starting Flask API Server...")
        logger.info(f"   Host: {config.flask.host}:{config.flask.port}")
        logger.info(f"   PDF: {config.document.file_path}")
        logger.info("")
        
        app.run(
            host=config.flask.host,
            port=config.flask.port,
            debug=config.flask.debug,
            threaded=config.flask.threaded
        )
        
    except Exception as e:
        logger.error(f"Failed to start: {e}")
        sys.exit(1)
