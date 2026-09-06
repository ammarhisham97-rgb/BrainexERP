"""
NovaERP — Credit Card Fraud Detection Inference API
Rule-based fraud detection system for payment validation
"""

from flask import Flask, request, jsonify
from flask_cors import CORS
import numpy as np
import os
from datetime import datetime

app = Flask(__name__)
CORS(app)


def calculate_fraud_score(features):
    """
    Calculate fraud probability based on heuristic rules
    Features format: [fraudScore, amount, paymentRatio, daysLate, hour, overpayment, is_credit_card, 
                      is_bank_transfer, is_cash, is_weekend, ...]
    """
    if len(features) < 10:
        return 0.0
    
    fraud_score = features[0]  # Already calculated on frontend (0-1 normalized)
    
    # Fraud probability based on fraud score
    # 0-2 points: 0-20% fraud probability
    # 2-4 points: 20-40% fraud probability  
    # 4-6 points: 40-60% fraud probability
    # 6-8 points: 60-80% fraud probability
    # 8-10 points: 80-100% fraud probability
    
    # But we use a more lenient threshold
    # Only flag as fraudulent if fraud_score > 6 (≈60% probability)
    fraud_probability = min(fraud_score * 10, 1.0)  # Convert 0-1 to 0-100
    
    return fraud_probability


@app.route("/predict", methods=["POST"])
def predict():
    """
    Predict whether a payment transaction is fraudulent using rule-based detection.

    Expected JSON payload:
    {
        "features": [list of features from frontend]
    }
    OR
    {
        "transactionFeatures": [list of features]
    }

    Returns:
    {
        "prediction": 0 | 1,
        "label": "Legitimate" | "Fraudulent",
        "fraud_probability": float (0-1),
        "confidence": float (0-100)
    }
    """
    try:
        data = request.get_json()
        
        # Accept both "features" and "transactionFeatures" keys
        features_list = data.get("features") or data.get("transactionFeatures")
        
        if features_list is None:
            return jsonify({"error": "Missing 'features' or 'transactionFeatures' in request body"}), 400
        
        if len(features_list) < 10:
            return jsonify({"error": f"Expected at least 10 features, got {len(features_list)}"}), 400
        
        features = np.array(features_list, dtype=float)
        
        # Calculate fraud probability using rule-based system
        fraud_probability = calculate_fraud_score(features)
        
        # Use threshold of 0.6 (60%) to flag as fraudulent
        prediction = 1 if fraud_probability >= 0.6 else 0
        label = "Fraudulent" if prediction == 1 else "Legitimate"
        confidence = abs(fraud_probability - 0.5) * 200  # Convert to 0-100 scale
        
        print(f"✓ Fraud Detection: {label} (probability: {fraud_probability:.4f}, confidence: {confidence:.2f}%)")

        return jsonify({
            "prediction": prediction,
            "label": label,
            "fraud_probability": round(fraud_probability * 100, 2),
            "confidence": round(confidence, 2)
        })

    except ValueError as e:
        print(f"✗ Validation error: {e}")
        return jsonify({"error": f"Invalid features format: {str(e)}"}), 400
    except Exception as e:
        print(f"✗ Prediction error: {e}")
        return jsonify({"error": str(e)}), 400


@app.route("/health", methods=["GET"])
def health():
    """Health check endpoint"""
    return jsonify({
        "status": "API is running",
        "fraud_detection": "Rule-based system active"
    }), 200


if __name__ == "__main__":
    app.run(debug=False, host="0.0.0.0", port=5003)
