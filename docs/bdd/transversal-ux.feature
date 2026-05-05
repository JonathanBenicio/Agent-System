# language: pt
Funcionalidade: Transversal e UX
  Como usuário do AgenticSystem
  Quero componentes transversais de navegação, feedback e temas
  Para ter uma experiência consistente e acessível em toda a aplicação

  Contexto:
    Dado que estou autenticado
    E a aplicação está carregada

  # ──────────────────────────────────────────────
  # US-26 — Sidebar com navegação e seções
  # ──────────────────────────────────────────────

  Cenário: Sidebar exibe todas as seções
    Dado que estou em qualquer página da aplicação
    Então a sidebar exibe as seções:
      | Seção       | Ícone |
      | Chat        | 💬    |
      | Dashboard   | 📊    |
      | Agents      | 🤖    |
      | Providers   | ⚡    |
      | Plugins     | 🔌    |
      | Settings    | ⚙️    |

  Cenário: Destaque da seção ativa
    Dado que estou na página "/agents"
    Então o item "Agents" na sidebar está destacado (ativo)
    E os demais itens estão em estado normal

  Cenário: Navegação via sidebar
    Dado que estou na página "/chat"
    Quando clico em "Dashboard" na sidebar
    Então sou redirecionado para "/dashboard"
    E a seção "Dashboard" fica ativa na sidebar

  Cenário: Sidebar responsiva (mobile)
    Dado que estou em um dispositivo com tela menor que 768px
    Quando acesso a aplicação
    Então a sidebar é recolhida por padrão
    E um botão hamburger permite expandir/recolher

  # ──────────────────────────────────────────────
  # US-27 — Toast de feedback
  # ──────────────────────────────────────────────

  Cenário: Toast de sucesso após ação
    Dado que estou em qualquer página
    Quando uma operação é concluída com sucesso
    Então um toast com variante "success" é exibido no canto superior direito
    E o toast desaparece automaticamente após 5 segundos

  Cenário: Toast de erro com detalhes
    Dado que estou em qualquer página
    Quando uma operação falha
    Então um toast com variante "error" é exibido
    E a mensagem inclui detalhes do erro

  Cenário: Toast empilhável
    Dado que estou em qualquer página
    Quando múltiplas notificações ocorrem em sequência
    Então os toasts são empilhados verticalmente
    E cada um pode ser fechado individualmente

  # ──────────────────────────────────────────────
  # US-28 — Modal de confirmação (ConfirmModal)
  # ──────────────────────────────────────────────

  Cenário: Modal de confirmação com variante danger
    Quando uma ação destrutiva é solicitada (ex: excluir agente)
    Então um modal aparece com variante "danger"
    E contém título, mensagem descritiva e botões "Confirmar"/"Cancelar"
    E o botão "Confirmar" tem estilo destrutivo (vermelho)

  Cenário: Modal de confirmação com variante warning
    Quando uma ação de risco moderado é solicitada
    Então um modal aparece com variante "warning"
    E o botão "Confirmar" tem estilo de alerta (amarelo)

  Cenário: Fechar modal com tecla Escape
    Dado que um modal de confirmação está aberto
    Quando pressiono a tecla Escape
    Então o modal é fechado
    E a ação NÃO é executada

  Cenário: Fechar modal clicando fora (backdrop)
    Dado que um modal de confirmação está aberto
    Quando clico fora do modal (no backdrop)
    Então o modal é fechado

  # ──────────────────────────────────────────────
  # US-29 — Loading state e Empty state
  # ──────────────────────────────────────────────

  Cenário: Loading state global
    Dado que uma chamada de API está em andamento
    Quando a resposta ainda não chegou
    Então um skeleton ou spinner é exibido na área de conteúdo
    E os controles de interação ficam desabilitados

  Cenário: Empty state informativo
    Dado que acesso a lista de agentes
    Quando não há agentes cadastrados
    Então uma mensagem "Nenhum agente encontrado" é exibida
    E um ícone ilustrativo e botão de ação são mostrados

  Cenário: Error state com retry
    Dado que uma chamada de API falhou
    Quando a tela de erro é exibida
    Então a mensagem de erro e o código HTTP são mostrados
    E um botão "Tentar novamente" permite recarregar

  # ──────────────────────────────────────────────
  # US-30 — Dark theme com TailwindCSS v4
  # ──────────────────────────────────────────────

  Cenário: Dark theme é o padrão
    Dado que acesso a aplicação pela primeira vez
    Então o tema escuro (dark) é o padrão
    E o background segue o scheme de cores do TailwindCSS v4

  Cenário: Alternância manual de tema
    Dado que o tema escuro está ativo
    Quando clico no toggle de tema na sidebar ou header
    Então o tema muda para claro (light)
    E a preferência é salva no localStorage

  Cenário: Persistência do tema selecionado
    Dado que selecionei o tema claro
    Quando fecho e reabro a aplicação
    Então o tema claro é restaurado do localStorage

  Cenário: Respeitar preferência do sistema operacional
    Dado que NÃO tenho preferência salva no localStorage
    E meu sistema operacional usa tema escuro
    Quando acesso a aplicação
    Então o tema escuro é aplicado seguindo prefers-color-scheme
