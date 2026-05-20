# ADR-020: Arquitetura de Múltiplas API Keys por Provedor LLM

**Status:** Aprovado  
**Data:** 20 de Maio de 2026  
**Autor(es):** Equipe de Arquitetura & Antigravity  

---

## Contexto

Atualmente, o Agentic System armazena as credenciais de provedores externos de LLM (como Google Gemini, OpenAI, Anthropic Claude, OpenRouter) como configurações globais de infraestrutura em formato de par chave-valor (`IConfigManager`). Esse design limita o sistema a uma única chave de API ativa por provedor por vez.

Em cenários reais de implantação empresarial e multi-tenant (SaaS):
1. **Necessidade de Múltiplas Contas/Orçamentos:** Um único cliente/tenant do sistema pode querer registrar diferentes chaves de API do mesmo provedor para fins e departamentos distintos (ex: "Gemini Marketing" com limites baixos vs "Gemini TI" com modelos avançados).
2. **Segregação de Modelos por Chave:** O catálogo de modelos acessíveis pode diferir drasticamente por chave de API (ex: acesso a modelos finetunados ou recursos beta). Mesclar tudo em um pool único degrada a segurança e gera falhas de execução quando o modelo é chamado com uma chave sem permissão.
3. **Diferenciação Visual:** O usuário precisa identificar de forma segura e fácil qual chave está ativa ou configurada na interface gráfica, sem que a chave secreta seja exposta em texto plano.

## Decisão

Adotaremos uma arquitetura robusta de **Multi-Key Management & Routing** estruturada nos seguintes pilares:

### 1. Modelo de Dados Unificado e Tenant-Isolated (`LLMProviderApiKeyEntity`)
Criaremos uma nova entidade persistida via EF Core com PostgreSQL:
* **Tenant Isolation:** A entidade implementará `ITenantEntity`, ativando automaticamente os filtros globais de consulta do `AgenticDbContext` para impedir qualquer vazamento de credenciais entre clientes.
* **Segurança Criptográfica:** O valor confidencial da chave de API (`EncryptedValue`) será encriptado de forma reversível em repouso usando o `IConfigEncryptionService` baseado em AES-256 já existente.
* **Mapeamento de Sufixo Seguro (`LastFour`):** Para permitir a diferenciação visual e seleção amigável pelo usuário, extrairemos e armazenaremos os últimos 4 caracteres da chave em texto plano na criação/atualização. O restante da chave é completamente omitido em listagens normais.
* **Isolamento de Modelos por Chave:** Adicionaremos o campo `Models` (JSON/CSV) para armazenar os modelos válidos descobertos unicamente por aquela chave de API durante a rotina de descoberta.

```sql
CREATE TABLE llm_provider_api_keys (
    id VARCHAR(64) PRIMARY KEY,
    tenant_id VARCHAR(64) NOT NULL,
    provider_name VARCHAR(64) NOT NULL,
    name VARCHAR(128) NOT NULL,
    encrypted_value TEXT NOT NULL,
    last_four VARCHAR(4) NOT NULL,
    is_enabled BOOLEAN NOT NULL DEFAULT TRUE,
    is_default BOOLEAN NOT NULL DEFAULT FALSE,
    models TEXT NOT NULL,
    created_at TIMESTAMP DEFAULT NOW(),
    updated_at TIMESTAMP DEFAULT NOW(),
    UNIQUE(tenant_id, provider_name, name)
);
```

### 2. Resolução Dinâmica e Roteamento Isolado no `LLMManager`
O `LLMRequest` e o `LLMRuntimeContext` serão expandidos para aceitar um `LlmApiKeyId` opcional.
* **Se `LlmApiKeyId` for fornecido:** O sistema carrega a credencial correspondente da tabela, descriptografa em memória e cria um provedor efêmero utilizando `CreateEphemeralProvider(providerName, decryptedKey, model)`.
* **Se nenhum ID for especificado:** O sistema tenta encontrar uma credencial cadastrada para o provedor que esteja marcada como padrão ativa (`IsDefault = true`).
* **Fallback Legado:** Se nenhuma credencial estiver registrada na tabela de chaves, o sistema fará o fallback automático para a chave de API global legada (`llm.providers.{provider}.apiKey`) configurada em arquivos de ambiente ou banco chave-valor global.

### 3. Sincronização Dinâmica Isolada por Chave
O endpoint `/api/admin/llm/providers/{name}/keys/{id}/discover-models` fará a chamada de descoberta utilizando especificamente a credencial informada. Os modelos retornados serão persistidos no campo `Models` da própria credencial `LLMProviderApiKeyEntity`, garantindo que o catálogo de cada chave seja isolado e não sofra poluição por chaves de terceiros.

### 4. Experiência de Usuário Premium com Sufixo Legível
* O frontend exibirá cartões organizados para cada credencial, exibindo seu nome amigável, indicador de status, tags de modelos específicos que ela possui acesso, e o sufixo no formato `•••• •••• •••• 4x9t`.
* A seleção do provedor na tela de chat permitirá que o usuário selecione a dupla `[Modelo, Credencial]` de forma explícita.

## Justificativa

1. **[Segurança]:** O uso de AES-256 e a exposição estrita dos 4 últimos caracteres garante que credenciais valiosas nunca vazem para o navegador do cliente ou logs de console.
2. **[Isolamento Multi-Tenant]:** Herdar de `ITenantEntity` vincula a governança de chaves ao mesmo modelo robusto de segurança de dados SaaS adotado nas tabelas de chat e documentos do sistema.
3. **[Robustez de Roteamento]:** Manter o fallback de chaves legadas assegura compatibilidade retroativa imediata (backward compatibility) para desenvolvedores locais rodando a aplicação sem cadastrar novas chaves na UI.

## Consequências

### Positivas
* Flexibilidade total para registrar múltiplas chaves do mesmo provedor (ex: 2 Gemini, 3 OpenRouter).
* Isolamento perfeito de modelos, impedindo tentativas de chamadas com modelos incompatíveis ou restritos.
* Alta visibilidade das credenciais configuradas na interface através do sufixo seguro.
* Conexão simplificada com sistemas de faturamento externos por chave.

### Desafios / Pontos de Atenção (Negativas)
* Aumento de complexidade no banco de dados com uma nova tabela e relacionamento implícito na resolução de chat.
* Necessidade de descriptografar chaves em tempo de execução para cada chamada (impacto de latência desprezível com AES-256 local em memória).

## Referências

- [GitHub Issue #61](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/docs/issue-61-multi-provider-api-keys.md)
- [US-42: Gerenciamento e Roteamento de Múltiplas API Keys](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/docs/user-stories/us-multi-provider-api-keys.md)
- [ADR-012: Multi-Tenant Agent Memory Schema](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/docs/architecture/adr/012-multi-tenant-agent-memory-schema.md)
- [ADR-014: Multi-LLM Provider Architecture](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/docs/architecture/adr/014-multi-llm-provider-architecture.md)
