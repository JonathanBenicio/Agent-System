import json, sys
sys.stdout.reconfigure(encoding='utf-8')

d = json.load(open('tests/postman/AgenticSystem.postman_collection.json', encoding='utf-8'))

print(f"=== JSON LOCAL ===")
print(f"Pastas: {len(d['item'])}")
print(f"Variáveis: {len(d['variable'])}")
print()

total_requests = 0
for folder in d['item']:
    requests = folder.get('item', [])
    total_requests += len(requests)
    name = folder['name']
    has_desc = bool(folder.get('description'))
    print(f"  {'✅' if has_desc else '❌'} {name} ({len(requests)} requests)")

print()
print(f"Total de requests: {total_requests}")
print()

# Folders no Postman (do getCollection)
postman_folders = [
    "⚙️ Configuração de Agentes",
    "📁 AgenticSystem.Api",
    "🤖 Gerenciamento de Agentes",
    "⚡ Execução e Runtime",
    "🧠 Habilidades de Agentes",
    "🛠️ Ferramentas de Agentes",
    "🚨 Alertas do Sistema",
    "🔒 Autenticação e Segurança",
    "💬 Chat e Conversação",
    "🔩 Configurações Globais",
    "📁 Conversations",
    "📄 Ingestão de Documentos (RAG)",
    "🔄 Migração de Embeddings",
    "📁 Entities",
    "🎛️ Gateway de API e Custo",
    "📚 Salas de Conhecimento",
    "🌐 Provedores de LLM",
    "🔑 Chaves dos Provedores LLM",
    "🔌 Plugins MCP",
    "📓 Integração Obsidian",
    "🧠 Modelos ONNX Locais",
    "📁 OpenAIChatCompletion",
    "🗺️ Planejador de Tarefas",
    "📁 ResponsesHttpHandler",
    "⏰ Tarefas Agendadas e Regras",
    "🗂️ Histórico e Sessões de Chat",
    "⚙️ Configurações do Administrador",
    "🚀 Setup Inicial",
    "🗣️ Integração de Voz",
    "🪝 Webhooks do Sistema",
    "🔄 Engine de Workflows",
]

local_names = [f['name'] for f in d['item']]

print("=== COMPARAÇÃO COM POSTMAN ===")
missing_in_local = [n for n in postman_folders if n not in local_names]
missing_in_postman = [n for n in local_names if n not in postman_folders]

if missing_in_local:
    print(f"\n❌ Pastas no Postman mas NÃO no JSON local ({len(missing_in_local)}):")
    for n in missing_in_local:
        print(f"  - {n}")
else:
    print("\n✅ Todas as pastas do Postman estão no JSON local!")

if missing_in_postman:
    print(f"\n⚠️ Pastas no JSON local mas NÃO no Postman ({len(missing_in_postman)}):")
    for n in missing_in_postman:
        print(f"  - {n}")
