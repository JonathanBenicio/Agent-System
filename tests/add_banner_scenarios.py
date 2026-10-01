import json
import os
import uuid
import sys

sys.stdout.reconfigure(encoding='utf-8')

file_path = 'tests/postman/AgenticSystem.postman_collection.json'

if not os.path.exists(file_path):
    print(f"Error: Collection file not found at {file_path}")
    exit(1)

with open(file_path, 'r', encoding='utf-8') as f:
    collection = json.load(f)

# Request 1: Start Banner Production Workflow (E2E)
workflow_request = {
    "id": str(uuid.uuid4()),
    "name": "[POST] Iniciar Workflow banner-production (E2E)",
    "request": {
        "method": "POST",
        "header": [
            {
                "key": "X-Api-Key",
                "value": "{{api_key}}",
                "type": "text"
            },
            {
                "key": "X-Tenant-Id",
                "value": "{{tenant_id}}",
                "type": "text"
            },
            {
                "key": "Authorization",
                "value": "Bearer {{jwt_token}}",
                "type": "text"
            }
        ],
        "url": {
            "raw": "{{base_url}}/api/workflow/executions/start/banner-production",
            "host": [
                "{{base_url}}"
            ],
            "path": [
                "api",
                "workflow",
                "executions",
                "start",
                "banner-production"
            ]
        },
        "body": {
            "mode": "raw",
            "raw": "{\n  \"imagePath\": \"/assets/apartment-front.jpg\",\n  \"price\": 320000,\n  \"bedrooms\": 3\n}",
            "options": {
                "raw": {
                    "language": "json"
                }
            }
        }
    },
    "event": [
        {
            "listen": "test",
            "script": {
                "exec": [
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
                    "// 3. Extração do Run ID para acompanhamento do progresso",
                    "if (pm.response.code === 200 || pm.response.code === 202) {",
                    "    var jsonData = pm.response.json();",
                    "    var runId = jsonData.executionId || jsonData.runId || jsonData.id;",
                    "    if (runId) {",
                    "        pm.collectionVariables.set(\"workflow_run_id\", runId);",
                    "        console.log(\"workflow_run_id definida com sucesso:\", runId);",
                    "    }",
                    "}"
                ],
                "type": "text/javascript"
              }
        }
    ]
}

# Request 2: Send Command to banner-production via Chat (E2E)
chat_request = {
    "id": str(uuid.uuid4()),
    "name": "[POST] Enviar Comando de Workflow no Chat (E2E)",
    "request": {
        "method": "POST",
        "header": [
            {
                "key": "X-Api-Key",
                "value": "{{api_key}}",
                "type": "text"
            },
            {
                "key": "X-Tenant-Id",
                "value": "{{tenant_id}}",
                "type": "text"
            },
            {
                "key": "Authorization",
                "value": "Bearer {{jwt_token}}",
                "type": "text"
            }
        ],
        "url": {
            "raw": "{{base_url}}/api/chat",
            "host": [
                "{{base_url}}"
            ],
            "path": [
                "api",
                "chat"
            ]
        },
        "body": {
            "mode": "raw",
            "raw": "{\n  \"message\": \"Por favor, produza um banner para o imóvel de 3 quartos com preço de R$ 320.000 usando a foto /assets/apartment-front.jpg.\",\n  \"targetAgent\": \"EditorChefe\",\n  \"context\": {\n    \"imagePath\": \"/assets/apartment-front.jpg\",\n    \"price\": 320000,\n    \"bedrooms\": 3\n  }\n}",
            "options": {
                "raw": {
                    "language": "json"
                }
            }
        }
    },
    "event": [
        {
            "listen": "test",
            "script": {
                "exec": [
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
                    "// 3. Validação de retorno no chat",
                    "if (pm.response.text().length > 0) {",
                    "    var jsonData = pm.response.json();",
                    "    pm.test(\"Retornou resposta do Meta-Agent\", function () {",
                    "        pm.expect(jsonData.response || jsonData.message).to.be.a('string');",
                    "    });",
                    "}"
                ],
                "type": "text/javascript"
            }
        }
    ]
}

def inject_scenarios(item_list):
    workflow_folder_found = False
    chat_folder_found = False

    for item in item_list:
        if 'item' in item:
            name = item.get('name', '')
            if name == "🔄 Engine de Workflows":
                # Add workflow request
                # Check if it already exists to avoid duplicates
                exists = any(r.get('name') == workflow_request['name'] for r in item['item'])
                if not exists:
                    item['item'].append(workflow_request)
                    print("Injected banner-production workflow request to 🔄 Engine de Workflows")
                    workflow_folder_found = True
            elif name == "💬 Chat e Conversação":
                # Add chat request
                exists = any(r.get('name') == chat_request['name'] for r in item['item'])
                if not exists:
                    item['item'].append(chat_request)
                    print("Injected banner-production chat request to 💬 Chat e Conversação")
                    chat_folder_found = True

            # Recursive check
            w_found, c_found = inject_scenarios(item['item'])
            workflow_folder_found = workflow_folder_found or w_found
            chat_folder_found = chat_folder_found or c_found

    return workflow_folder_found, chat_folder_found

w_ok, c_ok = inject_scenarios(collection.get('item', []))

if w_ok or c_ok:
    with open(file_path, 'w', encoding='utf-8') as f:
        json.dump(collection, f, indent=2, ensure_ascii=False)
    print("Postman collection updated successfully with E2E banner-production scenarios!")
else:
    print("Scenarios already present or folders not found.")
