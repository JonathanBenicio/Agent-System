# Plano de Implementação - Sugestões de Capacidades em PT-BR com Dicas Visuais

Este plano descreve a melhoria na interface de criação de agentes para incluir sugestões de "Capacidades" (Capabilities) em Português, facilitando a configuração do DNA do agente com explicações contextuais.

## Objetivo
Melhorar a experiência do usuário (UX) ao configurar as capacidades operacionais de um agente, substituindo o campo de texto livre por uma interface baseada em sugestões clicáveis e categorizadas, incluindo tooltips que explicam o impacto técnico de cada escolha no orquestrador.

## Arquivos Afetados
- `frontend/src/components/agents/AgentFormModal.tsx`

## Mudanças Propostas

### 1. Definição do Dicionário de Capacidades
Adicionar uma constante `CAPABILITY_CATALOG` no arquivo `AgentFormModal.tsx` mapeando cada tag para sua categoria e descrição técnica:

- **Inteligência**
  - `planejamento`: "Permite que o orquestrador solicite a criação de planos de ação estruturados."
  - `raciocínio-lógico`: "Sinaliza alta capacidade analítica para resolução de problemas complexos."
  - `análise-de-dados`: "Otimiza o processamento de tabelas, CSVs e estruturas JSON."
  - `sumarização`: "Especialidade em condensar grandes volumes de texto preservando pontos chave."

- **Técnico**
  - `execução-de-código`: "Autoriza o uso de ferramentas de sandbox para rodar scripts (Python/C#)."
  - `integração-api`: "Foco em chamadas REST/GraphQL e orquestração de serviços externos."
  - `gestão-de-arquivos`: "Permite leitura, escrita e organização de sistemas de arquivos."
  - `busca-web`: "Habilita a navegação ativa para extração de dados atualizados da internet."

- **Conhecimento**
  - `acesso-rag`: "Especialista em busca semântica na base de conhecimento vetorial."
  - `memória-longo-prazo`: "Permite ao agente persistir e recuperar fatos entre diferentes sessões."
  - `multimodal`: "Capacidade de interpretar imagens, áudio ou documentos complexos."
  - `extração-entidades`: "Foco em identificar e isolar dados específicos em textos não estruturados."

### 2. Alteração na UI (Aba Visual)
- Renderizar as categorias de `CAPABILITY_CATALOG`.
- Exibir cada tag como um badge clicável.
- **Dica Visual (Tooltip)**: Adicionar o atributo `title` (ou um componente de tooltip customizado) em cada badge com a descrição técnica correspondente.
- Estilizar as sugestões para que mudem de cor quando já selecionadas no formulário.

### 3. Lógica de Interação
- Implementar função `toggleCapability(tag)` para gerenciar a adição/remoção.
- Garantir que tags manuais continuem funcionando em paralelo às sugestões.

## Plano de Verificação e Testes

### Verificação de UX
- [x] Validar se as tooltips aparecem corretamente ao passar o mouse sobre cada sugestão.
- [x] Confirmar se as descrições estão claras e em português.

### Verificação Funcional
- [x] Testar a alternância (toggle) de cada tag sugerida.
- [x] Verificar se as tags selecionadas são persistidas corretamente ao salvar o agente.
- [x] Validar a integração com o YAML: tags selecionadas visualmente devem aparecer no YAML e vice-versa.

