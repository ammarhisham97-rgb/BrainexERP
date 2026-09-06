"""
This script saves the scaler and encoder pickle files needed for the Flask API.
It loads the data, trains the model components, and saves them.
Run this once to generate the required .pkl files.
"""

import pandas as pd
import pickle
import json
from sklearn.preprocessing import StandardScaler, OneHotEncoder
from sklearn.ensemble import RandomForestRegressor
from sklearn.model_selection import train_test_split

print("=" * 70)
print("🔧 GENERATING REQUIRED PICKLE FILES")
print("=" * 70)

# Load the data
print("\n1️⃣  Loading data...")
new_data = pd.read_csv('Extended_Employee_Performance_and_Productivity_Data.csv')
print(f"   ✓ Loaded {len(new_data)} employee records")

# Prepare data
print("\n2️⃣  Preparing data...")
data_for_new_target = new_data.drop(columns=['Employee_ID', 'Hire_Date']).copy()
X_new_target = data_for_new_target.drop(columns=['Performance_Score'])
y_new_target = data_for_new_target['Performance_Score']

categorical_features = list(X_new_target.select_dtypes(include=['object']).columns)
numerical_features = list(X_new_target.select_dtypes(include=['int64', 'float64']).columns)
print(f"   ✓ Categorical features: {categorical_features}")
print(f"   ✓ Numerical features: {numerical_features}")

# Fit and save encoder
print("\n3️⃣  Saving OneHotEncoder...")
encoder = OneHotEncoder(sparse_output=False, handle_unknown='ignore')
encoder.fit(X_new_target[categorical_features])

with open('encoder.pkl', 'wb') as file:
    pickle.dump(encoder, file)
print("   ✓ encoder.pkl saved successfully")

# Fit and save scaler
print("\n4️⃣  Saving StandardScaler...")
scaler = StandardScaler()
scaler.fit(X_new_target[numerical_features])

with open('scaler.pkl', 'wb') as file:
    pickle.dump(scaler, file)
print("   ✓ scaler.pkl saved successfully")

# Save feature metadata
print("\n5️⃣  Saving feature metadata...")
metadata = {
    'numerical_features': numerical_features,
    'categorical_features': categorical_features,
    'all_features': numerical_features + categorical_features
}

with open('feature_metadata.json', 'w') as file:
    json.dump(metadata, file, indent=2)
print("   ✓ feature_metadata.json saved successfully")

# Train and save model (if it doesn't exist)
try:
    with open('random_forest_regressor_model.pkl', 'rb') as file:
        model = pickle.load(file)
    print("\n6️⃣  ✓ random_forest_regressor_model.pkl already exists")
except FileNotFoundError:
    print("\n6️⃣  Training RandomForestRegressor...")
    # Encode and scale features
    encoded_features = encoder.transform(X_new_target[categorical_features])
    scaled_features = scaler.transform(X_new_target[numerical_features])
    
    encoded_df = pd.DataFrame(encoded_features, index=X_new_target.index)
    scaled_df = pd.DataFrame(scaled_features, columns=numerical_features, index=X_new_target.index)
    
    X_preprocessed = pd.concat([scaled_df, encoded_df], axis=1)
    
    # Split and train
    X_train, X_test, y_train, y_test = train_test_split(X_preprocessed, y_new_target, test_size=0.2, random_state=42)
    
    model = RandomForestRegressor(random_state=42)
    model.fit(X_train, y_train)
    
    with open('random_forest_regressor_model.pkl', 'wb') as file:
        pickle.dump(model, file)
    print("   ✓ random_forest_regressor_model.pkl saved successfully")

print("\n" + "=" * 70)
print("✅ ALL PICKLE FILES GENERATED SUCCESSFULLY!")
print("=" * 70)
print("\n📁 Files created:")
print("   • encoder.pkl")
print("   • scaler.pkl")
print("   • feature_metadata.json")
print("   • random_forest_regressor_model.pkl")
print("\n🚀 Now you can run: python run_server.py\n")
