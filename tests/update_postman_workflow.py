import json

file_path = 'tests/postman/AgenticSystem.postman_collection.json'

with open(file_path, 'r', encoding='utf-8') as f:
    collection = json.load(f)

for folder in collection.get('item', []):
    if folder.get('name') == '🔄 Engine de Workflows':
        for req in folder.get('item', []):
            if req.get('name') == '[POST] /api/workflow/executions/start/{definitionId}':
                print("Found request. Current body:")
                print(json.dumps(req.get('request', {}).get('body', {}), indent=2))

                # Ensure body is defined
                if 'request' in req:
                    if 'body' not in req['request']:
                        req['request']['body'] = {
                            "mode": "raw",
                            "raw": "{\n  \"input\": \"test\"\n}",
                            "options": {
                                "raw": {
                                    "language": "json"
                                }
                            }
                        }
                    else:
                        req['request']['body']['mode'] = "raw"
                        # We change the raw payload to represent a Dictionary<string, object>
                        req['request']['body']['raw'] = "{\n  \"contextVar1\": \"value1\",\n  \"contextVar2\": 42\n}"
                        if 'options' not in req['request']['body']:
                            req['request']['body']['options'] = {
                                "raw": {
                                    "language": "json"
                                }
                            }

with open(file_path, 'w', encoding='utf-8') as f:
    json.dump(collection, f, indent=4, ensure_ascii=False)
print("Updated collection.")
