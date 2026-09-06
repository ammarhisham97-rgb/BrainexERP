import os
import pandas as pd
import numpy as np
from sentence_transformers import SentenceTransformer, util
import nltk

class ResumeMatcher:
    def __init__(self, model_name="all-mpnet-base-v2"):
        """
        Initialize ResumeMatcher using Sentence Transformers.
        
        Model: all-mpnet-base-v2
        - Specifically trained for semantic similarity
        - 768-dimensional embeddings
        - Works great for document matching and ranking
        - Much better than BERT-base-uncased for this task
        - Handles long documents well (up to 384 tokens)
        
        Why this model:
        1. Semantic understanding of meaning, not just structure
        2. Trained on diverse text similarity tasks
        3. Production-ready and reliable
        4. Good balance between accuracy and speed
        5. Differentiates well between skill levels
        """
        try:
            nltk.download('punkt', quiet=True)
            nltk.download('stopwords', quiet=True)
            
            # Load the Sentence Transformer model
            self.model = SentenceTransformer(model_name)
            print(f"✓ ResumeMatcher initialized with {model_name}")
            print(f"  Model dimension: {self.model.get_sentence_embedding_dimension()}")
        except Exception as e:
            print(f"✗ Error loading Sentence Transformer: {e}")
            raise

    def preprocess_text(self, text):
        """Clean and preprocess text for matching"""
        if not isinstance(text, str):
            return ""
        
        # Convert to lowercase for consistency
        text = text.lower()
        
        # Remove extra whitespace
        text = ' '.join(text.split())
        
        return text

    def get_embedding(self, text):
        """Get sentence embedding using Sentence Transformer"""
        if not isinstance(text, str) or text.strip() == "":
            # Return zero vector if empty
            return np.zeros((self.model.get_sentence_embedding_dimension(),), dtype=float)
        
        # Get embedding (handles long texts automatically)
        embedding = self.model.encode(text, convert_to_numpy=True)
        return embedding

    def match_resumes(self, job_description_text, resume_data_list):
        """
        Match resumes against job description using Sentence Transformers.
        
        Uses cosine similarity on semantic embeddings to find best matches.
        
        Args:
            job_description_text: The job posting description
            resume_data_list: List of resume dictionaries with 'ID', 'text', 'Category'
            
        Returns:
            DataFrame with matched results sorted by similarity (0-1 scale)
        """
        
        # Preprocess job description
        job_desc_clean = self.preprocess_text(job_description_text)
        
        if not job_desc_clean:
            return pd.DataFrame()
        
        # Get job description embedding
        job_embedding = self.get_embedding(job_desc_clean)
        
        print(f"\n🔍 Semantic Matching Analysis:")
        print(f"   Job Description: {job_desc_clean[:100]}...")
        
        # Get embeddings for all resumes
        results = []
        
        for resume in resume_data_list:
            resume_text = self.preprocess_text(resume['text'])
            
            if not resume_text:
                similarity = 0.0
            else:
                # Get resume embedding
                resume_embedding = self.get_embedding(resume_text)
                
                # Calculate cosine similarity (0-1 scale)
                similarity = float(util.cos_sim(job_embedding, resume_embedding)[0][0])
            
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
