# Migração de Autenticação para Cookies HttpOnly

## Goal
Migrar o armazenamento e a transmissão da ApiKey do LocalStorage para cookies `httpOnly`, eliminando vulnerabilidades de XSS e estabilizando a conexão dos Hubs do SignalR.

## Overview
Atualmente, o frontend armazena a `agentic_api_key` no LocalStorage e tenta enviá-la via cabeçalho customizado `X-Api-Key` no SignalR, o que falha nativamente em conexões WebSocket nos navegadores. Migrar para cookies `httpOnly` garante o envio automático e seguro da credencial em requisições HTTP e WebSockets, impedindo o acesso via JavaScript do lado do cliente.

## Success Criteria
- [x] Login bem-sucedido via endpoint POST `/api/auth/login` retornando cookie `httpOnly`.
- [x] Requisições HTTP autenticadas automaticamente através do cookie.
- [x] Conexões de Hub do SignalR (`/hubs/chat` e `/hubs/gateway`) estabelecidas com sucesso sem desconexão.
- [x] Chave de API não exposta no LocalStorage.

## Tech Stack & Rationale
- **Backend (C# / ASP.NET Core)**: Emissão de cookies com atributos `HttpOnly`, `Secure`, `SameSite = SameSiteMode.Strict`.
- **Frontend (React / TypeScript)**: Requisições configuradas com `credentials: 'include'` para envio automático dos cookies.

## File Structure
```
├── src/AgenticSystem.Api/
│   ├── Controllers/AuthController.cs          # Novo controller para login/logout
│   └── Auth/ApiKeyAuthenticationHandler.cs    # Ajustado para ler chave do cookie
└── frontend/src/
    ├── lib/auth.ts                            # Ajustado para gerenciar autenticação via cookie
    ├── store/authStore.ts                     # Atualizado para refletir estado do login via API
    ├── lib/signalr.ts                         # Ajustado para omitir cabeçalhos manuais
    └── lib/signalr-gateway.ts                 # Ajustado para omitir cabeçalhos manuais
```

## Tasks
- [x] Task 1: Criar `AuthController.cs` no backend com endpoints `/api/auth/login` e `/api/auth/logout`. → Verify: Chamada POST retorna cabeçalho `Set-Cookie`.
- [x] Task 2: Atualizar `ApiKeyAuthenticationHandler.cs` para buscar a chave de API no cookie `agentic_api_key` caso não esteja no cabeçalho ou query string. → Verify: Endpoint protegido aceita requisição com cookie.
- [x] Task 3: Atualizar `frontend/src/lib/auth.ts` e `authStore.ts` para realizar requisições de login/logout via API e remover o LocalStorage. → Verify: Estado de autenticação refletido na UI.
- [x] Task 4: Atualizar `signalr.ts` e `signalr-gateway.ts` para que utilizem a transmissão automática de cookies. → Verify: Conexão do SignalR estabelecida e mantida.

## Rollback Strategy
Caso ocorra incompatibilidade de CORS ou SameSite no ambiente local:
- Reverter `lib/auth.ts` e `authStore.ts` para o estado anterior.
- Manter a leitura de query string no `ApiKeyAuthenticationHandler.cs`.

## Phase X: Verification
- [x] Executar build do backend e frontend (`dotnet build` e `tsc --noEmit`).
- [x] Testar fluxo de login completo na interface e verificar armazenamento nos cookies do navegador.
- [x] Validar ausência de erros e desconexões nos logs do Docker Compose.
