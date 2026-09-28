import json
import os
import uuid

def generate_postman_collection():
    swagger_path = 'swagger.json'
    if not os.path.exists(swagger_path):
        print("Erro: swagger.json nao encontrado!")
        return

    with open(swagger_path, 'r', encoding='utf-8') as f:
        swagger = json.load(f)

    # 1. Mapeamento Completo de Tags para PT-BR com Ícones e Descrições detalhadas
    tag_metadata = {
        "AgentConfiguration": {
            "name": "⚙️ Configuração de Agentes",
            "description": "Gerencie a importação, exportação e validação de configurações de agentes estruturadas em arquivos YAML."
        },
        "AgentManagement": {
            "name": "🤖 Gerenciamento de Agentes",
            "description": "CRUD dinâmico e controle de agentes registrados no banco de dados do sistema."
        },
        "AgentRuntime": {
            "name": "⚡ Execução e Runtime",
            "description": "Endpoints de execução direta dos agentes, controle de execução em tempo real e estados operacionais."
        },
        "AgentSkills": {
            "name": "🧠 Habilidades de Agentes",
            "description": "Gerenciamento completo das habilidades (Skills) dinâmicas mapeadas no PostgreSQL com suporte a imports/uploads Markdown."
        },
        "AgentTools": {
            "name": "🛠️ Ferramentas de Agentes",
            "description": "Acesse, gerencie e modifique as ferramentas (Tools) disponíveis para os agentes utilizarem."
        },
        "Alerts": {
            "name": "🚨 Alertas do Sistema",
            "description": "Monitore alertas do sistema, custos e limites de infraestrutura."
        },
        "Auth": {
            "name": "🔒 Autenticação e Segurança",
            "description": "Autenticação de administrador, login de chaves de API globais e logout seguro."
        },
        "Chat": {
            "name": "💬 Chat e Conversação",
            "description": "Endpoints de chat síncrono e streaming de Server-Sent Events (SSE) com o Meta-Agent."
        },
        "Config": {
            "name": "🔩 Configurações Globais",
            "description": "Controle e atualização das variáveis de ambiente e configurações estruturadas do monorepo."
        },
        "Document": {
            "name": "📄 Ingestão de Documentos (RAG)",
            "description": "Faça upload e ingestão de documentos (Markdown, PDF, TXT) no pipeline RAG com vetorização automática."
        },
        "EmbeddingMigration": {
            "name": "🔄 Migração de Embeddings",
            "description": "Utilitários para migrar dados vetorizados entre diferentes modelos e provedores de embedding."
        },
        "Gateway": {
            "name": "🎛️ Gateway de API e Custo",
            "description": "Controle de limites diários, cotas por tenant e monitoramento de falhas do gateway."
        },
        "KnowledgeRoom": {
            "name": "📚 Salas de Conhecimento",
            "description": "Administre as salas de conhecimento e as permissões de acesso por usuário/tenant."
        },
        "LLM": {
            "name": "🌐 Provedores de LLM",
            "description": "Acesse o catálogo de modelos disponíveis e gerencie os provedores configurados."
        },
        "LLMProviderApiKey": {
            "name": "🔑 Chaves dos Provedores LLM",
            "description": "Configure chaves de API individuais e criptografadas para OpenAI, Gemini, Claude e OpenRouter."
        },
        "MCPPlugin": {
            "name": "🔌 Plugins MCP",
            "description": "Mapeie, adicione e atualize plugins do Model Context Protocol (MCP) externos."
        },
        "Obsidian": {
            "name": "📓 Integração Obsidian",
            "description": "Mapeamento e leitura de cofres (vaults) locais do Obsidian como memórias externas."
        },
        "OnnxModel": {
            "name": "🧠 Modelos ONNX Locais",
            "description": "Gerenciamento e execução de inferências com modelos locais .onnx na CPU (FastPath e Reranking)."
        },
        "Planner": {
            "name": "🗺️ Planejador de Tarefas",
            "description": "Consulte planos táticos estruturados gerados de forma cognitiva pelo planejador de tarefas."
        },
        "RagTest": {
            "name": "🔬 Testes de RAG",
            "description": "Endpoints de teste e avaliação de precisão das buscas vetoriais."
        },
        "ScheduledTasks": {
            "name": "⏰ Tarefas Agendadas e Regras",
            "description": "Crie regras cronógicas de automação e configure canais de disparo."
        },
        "Session": {
            "name": "🗂️ Histórico e Sessões de Chat",
            "description": "Acesse o histórico de mensagens, sessões ativas e consolidações de memória."
        },
        "Settings": {
            "name": "⚙️ Configurações do Administrador",
            "description": "Configuração refinada de Reranking neural, limites de pool e backups."
        },
        "Setup": {
            "name": "🚀 Setup Inicial",
            "description": "Assistente de bootstrap inicial do banco de dados e parametrização básica."
        },
        "Voice": {
            "name": "🗣️ Integração de Voz",
            "description": "Endpoints para conversão de áudio para texto e respostas assistidas por voz."
        },
        "Webhooks": {
            "name": "🪝 Webhooks do Sistema",
            "description": "Mapeamento e recebimento de payloads em tempo real."
        },
        "Workflow": {
            "name": "🔄 Engine de Workflows",
            "description": "Criação, execução e cancelamento de Workflows complexos em lote e paralelos."
        },
        "AgenticSystem.Api": {
            "name": "🔧 Endpoints do Sistema (API)",
            "description": "Endpoints base do sistema para checagens de status, health check e versão da API."
        },
        "Conversations": {
            "name": "💬 Compatibilidade OpenAI - Conversas",
            "description": "Endpoints para gerenciamento de histórico de conversas compatíveis com o formato oficial do OpenAI."
        },
        "Entities": {
            "name": "🗄️ Entidades e Persistência",
            "description": "Acesso e consultas estruturadas de persistência de dados das tabelas de configuração do banco."
        },
        "OpenAIChatCompletion": {
            "name": "🤖 Completude OpenAI v1",
            "description": "Serviço de completude de chat compatível com o formato OpenAI (/v1/chat/completions) para integrar ferramentas de terceiros."
        },
        "ResponsesHttpHandler": {
            "name": "📥 Handlers HTTP DevUI",
            "description": "Controladores de respostas mockadas do OpenAI e utilitários de suporte à interface DevUI."
        }
    }

    # 2. Estrutura da Collection do Postman (v2.1.0)
    collection = {
        "info": {
            "name": "🚀 Agentic System - Suite Completa de Testes E2E",
            "description": "Collection traduzida e estruturada com dados realistas para automação completa do backend do Agentic System.",
            "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
        },
        "item": [],
        "variable": [
            { "key": "base_url", "value": "http://localhost:8080", "type": "string" },
            { "key": "api_key", "value": "minha-chave-secreta-admin-123", "type": "string" },
            { "key": "tenant_id", "value": "admin", "type": "string" },
            { "key": "session_id", "value": "session-postman-e2e-test-123", "type": "string" },
            { "key": "room_id", "value": "", "type": "string" },
            { "key": "workflow_id", "value": "", "type": "string" },
            { "key": "execution_id", "value": "", "type": "string" },
            { "key": "skill_id", "value": "skill-postman-e2e", "type": "string" }
        ]
    }

    folders = {}

    def get_or_create_folder(tag_name):
        meta = tag_metadata.get(tag_name, {"name": f"📁 {tag_name}", "description": ""})
        pt_name = meta["name"]
        
        if pt_name not in folders:
            folder = {
                "id": str(uuid.uuid4()),
                "name": pt_name,
                "description": meta["description"],
                "item": []
            }
            folders[pt_name] = folder
            collection["item"].append(folder)
        return folders[pt_name]

    # Dicionario de corpos realistas mapeados por rota + metodo
    rich_bodies = {
        "/api/auth/login": { "apiKey": "{{api_key}}" },
        "/api/chat": {
            "message": "Olá assistente, me liste quais ferramentas e habilidades você possui disponíveis.",
            "sessionId": "{{session_id}}",
            "targetAgent": "FastPath"
        },
        "/api/chat/stream": {
            "message": "Explique de forma detalhada o que é a arquitetura MAF e como ela isola os agentes.",
            "sessionId": "{{session_id}}"
        },
        "/api/knowledge/rooms": {
            "id": "postman-support-room",
            "name": "Sala de Suporte E2E",
            "description": "Sala gerada automaticamente por teste do Postman",
            "isEnabled": True,
            "tenantId": "{{tenant_id}}"
        },
        "/api/knowledge/rooms/{id}/permissions": {
            "userId": "user-teste-postman",
            "role": 1 # Reader
        },
        "/api/workflow/definitions": {
            "id": "e2e-workflow-test",
            "name": "E2E Test Workflow",
            "description": "Workflow de teste de integridade gerado pelo Postman",
            "version": 1,
            "steps": [
                {
                    "id": "step-1",
                    "name": "Test Action Step",
                    "stepType": "Action",
                    "agentName": "FastPath",
                    "actionDescription": "Passo inicial de teste usando o agente FastPath",
                    "input": { "prompt": "Olá do Postman E2E!" }
                }
            ],
            "variables": { "systemEnvironment": "Development" },
            "triggerType": "Manual"
        },
        "/api/agent/skills": {
            "id": "custom-calculator-postman",
            "name": "Postman Dynamic Calculator",
            "domain": "mathematics",
            "type": "Instruction",
            "systemPromptFragment": "# Instruções\nExecute cálculos matemáticos e retorne de forma estruturada.",
            "fewShotExamples": "User: 5 + 5\nAgent: 10",
            "metadata": { "author": "Postman E2E Script" }
        },
        "/api/agent/agents/validate-yaml": {
            "yaml": "id: fastpath\nname: FastPath\ndescription: Fast response agent\nskills:\n  - clean-code\ntier: 1"
        },
        "/api/agent/agents/save-yaml": {
            "yaml": "id: fastpath\nname: FastPath\ndescription: Fast response agent\nskills:\n  - clean-code\ntier: 1"
        },
        "/api/agent/runtime/execute": {
            "agentId": "fastpath",
            "prompt": "Olá! Imprima 'Hello World'!",
            "sessionId": "{{session_id}}"
        },
        "/api/admin/scheduled-tasks/rules": {
            "id": "rule-e2e-postman",
            "name": "Regra Agendada E2E",
            "description": "Regra de teste cron",
            "cronExpression": "*/5 * * * *",
            "agentId": "fastpath",
            "prompt": "Verificar status",
            "isEnabled": True
        },
        "/api/admin/llm/providers/OpenAI/keys": {
            "name": "Chave OpenAI Teste",
            "encryptedValue": "sk-proj-testkey12345...",
            "isEnabled": True
        },
        "/api/setup/start": {
            "adminEmail": "admin@agenticsystem.com",
            "adminPassword": "SenhaSegura123!",
            "systemName": "Agentic System E2E"
        },
        "/api/voice/ask": {
            "voiceInput": "Olá assistente, fale comigo.",
            "voiceLocale": "pt-BR"
        },
        "/api/admin/plugins": {
            "id": "mcp-postman-test",
            "name": "MCP Plugin Postman",
            "description": "Plugin de teste",
            "url": "http://localhost:5009/mcp",
            "isEnabled": True
        },
        "/api/admin/embedding-migration/start": {
            "sourceProvider": "Ollama",
            "targetProvider": "OpenAI",
            "sourceModel": "nomic-embed-text",
            "targetModel": "text-embedding-3-small"
        },
        "/api/admin/settings/gateway": {
            "defaultDailyBudget": 50.0,
            "defaultFailureThreshold": 5,
            "defaultBreakDurationSeconds": 30,
            "defaultRequestsPerMinute": 30
        },
        "/api/admin/settings/memory": {
            "obsidianVaultPath": "wwwroot/vault",
            "vectorStoreType": "PostgreSQL",
            "connectionString": "Host=localhost;Database=agentic_dev"
        },
        "/api/admin/settings/reranking": {
            "enabled": True,
            "useDedicatedProvider": True,
            "dedicatedProvider": "LocalOnnxCrossEncoder"
        },
        "/v1/chat/completions": {
            "model": "gpt-4o-mini",
            "messages": [
                { "role": "user", "content": "Olá do Postman v1 API!" }
            ],
            "temperature": 0.7
        },
        "/AgenticSystem/v1/responses": {
            "input": "Olá assistente, me descreva o Agentic System.",
            "agent": {
                "id": "fastpath",
                "name": "FastPath"
            },
            "model": "gpt-4o-mini",
            "instructions": "Você é um assistente útil e direto.",
            "max_output_tokens": 100,
            "store": True,
            "stream": False,
            "temperature": 0.7
        },
        "/api/admin/llm/default-selection": {
            "providerName": "Gemini",
            "model": "gemini-3-pro-preview"
        }
    }

    # 3. Mapeia todos os caminhos do swagger.json
    paths = swagger.get("paths", {})
    for path, methods in paths.items():
        for method, details in methods.items():
            tag = details.get("tags", ["General"])[0]
            folder = get_or_create_folder(tag)

            url_params = []
            query_params = []
            parameters = details.get("parameters", [])
            for p in parameters:
                if p.get("in") == "query":
                    query_params.append({
                        "key": p.get("name"),
                        "value": "",
                        "description": p.get("description", ""),
                        "disabled": not p.get("required", False)
                    })

            # Converte os parametros de path no Postman
            postman_path = [segment for segment in path.strip('/').split('/')]
            postman_path = [f":{segment[1:-1]}" if segment.startswith('{') and segment.endswith('}') else segment for segment in postman_path]

            normalized_path_key = path.replace('{', '{').replace('}', '}')

            request_item = {
                "id": str(uuid.uuid4()),
                "name": f"[{method.upper()}] {details.get('summary', path)}",
                "request": {
                    "method": method.upper(),
                    "header": [
                        { "key": "X-Api-Key", "value": "{{api_key}}", "type": "text" },
                        { "key": "X-Tenant-Id", "value": "{{tenant_id}}", "type": "text" }
                    ],
                    "url": {
                        "raw": "{{base_url}}" + path,
                        "host": ["{{base_url}}"],
                        "path": postman_path
                    }
                }
            }

            if query_params:
                request_item["request"]["url"]["query"] = query_params

            # Vincula corpo realista
            matched_body = rich_bodies.get(normalized_path_key)
            
            if matched_body and method.lower() in ["post", "put"]:
                request_item["request"]["body"] = {
                    "mode": "raw",
                    "raw": json.dumps(matched_body, indent=2, ensure_ascii=False),
                    "options": {
                        "raw": { "language": "json" }
                    }
                }
            elif "requestBody" in details and method.lower() in ["post", "put"]:
                content = details["requestBody"].get("content", {})
                json_content = content.get("application/json", {}) or content.get("text/json", {}) or content.get("application/*+json", {})
                if json_content:
                    schema_ref = json_content.get("schema", {}).get("$ref", "")
                    body_json = {}
                    if schema_ref:
                        schema_name = schema_ref.split('/')[-1]
                        schema_def = swagger.get("components", {}).get("schemas", {}).get(schema_name, {})
                        for prop, prop_def in schema_def.get("properties", {}).items():
                            body_json[prop] = prop_def.get("type", "string")

                    request_item["request"]["body"] = {
                        "mode": "raw",
                        "raw": json.dumps(body_json, indent=2, ensure_ascii=False),
                        "options": {
                            "raw": { "language": "json" }
                        }
                    }

            # Configura os scripts pós-execução (pm.test) ricos para execução pronta!
            test_scripts = [
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
                "// 3. Validação de formato da resposta (JSON) se houver corpo",
                "if (pm.response.text().length > 0 && pm.response.headers.get('Content-Type') && pm.response.headers.get('Content-Type').includes('application/json')) {",
                "    pm.test(\"Estrutura da resposta e um JSON valido\", function () {",
                "        pm.expect(pm.response.json()).to.be.an('object');",
                "    });",
                "}"
            ]

            # Injeções de variáveis E2E e asserções customizadas
            if path == "/api/auth/login" and method == "post":
                test_scripts.extend([
                    "",
                    "// 4. Captura dinâmica do Tenant ID",
                    "const loginData = pm.response.json();",
                    "pm.test(\"Login efetuado com sucesso\", function () {",
                    "    pm.expect(loginData.success).to.be.true;",
                    "    pm.expect(loginData.tenantId).to.exist;",
                    "});",
                    "if (loginData.tenantId) {",
                    "    pm.environment.set(\"tenant_id\", loginData.tenantId);",
                    "}"
                ])

            if path == "/api/knowledge/rooms" and method == "post":
                test_scripts.extend([
                    "",
                    "// 4. Armazena o Room ID gerado",
                    "const roomData = pm.response.json();",
                    "pm.test(\"Sala de conhecimento criada\", function () {",
                    "    pm.expect(roomData.id).to.exist;",
                    "});",
                    "if (roomData.id) {",
                    "    pm.environment.set(\"room_id\", roomData.id);",
                    "}"
                ])

            if path == "/api/workflow/definitions" and method == "post":
                test_scripts.extend([
                    "",
                    "// 4. Armazena o Workflow Definition ID",
                    "const workflowData = pm.response.json();",
                    "pm.test(\"Definicao de Workflow criada\", function () {",
                    "    pm.expect(workflowData.id).to.exist;",
                    "});",
                    "if (workflowData.id) {",
                    "    pm.environment.set(\"workflow_id\", workflowData.id);",
                    "}"
                ])

            if path.startswith("/api/workflow/executions/start/") and method == "post":
                test_scripts.extend([
                    "",
                    "// 4. Armazena o Execution ID",
                    "const executionData = pm.response.json();",
                    "pm.test(\"Execucao de Workflow iniciada\", function () {",
                    "    pm.expect(executionData.id).to.exist;",
                    "});",
                    "if (executionData.id) {",
                    "    pm.environment.set(\"execution_id\", executionData.id);",
                    "}"
                ])

            if path == "/api/agent/skills" and method == "post":
                test_scripts.extend([
                    "",
                    "// 4. Armazena o Skill ID",
                    "const skillData = pm.response.json();",
                    "pm.test(\"Skill criada e persistida no PostgreSQL\", function () {",
                    "    pm.expect(skillData.id).to.exist;",
                    "});",
                    "if (skillData.id) {",
                    "    pm.environment.set(\"skill_id\", skillData.id);",
                    "}"
                ])

            request_item["event"] = [
                {
                    "listen": "test",
                    "script": {
                        "exec": test_scripts,
                        "type": "text/javascript"
                    }
                }
            ]

            # Asserções de variáveis de path para rotas de detalhe
            if ":id" in postman_path:
                request_item["request"]["url"]["variable"] = [
                    { "key": "id", "value": "{{room_id}}", "description": "ID dinâmico da entidade gerado pelo E2E" }
                ]
            if ":definitionId" in postman_path:
                request_item["request"]["url"]["variable"] = [
                    { "key": "definitionId", "value": "{{workflow_id}}", "description": "ID dinâmico da definicao de workflow" }
                ]

            folder["item"].append(request_item)

    # 3. Adiciona rotas manuais ignoradas de upload devidamente configuradas com testes prontos
    onnx_folder = get_or_create_folder("OnnxModel")
    
    onnx_folder["item"].append({
        "id": str(uuid.uuid4()),
        "name": "[POST] Ingerir / Upload ONNX Model (multipart/form-data)",
        "request": {
            "method": "POST",
            "header": [
                { "key": "X-Api-Key", "value": "{{api_key}}", "type": "text" },
                { "key": "X-Tenant-Id", "value": "{{tenant_id}}", "type": "text" }
            ],
            "body": {
                "mode": "formdata",
                "formdata": [
                    { "key": "file", "description": "Arquivo do modelo .onnx", "type": "file", "src": "" },
                    { "key": "name", "value": "meu_modelo_teste", "type": "text" },
                    { "key": "inputNodeName", "value": "input_0", "type": "text" },
                    { "key": "outputNodeName", "value": "output_0", "type": "text" },
                    { "key": "description", "value": "Modelo testado via Postman", "type": "text" }
                ]
            },
            "url": {
                "raw": "{{base_url}}/api/onnx/models",
                "host": ["{{base_url}}"],
                "path": ["api", "onnx", "models"]
            }
        },
        "event": [
            {
                "listen": "test",
                "script": {
                    "exec": [
                        "pm.test(\"Status code aceitavel\", function () {",
                        "    pm.expect(pm.response.code).to.be.oneOf([200, 201]);",
                        "});"
                    ],
                    "type": "text/javascript"
                }
            }
        ]
    })

    # 4. Salva a collection consolidada
    target_dir = 'tests/postman'
    os.makedirs(target_dir, exist_ok=True)
    target_path = os.path.join(target_dir, 'AgenticSystem.postman_collection.json')

    with open(target_path, 'w', encoding='utf-8') as f:
        json.dump(collection, f, indent=2, ensure_ascii=False)

    print(f"Sucesso! Postman Collection com corpos realistas, icones, PT-BR e testes unitarios pronta em: {target_path}")

if __name__ == '__main__':
    generate_postman_collection()
