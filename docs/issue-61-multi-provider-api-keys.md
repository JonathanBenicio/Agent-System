# [FEAT] Suporte a Múltiplas API Keys por Provedor com Descoberta Dinâmica e Roteamento Isolado (#61)

## 📝 Descrição
Atualmente, o sistema suporta apenas uma única chave de API global por provedor de IA (OpenAI, Gemini, Claude, OpenRouter, Ollama) armazenada de forma estática no banco chave-valor. Em cenários reais, os usuários precisam registrar múltiplas credenciais para o mesmo provedor (ex: uma chave de testes e outra de produção para o Gemini), com isolamento estrito de modelos por credencial, e a capacidade de selecionar explicitamente qual credencial deve ser utilizada na sessão de chat ou fluxo de trabalho.

Esta funcionalidade estende o runtime do Microsoft Agent Framework (MAF) para permitir o cadastro de N chaves por provedor por tenant, encriptando-as em repouso e exibindo apenas os 4 últimos caracteres na UI para diferenciação segura.

## 🎯 Objetivo / Valor de Negócio
- **Autonomia & Flexibilidade:** Permitir que o usuário utilize chaves de API diferentes para finalidades e orçamentos distintos dentro do mesmo tenant.
- **Isolamento Semântico:** Garantir que os modelos customizados ou finetunados de uma chave de API específica não sejam expostos a credenciais que não possuem acesso a eles.
- **Segurança da Informação:** Manter a proteção de chaves confidenciais por meio de encriptação AES em repouso, mascarando as credenciais no frontend e exibindo apenas o sufixo legível (`LastFour`).

## 🔗 Rastreabilidade & Documentação (Obrigatório)
- **ADR (Architectural Decision Record)**: [ADR-020: Multi-Provider API Key Architecture](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/docs/architecture/adr/020-multi-provider-api-keys.md)
- **User Story**: [US-42: Gerenciamento e Roteamento de Múltiplas API Keys](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/docs/user-stories/us-multi-provider-api-keys.md)
- **Implementation Plan**: [Roadmap: Multi-Provider API Keys](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/docs/plan/multi-provider-api-keys.md)
- **BDD Feature**: [BDD: Multi-Provider API Keys Feature](file:///c:/Users/Jonathan/Documents/Developer/GitHub/Agent-System/docs/bdd/multi-provider-api-keys.feature)

## ✅ Critérios de Aceite
- [ ] O usuário deve poder cadastrar múltiplas credenciais para os provedores Gemini, OpenAI, Claude e OpenRouter.
- [ ] Cada credencial cadastrada deve possuir um nome amigável (ex: "Chave Gemini Produção") e ter seu valor real de chave criptografado em repouso.
- [ ] O backend deve armazenar e expor o sufixo (últimos 4 caracteres) da chave em texto plano (`LastFour`) para fins de diferenciação visual na UI.
- [ ] O endpoint de descoberta de modelos (`DiscoverModels`) deve ser executável de forma isolada por chave de API cadastrada, associando os modelos descobertos unicamente a essa credencial.
- [ ] A listagem de modelos nas configurações de chat deve permitir a seleção explícita do par `[Modelo, Credencial/API Key]`.
- [ ] Se nenhuma credencial cadastrada for selecionada ou existir, o sistema deve realizar o fallback automático para a chave de API global/legada configurada nos arquivos de configuração ou ambiente (retrocompatibilidade).

## 🛠️ Requisitos Técnicos (Opcional)
- [ ] Criação da tabela/entidade `LLMProviderApiKeyEntity` herdando de `ITenantEntity`.
- [ ] Mapeamento e criação da migração do EF Core no subprojeto `AgenticSystem.Infrastructure`.
- [ ] Implementação de CRUD seguro com encriptação usando o `IConfigEncryptionService` existente.
- [ ] Atualização do `LLMManager.ResolveSelectionAsync` para suportar o roteamento de chave por ID (`LlmApiKeyId`).
- [ ] Testes de isolamento multi-tenant garantindo cobertura de 80% nos serviços criados.

## 🏁 Definition of Done (DoD)
- [ ] Código revisado de ponta a ponta.
- [ ] Testes de unidade e integração criados e passando.
- [ ] Sem vazamento de chaves reais nos endpoints REST ou logs de monitoramento.
- [ ] Interface visual do frontend atualizada de forma elegante com indicador de status, testes rápidos e sincronização.
