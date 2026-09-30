import json

with open('swagger.json', 'r', encoding='utf-8') as f:
    data = json.load(f)

print("Paths in swagger.json:")
for path in list(data.get('paths', {}).keys())[:20]:
    print(path)

# Let's search for cancel
print("\nPaths containing 'cancel':")
for path in data.get('paths', {}).keys():
    if 'cancel' in path.lower():
        print(path)
        print(data['paths'][path])
