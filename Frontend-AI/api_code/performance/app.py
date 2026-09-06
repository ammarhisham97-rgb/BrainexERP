from flask import Flask, request, jsonify
from flask_cors import CORS
import pickle
import json
import numpy as np
import pandas as pd
import os

app = Flask(__name__)
CORS(app)  # Enable CORS for all routes

# ---------------------------------------------------------------------------
# Resolve model paths relative to this script (-> ../../models/performance/)
# ---------------------------------------------------------------------------
MODEL_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "models", "performance")

try:
    model = pickle.load(open(os.path.join(MODEL_DIR, "random_forest_regressor_model.pkl"), "rb"))
    scaler = pickle.load(open(os.path.join(MODEL_DIR, "scaler.pkl"), "rb"))
    encoder = pickle.load(open(os.path.join(MODEL_DIR, "encoder.pkl"), "rb"))
    
    # Load feature metadata
    with open(os.path.join(MODEL_DIR, "feature_metadata.json"), "r") as f:
        metadata = json.load(f)
    
    numerical_features = metadata['numerical_features']
    categorical_features = metadata['categorical_features']
    
    print("✓ Model, scaler, encoder, and metadata loaded successfully")
except FileNotFoundError as e:
    print(f"Error: {e}")
    print("Run 'python generate_pickle_files.py' first to create all required files.")

def preprocess_employee_data(employee_dict):
    """
    Preprocess raw employee data (dict) into scaled/encoded features
    """
    try:
        # Convert to DataFrame
        df = pd.DataFrame([employee_dict])
        
        # Drop non-feature columns
        cols_to_drop = [col for col in ['Employee_ID', 'Hire_Date', 'Performance_Score', 'Name'] if col in df.columns]
        if cols_to_drop:
            df = df.drop(columns=cols_to_drop)
        
        # Ensure all required columns exist
        for col in numerical_features:
            if col not in df.columns:
                df[col] = 0
            df[col] = pd.to_numeric(df[col], errors='coerce').fillna(0)
        
        for col in categorical_features:
            if col not in df.columns:
                df[col] = 'Unknown'
        
        # Select only the required features in the correct order
        df_numerical = df[numerical_features]
        df_categorical = df[categorical_features]
        
        # Scale numerical features
        scaled_numerical = scaler.transform(df_numerical)
        
        # Encode categorical features
        encoded_categorical = encoder.transform(df_categorical)
        
        # Combine
        final_features = np.concatenate([scaled_numerical, encoded_categorical], axis=1)
        
        return final_features[0]
    except Exception as e:
        raise Exception(f"Preprocessing error: {str(e)}")

@app.route('/predict', methods=['POST'])
def predict():
    """
    API endpoint for Employee Performance prediction
    Expected JSON input: {"employee": {employee_data_dict}}
    Returns: {"performance_score": float, "rating": "string"}
    """
    try:
        data = request.get_json()
        
        if "employee" in data:
            # Raw employee data provided
            features = preprocess_employee_data(data["employee"]).reshape(1, -1)
        elif "features" in data:
            # Already preprocessed features provided
            features = np.array(data["features"]).reshape(1, -1)
        else:
            raise ValueError("Request must contain 'employee' or 'features' key")

        # Make prediction
        performance_score = model.predict(features)[0]

        # Determine rating
        if performance_score >= 4.5:
            rating = "Excellent"
        elif performance_score >= 4.0:
            rating = "Very Good"
        elif performance_score >= 3.5:
            rating = "Good"
        elif performance_score >= 3.0:
            rating = "Satisfactory"
        else:
            rating = "Needs Improvement"

        return jsonify({
            "performance_score": round(float(performance_score), 2),
            "rating": rating
        })
    except Exception as e:
        return jsonify({"error": str(e)}), 400

@app.route('/predict-batch', methods=['POST'])
def predict_batch():
    """
    Batch prediction endpoint for multiple employees
    Expected JSON input: {"employees": [list of employee dicts]}
    Returns: list of predictions
    """
    try:
        data = request.get_json()
        
        if "employees" not in data:
            raise ValueError("Request must contain 'employees' key")
        
        employees = data["employees"]
        features_list = []
        
        for emp in employees:
            try:
                features = preprocess_employee_data(emp)
                features_list.append(features)
            except Exception as e:
                print(f"Error preprocessing employee: {e}")
                raise
        
        features = np.array(features_list)
        
        # Make predictions
        predictions = model.predict(features)

        results = []
        for score in predictions:
            if score >= 4.5:
                rating = "Excellent"
            elif score >= 4.0:
                rating = "Very Good"
            elif score >= 3.5:
                rating = "Good"
            elif score >= 3.0:
                rating = "Satisfactory"
            else:
                rating = "Needs Improvement"
            
            results.append({
                "performance_score": round(float(score), 2),
                "rating": rating
            })

        return jsonify({"predictions": results})
    except Exception as e:
        return jsonify({"error": str(e)}), 400

@app.route('/health', methods=['GET'])
def health():
    """Health check endpoint"""
    return jsonify({"status": "API is running"}), 200

if __name__ == '__main__':
    app.run(debug=False, host='0.0.0.0', port=5002)
