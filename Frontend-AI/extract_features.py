import pickle
import warnings
warnings.filterwarnings('ignore')

m = pickle.load(open('models/hr_attrition/model.pkl', 'rb'))
print("Feature Names:")
for i, name in enumerate(m.feature_names_in_):
    print(f"  {i}: {name}")
print(f"\nTotal Features: {m.n_features_in_}")
