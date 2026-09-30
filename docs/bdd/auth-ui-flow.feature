Funcionalidade: Fluxo de Autenticação (Interface)
  Como um usuário do sistema
  Quero me autenticar via Token JWT ou Chave de API
  Para acessar as funcionalidades protegidas da Single Page Application

  # ──────────────────────────────────────────────
  # INTERFACE DE LOGIN (MODAL)
  # ──────────────────────────────────────────────

  Cenário: Autenticação via Token JWT com sucesso
    Dado que o Modal de Login está visível
    E a aba "Token JWT" está selecionada
    Quando insiro um token válido começando com "eyJ"
    E clico em "Acessar Sistema"
    Então vejo um indicador de "Autenticando..."
    E o token é salvo no store de autenticação (localStorage)
    E o modal de login é fechado

  Cenário: Falha de autenticação por token inválido
    Dado que a aba "Token JWT" está selecionada
    Quando insiro o texto "token-curto"
    E clico em "Acessar Sistema"
    Então uma mensagem de erro "Token JWT em formato inválido." deve ser exibida
    E o botão de login deve ser reabilitado

  Cenário: Autenticação via Chave de API (X-Api-Key)
    Dado que mudo para a aba "Chave de API"
    Quando insiro uma chave válida
    E clico em "Acessar Sistema"
    Então o sistema chama a validação no backend
    E após o sucesso, o estado global do usuário é atualizado

  # ──────────────────────────────────────────────
  # PROTEÇÃO DE ROTAS (UX)
  # ──────────────────────────────────────────────

  Cenário: Redirecionamento de usuário não autenticado
    Dado que não estou autenticado (sem token válido)
    Quando tento acessar a URL "/agents" diretamente
    Então o sistema deve me redirecionar para a página inicial ou exibir o Modal de Login
    E a rota "/agents" não deve ser renderizada (ProtectedRoute)

  Cenário: Expiração de sessão (401 Unauthorized)
    Dado que minha sessão expirou enquanto eu navegava
    Quando qualquer chamada de API retorna status 401
    Então o sistema deve limpar os dados locais de autenticação
    E exibir o Modal de Login automaticamente informando que a sessão expirou

  Cenário: Persistência de sessão ao recarregar
    Dado que estou autenticado com um token válido
    Quando atualizo (F5) a página do navegador
    Então o sistema deve ler o token do localStorage
    E manter meu estado de autenticação como "Ativo"
    E não deve exibir o modal de login novamente
