import json

def load_json(filepath):
    with open(filepath, 'r', encoding='utf-8') as f:
        return json.load(f)

# Load swagger and postman
swagger = load_json('swagger.json')
postman = load_json('tests/postman/AgenticSystem.postman_collection.json')

# Build a map of required bodies from Swagger
# Map format: { "/api/endpoint": { "post": True, "put": False } }
# True if it expects a body, False if it doesn't
swagger_reqs = {}
for path, methods in swagger.get('paths', {}).items():
    # Postman sometimes has {{base_url}}/api/... we will normalize to /api/...
    norm_path = path.lower()
    if not norm_path.startswith('/'):
        norm_path = '/' + norm_path

    swagger_reqs[norm_path] = {}
    for method, details in methods.items():
        if method.lower() not in ['get', 'post', 'put', 'patch', 'delete']:
            continue

        has_body = False
        # In OpenAPI 3, body is in requestBody
        if 'requestBody' in details:
            has_body = True
        # In Swagger 2, body is a parameter with 'in': 'body'
        elif 'parameters' in details:
            for param in details['parameters']:
                if param.get('in') == 'body':
                    has_body = True
                    break

        swagger_reqs[norm_path][method.lower()] = has_body

# Recursively extract all requests from Postman
postman_requests = []
def extract_requests(item_list):
    for item in item_list:
        if 'item' in item:
            extract_requests(item['item'])
        elif 'request' in item:
            postman_requests.append(item)

extract_requests(postman.get('item', []))

# Cross-reference
print("=== ENDPOINTS WITH MISSING BODIES IN POSTMAN ===")
missing_bodies = 0
found_in_swagger = 0

for req_item in postman_requests:
    req = req_item.get('request', {})
    if not req: continue

    url_info = req.get('url', {})
    if isinstance(url_info, dict):
        path_arr = url_info.get('path', [])
        # reconstruct path
        raw_path = "/" + "/".join(path_arr)
    elif isinstance(url_info, str):
        # basic extraction, assuming it starts with {{base_url}}
        raw_path = url_info.replace('{{base_url}}', '')
        if not raw_path.startswith('/'):
            raw_path = '/' + raw_path
    else:
        continue

    method = req.get('method', 'GET').lower()
    norm_path = raw_path.lower()

    # Handle path variable replacements from postman (e.g. /users/:id to /users/{id})
    # Since Swagger uses {id}, we might need a loose match or regex if exact match fails
    # For now, let's try direct lookup or prefix match
    matched_swagger_path = None
    if norm_path in swagger_reqs:
        matched_swagger_path = norm_path
    else:
        # try to match by segments
        pm_segments = norm_path.strip('/').split('/')
        for sw_path in swagger_reqs.keys():
            sw_segments = sw_path.strip('/').split('/')
            if len(pm_segments) == len(sw_segments):
                match = True
                for i in range(len(pm_segments)):
                    if pm_segments[i].startswith(':') or (sw_segments[i].startswith('{') and sw_segments[i].endswith('}')):
                        continue # variable segment matches
                    if pm_segments[i] != sw_segments[i]:
                        match = False
                        break
                if match:
                    matched_swagger_path = sw_path
                    break

    if matched_swagger_path:
        found_in_swagger += 1
        requires_body = swagger_reqs[matched_swagger_path].get(method, False)

        if requires_body:
            # Check if Postman request has a valid body
            body_info = req.get('body', {})
            mode = body_info.get('mode')
            has_valid_body = False

            if mode == 'raw':
                raw_content = body_info.get('raw', '').strip()
                if raw_content and raw_content != '{}' and raw_content != '""':
                    has_valid_body = True
            elif mode == 'formdata' or mode == 'urlencoded':
                if len(body_info.get(mode, [])) > 0:
                    has_valid_body = True

            if not has_valid_body:
                missing_bodies += 1
                print(f"[MISSING BODY] {method.upper()} {raw_path}")
                print(f"  -> {req_item.get('name')}")
                print()

print(f"Analyzed {found_in_swagger} endpoints present in both Swagger and Postman.")
if missing_bodies == 0:
    print("SUCCESS: All requests requiring a body seem to have one configured in Postman!")
else:
    print(f"FAIL: Found {missing_bodies} requests that need a body but are empty or '{{}}'.")
