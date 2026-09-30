# US-42: Gerenciamento e Roteamento de Múltiplas API Keys por Provedor

**Épico:** Multi-Provider Credential & Model Management  
**Prioridade:** Alta  
**Estimativa (Story Points):** 8 SP  

---

## Descrição

**Como** administrador ou usuário avançado do Agentic System,  
**Eu quero** cadastrar, editar, excluir, testar e sincronizar modelos de múltiplas API Keys para o mesmo provedor de IA no meu tenant,  
**Para que** eu possa ter segregação de orçamentos, acesso a modelos específicos associados a cada credencial e escolher de forma explícita qual chave de API deve guiar minhas interações e agentes especialistas.

## Regras de Negócio e Contexto

1. **Multi-Key por Provedor:** O sistema deve aceitar um número ilimitado de chaves de API para os provedores Gemini, OpenAI, Claude e OpenRouter.
2. **Encriptação de Credenciais:** Nenhuma chave secreta em texto puro deve ser armazenada no banco ou enviada nas APIs de leitura. O valor real deve ser encriptado com criptografia simétrica AES-256 no backend.
3. **Exposição Visual Segura (`LastFour`):** Para diferenciar as chaves visualmente, a API deve retornar apenas os 4 últimos caracteres das chaves registradas (ex: `•••• 3d8t`). A API de cadastro receberá a chave completa, mas a API de leitura omitirá o valor completo de forma estrita.
4. **Isolamento de Modelos por Credencial:** A descoberta de modelos (`DiscoverModels`) é individual por credencial. Cada chave mantém sua própria lista de modelos em banco. O chat herda apenas os modelos da chave ativamente selecionada.
5. **Políticas de Fallback:**
   - Se um `LlmApiKeyId` específico for solicitado pelo chat ou sessão, usar essa credencial se ativa.
   - Se nenhum ID for passado, resolver pela chave marcada como `IsDefault = true` para o provedor/tenant.
   - Se nenhuma chave estiver registrada na tabela do banco, recorrer à chave de API legada configurada nas variáveis de ambiente globais (`appsettings.json` ou `ConfigEntryEntity`).

## Critérios de Aceite (DoD)

- [ ] **Critério 1: CRUD de Credenciais com Isolamento Multi-Tenant**  
  O usuário deve conseguir listar, cadastrar, desativar, marcar como padrão e deletar suas credenciais. A listagem deve filtrar estritamente pelo `TenantId` da sessão ativa.
- [ ] **Critério 2: Encriptação e Mascaramento Seguro**  
  Ao salvar uma chave, ela deve ser encriptada no banco e seu sufixo de 4 caracteres extraído. A API de listagem `GET /api/admin/llm/providers/{name}/keys` deve mascarar o valor da chave e expor apenas os últimos 4 caracteres.
- [ ] **Critério 3: Teste e Descoberta Isolados**  
  A interface deve fornecer botões rápidos "Testar Credencial" e "Sincronizar Modelos" para cada card de chave. A descoberta de modelos da chave X deve preencher apenas o catálogo de modelos associados à chave X.
- [ ] **Critério 4: Dropdown de Chat com Seleção de Credencial**  
  A tela de Chat/Dashboard do usuário deve exibir um dropdown combinando o modelo e a chave amigável de API associada (ex: `gemini-1.5-pro [Chave Dev]`, `gemini-1.5-pro [Chave Produção]`).
- [ ] **Critério 5: Resiliência e Fallback Retrocompatível**  
  Caso nenhuma credencial esteja cadastrada, a chamada de chat deve funcionar perfeitamente utilizando a chave legada configurada em arquivos de ambiente, sem lançar exceções.

## Dependências Técnicas

* [x] ADR-020 (Arquitetura de Múltiplas API Keys por Provedor LLM)
* [ ] Nova migração EF Core para a tabela `llm_provider_api_keys` aplicada no banco de dados.
* [ ] Criação dos endpoints em `LLMController.cs` e refatoração do `LLMManager.cs`.
