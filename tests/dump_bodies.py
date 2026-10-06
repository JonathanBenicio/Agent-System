import json

file_path = 'tests/postman/AgenticSystem.postman_collection.json'

with open(file_path, 'r', encoding='utf-8') as f:
    collection = json.load(f)

def check_req(item_list):
    for item in item_list:
        if 'item' in item:
            check_req(item['item'])
        elif 'request' in item:
            url_info = item['request'].get('url', {})
            raw_url = url_info.get('raw', '') if isinstance(url_info, dict) else url_info
            # URLs to check based on previous output
            targets = [
                'agents/', 'skills/upload', 'document/ingest',
                'plugins/', 'channels/', 'webhooks/', 'workflow/'
            ]

            for t in targets:
                if t in raw_url.lower():
                    method = item['request'].get('method', 'GET')
                    if method in ['POST', 'PUT']:
                        print(f"Req: {item.get('name')} | {method} {raw_url}")
                        print("Body:", json.dumps(item['request'].get('body', {}), indent=2))
                        print("-" * 40)
                        break

check_req(collection.get('item', []))
