import json
import sys

sys.stdout.reconfigure(encoding='utf-8')

file_path = 'tests/postman/AgenticSystem.postman_collection.json'

with open(file_path, 'r', encoding='utf-8') as f:
    collection = json.load(f)

def list_folders(item_list, prefix=""):
    for item in item_list:
        if 'item' in item:
            print(f"Folder: {prefix}{item.get('name')}")
            list_folders(item['item'], prefix + "  ")

list_folders(collection.get('item', []))
