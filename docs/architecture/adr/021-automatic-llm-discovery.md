# ADR 021: Inspeção Automática de Modelos LLM no Login

**Status:** Aprovado  
**Data:** 22 de Maio de 2026  
**Autor(es):** Antigravity (Backend Specialist)

---

## Contexto

Atualmente, o sistema de gerenciamento de chaves multi-provedor (ADR 020) permite que usuários cadastrem chaves de API específicas (OpenAI, Gemini, Claude, OpenRouter) para uso da inteligência artificial. No entanto, para que os modelos disponíveis de cada provedor sejam mapeados e fiquem prontos para uso, a inspeção/descoberta de modelos (`DiscoverModelsAsync`) precisa ser executada manualmente via interface ou chamadas individuais de API.

Para melhorar a experiência do usuário e garantir que os catálogos de modelos disponíveis estejam sempre atualizados com os limites, suporte e novos lançamentos de cada provedor, o sistema deve disparar as inspeções de modelos LLM automaticamente quando o usuário realizar login com sucesso na plataforma.

As seguintes restrições e desafios de engenharia devem ser resolvidos:
1. **Lógica Assíncrona e Performance**: As chamadas a APIs de LLM externas têm latência alta (de 3 a 15 segundos). Não podemos bloquear a resposta HTTP de login.
2. **Ciclo de Vida de Serviços (DI)**: O serviço `ILLMProviderApiKeyService` (dependente do scoped `AgenticDbContext`) não está registrado no container de DI, gerando erros de resolução.
3. **Isolamento Multi-Tenant**: A plataforma exige isolamento rígido entre inquilinos. A busca e atualização de chaves de API devem obedecer estritamente ao `TenantId` do usuário logado, impedindo vazamentos ou acessos cruzados.
4. **Atualização Dinâmica do Frontend**: Como o catálogo é atualizado assincronamente em segundo plano, o frontend precisa ser notificado via SignalR assim que a varredura terminar para atualizar a interface em tempo real.

---

## Decisão

Adotamos a implementação de um gatilho de varredura automática de modelos LLM executado em segundo plano de forma assíncrona após login bem-sucedido no `AuthController`. 

As seguintes decisões técnicas de baixo nível serão tomadas:
1. **Registro do Serviço Faltante**: Registrar `ILLMProviderApiKeyService` como **Scoped** no container de DI em `ServiceCollectionExtensions.cs`.
2. **Execução em Segundo Plano com IServiceScopeFactory**: Injetar `IServiceScopeFactory` no `AuthController` para iniciar de forma segura uma tarefa em background (`Task.Run`) que cria um escopo independente, prevenindo erros de liberação de recursos (`ObjectDisposedException`) causados pelo encerramento prematuro da requisição HTTP de login.
3. **Isolamento por Tenant**: A varredura de chaves registradas no banco de dados usará a resolução do `TenantId` atual a partir do contexto de autenticação do usuário para filtrar estritamente a tabela `ProviderApiKeys`.
4. **Integração Real-time com SignalR**: Ao término da varredura, injetaremos o Hub SignalR correspondente para disparar uma notificação de atualização (ex: `LlmCatalogUpdated`) ao cliente do tenant logado, forçando a revalidação dinâmica no frontend.
5. **Inspeção de Chaves Globais**: O processo também inspecionará os provedores de infraestrutura global configurados ativamente no `AgenticSystemSettings` se estiverem habilitados.

---

## Justificativa

1. **[Experiência do Usuário UX]:** O usuário não precisa clicar manualmente para sincronizar modelos; o sistema atualiza o catálogo automaticamente assim que ele entra.
2. **[Confiabilidade e Desempenho]:** O login HTTP é imediato (não-bloqueante), mantendo a resposta rápida do endpoint de autenticação.
3. **[Segurança Rígida]:** Garantimos que a varredura assíncrona opere sob as fronteiras do tenant do usuário logado, protegendo chaves confidenciais.
4. **[Estabilidade do Framework]:** O uso de `IServiceScopeFactory` evita colisões de concorrência ou vazamento de escopo do DbContext em threads secundárias.

---

## Consequências

### Positivas
* Sincronização automatizada e transparente de modelos disponíveis logo após o acesso à aplicação.
* UI sempre atualizada com os modelos reais suportados pelas chaves cadastradas.
* Correção definitiva da injeção de dependência do `LLMProviderApiKeyService`.

### Desafios / Pontos de Atenção (Negativas)
* Aumento pontual do tráfego de requisições de saída para APIs de LLM externas logo após o login. As políticas de resiliência e circuit breakers implementados em `LLMManager` serão acionadas em caso de instabilidade para não sobrecarregar a inicialização do sistema.
