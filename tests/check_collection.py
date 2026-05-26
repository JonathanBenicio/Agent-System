import json, sys
sys.stdout.reconfigure(encoding='utf-8')
d = json.load(open('tests/postman/AgenticSystem.postman_collection.json', encoding='utf-8'))
info = d.get('info', {})
print("Name:", info.get('name', '?'))
print("Schema:", info.get('schema', '?'))
print("Folders:", len(d.get('item', [])))
print("Variables:", len(d.get('variable', [])))
