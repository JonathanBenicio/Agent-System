import json
import sys

sys.stdout.reconfigure(encoding='utf-8')

file_path = 'tests/postman/AgenticSystem.postman_collection.json'

with open(file_path, 'r', encoding='utf-8') as f:
    collection = json.load(f)

# Define the automatic extraction test script for IngestDocument
ingest_test_script = [
    "// 1. Validação padrão de Status Code operacional",
    "pm.test(\"Status code operacional aceito\", function () {",
    "    pm.expect(pm.response.code).to.be.oneOf([200, 201, 202, 204]);",
    "});",
    "",
    "// 2. Validação padrão de Tempo de Resposta (Performance)",
    "pm.test(\"Tempo de resposta dentro do limite de 1000ms\", function () {",
    "    pm.expect(pm.response.responseTime).to.be.below(1000);",
    "});",
    "",
    "// 3. Extração do caminho físico do arquivo no disco do servidor",
    "if (pm.response.code === 200) {",
    "    var jsonData = pm.response.json();",
    "    if (jsonData.fileDiskPath) {",
    "        pm.collectionVariables.set(\"ingested_file_path\", jsonData.fileDiskPath);",
    "        console.log(\"ingested_file_path definida com sucesso:\", jsonData.fileDiskPath);",
    "    }",
    "}"
]

def update_collection_structure(item_list):
    updated = 0
    for item in item_list:
        if 'item' in item:
            updated += update_collection_structure(item['item'])
        elif 'request' in item:
            req = item['request']
            name = item.get('name', '')
            method = req.get('method', 'GET').upper()

            # 1. Update IngestDocument to extract fileDiskPath
            if method == 'POST' and name == '[POST] /api/Document/ingest':
                # Update tests script
                item['event'] = [
                    {
                        "listen": "test",
                        "script": {
                            "exec": ingest_test_script,
                            "type": "text/javascript"
                        }
                    }
                ]
                print(f"Updated test scripts for IngestDocument: {name}")
                updated += 1

            # 2. Update E2E Workflow request body to use variable
            elif method == 'POST' and name == '[POST] Iniciar Workflow banner-production (E2E)':
                req['body'] = {
                    "mode": "raw",
                    "raw": "{\n  \"imagePath\": \"{{ingested_file_path}}\",\n  \"price\": 320000,\n  \"bedrooms\": 3\n}",
                    "options": {
                        "raw": {
                            "language": "json"
                        }
                    }
                }
                print(f"Updated body of E2E Workflow request: {name}")
                updated += 1

            # 3. Update E2E Chat request body to use variable
            elif method == 'POST' and name == '[POST] Enviar Comando de Workflow no Chat (E2E)':
                req['body'] = {
                    "mode": "raw",
                    "raw": "{\n  \"message\": \"Por favor, produza um banner para o imóvel de 3 quartos com preço de R$ 320.000 usando a foto {{ingested_file_path}}.\",\n  \"targetAgent\": \"EditorChefe\",\n  \"context\": {\n    \"imagePath\": \"{{ingested_file_path}}\",\n    \"price\": 320000,\n    \"bedrooms\": 3\n  }\n}",
                    "options": {
                        "raw": {
                            "language": "json"
                        }
                    }
                }
                print(f"Updated body of E2E Chat request: {name}")
                updated += 1

    return updated

print("Starting automation adjustments on Postman collection...")
total_adjusted = update_collection_structure(collection.get('item', []))

# 4. Add "ingested_file_path" to collection variables if not present
vars_list = collection.get('variable', [])
var_exists = any(v.get('key') == 'ingested_file_path' for v in vars_list)
if not var_exists:
    vars_list.append({
        "key": "ingested_file_path",
        "value": "/assets/apartment-front.jpg",
        "type": "string",
        "description": "Caminho físico absoluto da foto ingerida no disco do servidor local, alimentado dinamicamente pelo upload."
    })
    collection['variable'] = vars_list
    print("Added 'ingested_file_path' to collection variables!")

print(f"Total elements adjusted: {total_adjusted}")

if total_adjusted > 0 or not var_exists:
    with open(file_path, 'w', encoding='utf-8') as f:
        json.dump(collection, f, indent=2, ensure_ascii=False)
    print("Postman collection updated successfully with dynamic path automation!")
else:
    print("No elements needed adjustment.")
