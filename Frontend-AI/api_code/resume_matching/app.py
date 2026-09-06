
import os
from flask import Flask, request, jsonify
from flask_cors import CORS
from resume_matching_service import ResumeMatcher
from config import API_HOST, API_PORT, DEBUG_MODE, CORS_ORIGINS, MAX_RESUMES, MAX_JOB_DESCRIPTION_LENGTH, MAX_RESUME_TEXT_LENGTH, MIN_RESUMES

app = Flask(__name__)
CORS(app)  # Enable CORS for all routes

# Initialize BERT ResumeMatcher
try:
    matcher = ResumeMatcher()
    print("✓ ResumeMatcher initialized successfully.")
except Exception as e:
    print(f"✗ Error initializing ResumeMatcher: {e}")
    matcher = None


@app.route('/')
def home():
    return jsonify({
        "message": "Resume Matching Service is running!",
        "version": "1.0.0"
    }), 200


@app.route('/health', methods=['GET'])
def health():
    """Health check endpoint"""
    if matcher is None:
        return jsonify({"status": "unhealthy", "error": "ResumeMatcher not initialized"}), 503
    return jsonify({"status": "healthy"}), 200


@app.route('/predict', methods=['POST'])
def predict():
    """
    API endpoint for resume matching.
    
    Expected JSON payload:
    {
        "jobDescription": "string",
        "resumes": [
            {
                "ID": "unique_id",
                "text": "resume_text_content",
                "Category": "domain_category"
            },
            ...
        ]
    }
    
    Returns: Top 5 matched resumes with similarity scores
    """
    try:
        # Validate matcher is initialized
        if matcher is None:
            return jsonify({
                "error": "ResumeMatcher service unavailable"
            }), 503
        
        # Parse JSON request
        data = request.get_json()
        if not data:
            return jsonify({
                "error": "Request body must be JSON"
            }), 400
        
        # Extract and validate job description
        job_description = data.get('jobDescription', '').strip()
        if not job_description:
            return jsonify({
                "error": "jobDescription is required and cannot be empty"
            }), 400
        
        if len(job_description) > MAX_JOB_DESCRIPTION_LENGTH:
            return jsonify({
                "error": f"jobDescription exceeds maximum length of {MAX_JOB_DESCRIPTION_LENGTH} characters"
            }), 400
        
        # Extract and validate resumes
        resumes = data.get('resumes', [])
        if not isinstance(resumes, list):
            return jsonify({
                "error": "resumes must be a list"
            }), 400
        
        if len(resumes) < MIN_RESUMES:
            return jsonify({
                "error": f"At least {MIN_RESUMES} resume(s) required"
            }), 400
        
        if len(resumes) > MAX_RESUMES:
            return jsonify({
                "error": f"Maximum {MAX_RESUMES} resumes allowed"
            }), 400
        
        # Validate resume structure
        validated_resumes = []
        for idx, resume in enumerate(resumes):
            if not isinstance(resume, dict):
                return jsonify({
                    "error": f"Resume at index {idx} must be an object"
                }), 400
            
            required_fields = ['ID', 'text', 'Category']
            for field in required_fields:
                if field not in resume:
                    return jsonify({
                        "error": f"Resume at index {idx} is missing required field: {field}"
                    }), 400
            
            resume_text = str(resume['text']).strip()
            if not resume_text:
                return jsonify({
                    "error": f"Resume at index {idx} has empty text field"
                }), 400
            
            if len(resume_text) > MAX_RESUME_TEXT_LENGTH:
                return jsonify({
                    "error": f"Resume at index {idx} text exceeds maximum length of {MAX_RESUME_TEXT_LENGTH} characters"
                }), 400
            
            validated_resumes.append({
                'ID': resume['ID'],
                'text': resume_text,
                'Category': str(resume['Category']).strip()
            })
        
        # Perform matching
        print(f"\n📋 MATCHING REQUEST:")
        print(f"   Job Description: {job_description[:80]}...")
        print(f"   Resume Count: {len(validated_resumes)}")
        for r in validated_resumes:
            print(f"   - {r['Category']}: {r['text'][:60]}... ({len(r['text'])} chars)")
        
        results_df = matcher.match_resumes(job_description, validated_resumes)
        
        print(f"\n📊 MATCHING RESULTS:")
        for _, row in results_df.iterrows():
            print(f"   Resume ID {row['resumeId']}: {row['similarity']:.2%} similarity")
        
        # Convert results to JSON format
        matched_resumes = []
        for _, row in results_df.iterrows():
            matched_resumes.append({
                "resumeId": int(row['resumeId']),
                "similarity": float(row['similarity']),
                "domainResume": str(row['domainResume']),
                "domainDesc": str(row['domainDesc']),
                "jobId": int(row['jobId'])
            })
        
        return jsonify({
            "matchedResumes": matched_resumes,
            "totalMatches": len(matched_resumes),
            "jobDescription": job_description[:100] + "..." if len(job_description) > 100 else job_description
        }), 200
    
    except Exception as e:
        print(f"Error in predict endpoint: {str(e)}")
        return jsonify({
            "error": f"Prediction failed: {str(e)}"
        }), 500


if __name__ == '__main__':
    app.run(debug=DEBUG_MODE, host=API_HOST, port=API_PORT)
