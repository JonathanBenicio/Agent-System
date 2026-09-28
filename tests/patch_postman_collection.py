import json
import os

file_path = 'tests/postman/AgenticSystem.postman_collection.json'

if not os.path.exists(file_path):
    print(f"Error: Collection file not found at {file_path}")
    exit(1)

with open(file_path, 'r', encoding='utf-8') as f:
    collection = json.load(f)

# Define the updates
# We will match by url path and method.
# For path matching, we will check if segments match.

def match_url(url_obj, target_path_segments):
    if not url_obj:
        return False

    # Extract path list
    path_arr = url_obj.get('path', [])
    if not isinstance(path_arr, list):
        return False

    # Reconstruct path string
    path_str = "/" + "/".join(path_arr).lower()

    # Normalize target path
    target = "/" + "/".join(target_path_segments).lower()

    # Simple replacement of placeholders to check match
    # e.g., /api/agent/agents/:name/rooms -> /api/agent/agents/{name}/rooms
    norm_path = path_str.replace(':', '{').replace('}', '}')
    norm_target = target.replace(':', '{').replace('}', '}')

    # Compare segments count
    if len(path_arr) != len(target_path_segments):
        return False

    for p_seg, t_seg in zip(path_arr, target_path_segments):
        p_seg = p_seg.lower()
        t_seg = t_seg.lower()
        if p_seg.startswith(':') or p_seg.startswith('{') or t_seg.startswith(':') or t_seg.startswith('{'):
            continue
        if p_seg != t_seg:
            return False

    return True

def update_item_bodies(item_list):
    updated_count = 0
    for item in item_list:
        if 'item' in item:
            updated_count += update_item_bodies(item['item'])
        elif 'request' in item:
            req = item['request']
            method = req.get('method', 'GET').upper()
            url_info = req.get('url', {})

            # 1. agents/{name}/rooms (PUT / POST)
            if method in ['PUT', 'POST'] and match_url(url_info, ['api', 'agent', 'agents', '{name}', 'rooms']):
                req['body'] = {
                    "mode": "raw",
                    "raw": "[\n  \"e2e-room-id\"\n]",
                    "options": {
                        "raw": {
                            "language": "json"
                        }
                    }
                }
                print(f"Updated: {method} {item.get('name')}")
                updated_count += 1

            # 2. skills/upload (POST)
            elif method == 'POST' and match_url(url_info, ['api', 'agent', 'skills', 'upload']):
                req['body'] = {
                    "mode": "formdata",
                    "formdata": [
                        {
                            "key": "file",
                            "type": "file",
                            "src": []
                        }
                    ]
                }
                print(f"Updated: {method} {item.get('name')}")
                updated_count += 1

            # 3. Document/ingest (POST)
            elif method == 'POST' and match_url(url_info, ['api', 'Document', 'ingest']):
                req['body'] = {
                    "mode": "formdata",
                    "formdata": [
                        {
                            "key": "file",
                            "type": "file",
                            "src": []
                        }
                    ]
                }
                print(f"Updated: {method} {item.get('name')}")
                updated_count += 1

            # 4. Document/ingest/batch (POST)
            elif method == 'POST' and match_url(url_info, ['api', 'Document', 'ingest', 'batch']):
                req['body'] = {
                    "mode": "formdata",
                    "formdata": [
                        {
                            "key": "files",
                            "type": "file",
                            "src": []
                        }
                    ]
                }
                print(f"Updated: {method} {item.get('name')}")
                updated_count += 1

            # 5. plugins/{pluginId}/tools/{toolName}/execute (POST)
            elif method == 'POST' and match_url(url_info, ['api', 'admin', 'plugins', '{pluginId}', 'tools', '{toolName}', 'execute']):
                req['body'] = {
                    "mode": "raw",
                    "raw": "{\n  \"prompt\": \"Executar ferramenta de teste\"\n}",
                    "options": {
                        "raw": {
                            "language": "json"
                        }
                    }
                }
                print(f"Updated: {method} {item.get('name')}")
                updated_count += 1

            # 6. scheduled-tasks/channels/{name}/test (POST)
            elif method == 'POST' and match_url(url_info, ['api', 'admin', 'scheduled-tasks', 'channels', '{name}', 'test']):
                req['body'] = {
                    "mode": "raw",
                    "raw": "{\n  \"webhookUrl\": \"http://localhost:8080/api/webhooks/receive/test\"\n}",
                    "options": {
                        "raw": {
                            "language": "json"
                        }
                    }
                }
                print(f"Updated: {method} {item.get('name')}")
                updated_count += 1

            # 7. Webhooks/receive/{id} (POST)
            elif method == 'POST' and match_url(url_info, ['api', 'Webhooks', 'receive', '{id}']):
                req['body'] = {
                    "mode": "raw",
                    "raw": "{\n  \"event\": \"test-webhook\",\n  \"data\": {\n    \"message\": \"Olá do Postman E2E!\"\n  }\n}",
                    "options": {
                        "raw": {
                            "language": "json"
                        }
                    }
                }
                print(f"Updated: {method} {item.get('name')}")
                updated_count += 1

    return updated_count

print("Starting Postman Collection Patch...")
total_updated = update_item_bodies(collection.get('item', []))
print(f"Total endpoints updated: {total_updated}")

if total_updated > 0:
    # Save the updated collection
    with open(file_path, 'w', encoding='utf-8') as f:
        json.dump(collection, f, indent=2, ensure_ascii=False)
    print("Postman Collection updated successfully and saved!")
else:
    print("No matching endpoints found to update.")
