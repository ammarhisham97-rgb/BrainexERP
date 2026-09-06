import os
import pandas as pd
import numpy as np
import torch
from transformers import AutoModel, AutoTokenizer
from sklearn.metrics.pairwise import cosine_similarity
import nltk

class ResumeMatcherBERT:
    """
    BERT-based Resume Matching Service
    
    Model: bert-base-uncased
    - General-purpose language model trained on Wikipedia and books
    - 768-dimensional embeddings
    - Fast inference
    - Good for general semantic understanding
    
    Pros:
    ✓ Lightweight and fast
    ✓ Works well for general text similarity
    ✓ GPU acceleration available
    
    Cons:
    ✗ Not specifically trained for semantic similarity tasks
    ✗ May not differentiate specialized domains well (like tech skills)
    ✗ Slower at ranking specialized documents
    
    USE CASE: General purpose matching when speed is critical
    """
    
    def __init__(self, model_name="bert-base-uncased"):
        nltk.download('punkt', quiet=True)
        nltk.download('stopwords', quiet=True)

        self.device = "cuda" if torch.cuda.is_available() else "cpu"
        print(f"✓ Using device: {self.device}")

        self.tokenizer = AutoTokenizer.from_pretrained(model_name)
        self.model = AutoModel.from_pretrained(model_name).to(self.device)
        self.model.eval()
        print(f"✓ ResumeMatcher (BERT) initialized with {model_name}")

    def get_embedding(self, text):
        """Get BERT embedding for text"""
        if not isinstance(text, str) or text.strip() == "":
            return np.zeros((768,), dtype=float)

        # BERT tokenizer with proper truncation
        inputs = self.tokenizer(
            text,
            return_tensors="pt",
            truncation=True,
            padding=True,
            max_length=512
        ).to(self.device)
        
        with torch.no_grad():
            outputs = self.model(**inputs)
        
        # Use mean pooling of all tokens
        emb = outputs.last_hidden_state.mean(dim=1).cpu().numpy().squeeze()
        
        if emb.ndim == 0:
            emb = emb.reshape(1)
        
        return emb

    def match_resumes(self, job_description_text, resume_data_list):
        """
        Match resumes against job description using BERT embeddings.
        
        Args:
            job_description_text: The job posting description
            resume_data_list: List of resume dictionaries with 'ID', 'text', 'Category'
            
        Returns:
            DataFrame with matched results sorted by similarity
        """
        
        if not job_description_text or not job_description_text.strip():
            return pd.DataFrame()
        
        # Get job description embedding
        job_embedding = self.get_embedding(job_description_text)
        
        print(f"\n🧠 BERT Matching Analysis:")
        print(f"   Job Description: {job_description_text[:100]}...")
        print(f"   Embedding dimension: {job_embedding.shape}")
        
        # Get embeddings for all resumes and calculate similarities
        results = []
        
        for resume in resume_data_list:
            resume_text = resume.get('text', '')
            
            if not resume_text or not resume_text.strip():
                similarity = 0.0
            else:
                # Get resume embedding
                resume_embedding = self.get_embedding(resume_text)
                
                # Ensure proper shape for cosine_similarity
                if job_embedding.ndim == 1:
                    job_embedding = job_embedding.reshape(1, -1)
                if resume_embedding.ndim == 1:
                    resume_embedding = resume_embedding.reshape(1, -1)
                
                # Calculate cosine similarity
                similarity = float(cosine_similarity(job_embedding, resume_embedding)[0][0])
            
            results.append({
                'jobId': 0,
                'resumeId': int(resume['ID']),
                'similarity': similarity,
                'domainResume': resume['Category'],
                'domainDesc': 'Job Posting'
            })
        
        # Create DataFrame and sort by similarity descending
        results_df = pd.DataFrame(results).sort_values(by='similarity', ascending=False)
        
        # Get top 5 results
        results_df = results_df.head(5)
        
        return results_df
