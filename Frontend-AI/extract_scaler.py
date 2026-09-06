import pickle
import warnings
warnings.filterwarnings('ignore')

s = pickle.load(open('models/hr_attrition/scaler.pkl', 'rb'))
print("Scaler type:", type(s))
print("Scaler mean (first 10):", s.mean_[:10])
print("Scaler scale (first 10):", s.scale_[:10])
print("Scaler feature_names_in_:", s.feature_names_in_)
