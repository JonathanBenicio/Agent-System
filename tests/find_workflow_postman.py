import json
import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('tests/postman/AgenticSystem.postman_collection.json', 'r', encoding='utf-8') as f:
    d = json.load(f)

for folder in d.get('item', []):
    if 'Workflow' in folder.get('name', '') or 'workflow' in folder.get('name', '').lower():
        print(f"Folder: {folder.get('name')}")
        for req in folder.get('item', []):
            req_name = req.get('name', '')
            req_url = req.get('request', {}).get('url', {}).get('raw', '')
            print(f"  Req: {req_name} | URL: {req_url}")
