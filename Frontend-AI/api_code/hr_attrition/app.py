from flask import Flask, request, jsonify
from flask_cors import CORS
import pickle
import numpy as np
import pandas as pd
import os

app = Flask(__name__)
CORS(app)  # Enable CORS for all routes

# ---------------------------------------------------------------------------
# Resolve model paths relative to this script (-> ../../models/hr_attrition/)
# ---------------------------------------------------------------------------
MODEL_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "models", "hr_attrition")

model = pickle.load(open(os.path.join(MODEL_DIR, "model.pkl"), "rb"))
scaler = pickle.load(open(os.path.join(MODEL_DIR, "scaler.pkl"), "rb"))

@app.route('/predict', methods=['POST'])
def predict():
    """
    API endpoint for HR Attrition prediction
    Expected JSON input: {"features": [list of 30 features]}
    Returns: {"prediction": 0 or 1, "risk": "Low" or "High"}
    """
    try:
        data = request.get_json()
        features = np.array(data["features"]).reshape(1, -1)
        
        # Scale features (suppress the feature names warning)
        import warnings
        with warnings.catch_warnings():
            warnings.simplefilter("ignore")
            scaled_features = scaler.transform(features)
        
        # Make prediction
        prediction = model.predict(scaled_features)[0]
        prediction_proba = model.predict_proba(scaled_features)[0]
        
        # Determine risk level
        risk_score = prediction_proba[1]
        risk_level = "High Risk" if prediction == 1 else "Low Risk"
        
        return jsonify({
            "prediction": int(prediction),
            "risk_level": risk_level,
            "confidence": round(max(prediction_proba) * 100, 2),
            "attrition_probability": round(risk_score * 100, 2)
        })
    except Exception as e:
        return jsonify({"error": str(e)}), 400

@app.route('/health', methods=['GET'])
def health():
    """Health check endpoint"""
    return jsonify({"status": "API is running"}), 200

if __name__ == '__main__':
    app.run(debug=False, host='0.0.0.0', port=5001)
