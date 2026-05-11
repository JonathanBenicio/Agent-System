---
description: Regras de Negócio e Arquitetura Global da Tabatine (Integração API Omie via Proxy Server-side)
trigger: model_decision
---

# Tabatine — Regras do Workspace

## Visão Geral do Projeto

Tabatine é uma plataforma de integração com o **Omie ERP** construída com **Next.js** (App Router). Ela consome a API REST/JSON do Omie via proxy server-side (API Routes do Next.js), protegendo as credenciais `APP_KEY` e `APP_SECRET` no `.env.local`.

---

## Arquitetura da Integração

```
Frontend (React + Zustand stores)
  → Next.js API Route (src/app/api/omie/*)
    → Omie API (https://app.omie.com.br/api/v1/*)
```

- **Credenciais**: `APP_KEY` e `APP_SECRET` ficam em `.env.local` e são injetados no corpo da requisição pelo API Route.
- **Proxy Pattern**: O frontend chama `/api/omie/<módulo>` com `{ call: "NomeDoMétodo", param: [...] }`. O route.ts injeta as credenciais e faz POST para a URL correspondente no Omie.
- **State Management**: Zustand stores em `src/store/` gerenciam o estado global.
- **Data Transformation (SSOT)**: Mapeadores centralizados em `src/lib/*-mapper.ts` transformam dados brutos (API/Supabase) para interfaces planas consumidas pela UI.

---

## API Omie — Referência Rápida

### Protocolo de Comunicação

Todas as APIs do Omie usam **JSON via HTTP POST**. Estrutura padrão de requisição:

```json
{
  "call": "NomeDoMetodo",
  "app_key": "...",
  "app_secret": "...",
  "param": [{ ...parâmetros... }]
}
```

Respostas de erro retornam `faultstring` e `faultcode`.

### Paginação

A maioria dos endpoints de listagem aceita:
```json
{
  "pagina": 1,
  "registros_por_pagina": 50
}
```
Respostas paginadas retornam: `pagina`, `total_de_paginas`, `registros`, `total_de_registros`.

### Limites

- Respeitar os limites de consumo da API (rate limiting).
- Documentação oficial: https://ajuda.omie.com.br/pt-BR/articles/8112984-limites-de-consumo-da-api-do-omie

---

## Endpoints Utilizados no Projeto

### 1. Pedidos de Venda (Vendas)
- **Endpoint**: `https://app.omie.com.br/api/v1/produtos/pedido/`
- **Route local**: `src/app/api/omie/vendas/route.ts`
- **Documentação**: https://app.omie.com.br/api/v1/produtos/pedido/
- **Método principal**: `ListarPedidos`
- **Exemplo de request**:
  ```json
  { "call": "ListarPedidos", "param": [{ "pagina": 1, "registros_por_pagina": 50 }] }
  ```
- **Campos importantes da resposta**: `cabecalho` (dados do pedido), `det[]` (itens/produtos), `frete`, `lista_parcelas`, `infoCadastro`

### 2. Pedidos de Venda — Faturamento
- **Endpoint**: `https://app.omie.com.br/api/v1/produtos/pedidovendafat/`
- **Documentação**: https://app.omie.com.br/api/v1/produtos/pedidovendafat/
- **Métodos disponíveis**:
  - `ObterPedidosVenda` — Lista pedidos a serem faturados (por etapa)
  - `FaturarPedidoVenda` — Fatura um pedido
  - `CancelarPedidoVenda` — Cancela um pedido
  - `ValidarPedidoVenda` — Valida pedido para faturamento
  - `DuplicarPedidoVenda` — Duplica um pedido
  - `AssociarCodIntPedidoVenda` — Associa código de integração

### 3. Notas Fiscais (NF)
- **Endpoint**: `https://app.omie.com.br/api/v1/produtos/nfconsultar/`
- **Route local**: `src/app/api/omie/nf/route.ts`
- **Documentação**: https://app.omie.com.br/api/v1/produtos/nfconsultar/
- **Métodos disponíveis**:
  - `ListarNF` — Lista notas fiscais
  - `ConsultarNF` — Consulta uma NF por `nCodNF` ou `nNF`
- **Exemplo de request**:
  ```json
  { "call": "ListarNF", "param": [{ "pagina": 1, "registros_por_pagina": 20, "ordenar_por": "CODIGO" }] }
  ```
- **Estrutura da resposta (nfCadastro)**: `ide` (identificação), `nfDestInt` (destinatário), `detArray` (itens), `total` (totais/impostos), `info`, `compl`, `titulosArray`, `pedido`

### 4. Clientes
- **Endpoint**: `https://app.omie.com.br/api/v1/geral/clientes/`
- **Route local**: `src/app/api/omie/clientes/route.ts`
- **Documentação**: https://app.omie.com.br/api/v1/geral/clientes/
- **Métodos principais**: `ListarClientes`, `ConsultarCliente`, `IncluirCliente`, `AlterarCliente`, `UpsertCliente`
- **Exemplo de request**:
  ```json
  { "call": "ListarClientes", "param": [{ "pagina": 1, "registros_por_pagina": 50, "clientesFiltro": {} }] }
  ```

### 5. Vendedores (Módulo Geral)
- **Endpoint**: `https://app.omie.com.br/api/v1/geral/vendedores/`
- **Documentação**: https://app.omie.com.br/api/v1/geral/vendedores/
- **Métodos disponíveis**:
  - `ListarVendedores` — Listagem paginada
  - `ConsultarVendedor` — Por `codigo` ou `codInt`
  - `IncluirVendedor`, `AlterarVendedor`, `ExcluirVendedor`, `UpsertVendedor`
- **Exemplo de request**:
  ```json
  { "call": "ListarVendedores", "param": [{ "pagina": 1, "registros_por_pagina": 100, "apenas_importado_api": "N" }] }
  ```
- **Campos do cadastro**: `codigo`, `codInt`, `nome`, `inativo`, `email`, `fatura_pedido`, `visualiza_pedido`, `comissao`

### 6. Usuários CRM
- **Endpoint**: `https://app.omie.com.br/api/v1/crm/usuarios/`
- **Documentação**: https://app.omie.com.br/api/v1/crm/usuarios/
- **Métodos disponíveis**:
  - `ListarUsuarios` — Lista paginada de usuários do CRM
  - `ObterUsuarios` — Obtém todos os usuários (`cExibirTodos: "S"/"N"`)

---

## Catálogo Completo de APIs Omie

Referência completa para quando for necessário integrar novos módulos.

### Cadastros Gerais
| API | Endpoint |
|-----|----------|
| Clientes/Fornecedores | `/geral/clientes/` |
| Clientes - Características | `/geral/clientescaract/` |
| Tags | `/geral/clientetag/` |
| Projetos | `/geral/projetos/` |
| Empresas | `/geral/empresas/` |
| Departamentos | `/geral/departamentos/` |
| Categorias | `/geral/categorias/` |
| Parcelas | `/geral/parcelas/` |
| Cidades | `/geral/cidades/` |
| Países | `/geral/paises/` |
| Vendedores | `/geral/vendedores/` |
| Produtos | `/geral/produtos/` |
| Contas Correntes | `/geral/contacorrente/` |

### CRM
| API | Endpoint |
|-----|----------|
| Contas | `/crm/contas/` |
| Contatos | `/crm/contatos/` |
| Oportunidades | `/crm/oportunidades/` |
| Tarefas | `/crm/tarefas/` |
| Usuários/Vendedores/Telemarketing/Pré-Vendas | `/crm/usuarios/` |
| Fases | `/crm/fases/` |
| Status | `/crm/status/` |

### Finanças
| API | Endpoint |
|-----|----------|
| CC Lançamentos | `/financas/contacorrentelancamentos/` |
| Contas a Pagar | `/financas/contapagar/` |
| Contas a Receber | `/financas/contareceber/` |
| Boletos | `/financas/contareceberboleto/` |
| PIX | `/financas/pix/` |
| Extrato | `/financas/extrato/` |
| Movimentos Financeiros | `/financas/mf/` |

### Vendas e Faturamento (Produtos)
| API | Endpoint |
|-----|----------|
| Pedidos de Venda (resumido) | `/produtos/pedidovenda/` |
| Pedidos de Venda (completo) | `/produtos/pedido/` |
| Pedidos - Faturamento | `/produtos/pedidovendafat/` |
| Pedidos - Etapas | `/produtos/pedidoetapas/` |
| Consultar NF | `/produtos/nfconsultar/` |
| NF Utilitários | `/produtos/notafiscalutil/` |
| Importar NF-e | `/produtos/nfe/` |
| Vendedores (vendas) | `/geral/vendedores/` |
| Formas Pagamento Vendas | `/produtos/formaspagvendas/` |
| Tabela de Preços | `/produtos/tabelaprecos/` |
| Etapas de Faturamento | `/produtos/etapafat/` |

### Compras e Estoque
| API | Endpoint |
|-----|----------|
| Pedidos de Compra | `/produtos/pedidocompra/` |
| Nota de Entrada | `/produtos/notaentrada/` |
| Consulta Estoque | `/estoque/consulta/` |
| Ajustes de Estoque | `/estoque/ajuste/` |
| Locais de Estoque | `/estoque/local/` |

### Serviços
| API | Endpoint |
|-----|----------|
| Serviços | `/servicos/servico/` |
| Ordens de Serviço | `/servicos/os/` |
| OS Faturamento | `/servicos/osp/` |
| Contratos | `/servicos/contrato/` |
| Consultas NFS-e | `/servicos/nfse/` |

---

## Links de Documentação

- **Portal do Desenvolvedor (lista de APIs)**: https://developer.omie.com.br/service-list/
- **Exemplos no GitHub**: https://github.com/omiexperience/api-examples
- **Central de Ajuda - APIs e Webhooks**: https://ajuda.omie.com.br/pt-BR/collections/3045828-apis-e-webhooks
- **Boas Práticas de Integração**: https://ajuda.omie.com.br/pt-BR/articles/12607801-boas-praticas-de-integracao-com-as-apis-do-omie
- **Limites de Consumo**: https://ajuda.omie.com.br/pt-BR/articles/8112984-limites-de-consumo-da-api-do-omie
- **Tratamento de Erros**: https://ajuda.omie.com.br/pt-BR/articles/8001888-tratando-os-erros-de-api
- **Webhooks**: https://ajuda.omie.com.br/pt-BR/articles/9565655-caracteristicas-e-recomendacoes-dos-webhooks

### Documentação dos Endpoints Específicos
- Pedidos de Venda: https://app.omie.com.br/api/v1/produtos/pedido/
- Pedidos - Faturamento: https://app.omie.com.br/api/v1/produtos/pedidovendafat/
- Consultar NF: https://app.omie.com.br/api/v1/produtos/nfconsultar/
- Clientes: https://app.omie.com.br/api/v1/geral/clientes/
- Vendedores: https://app.omie.com.br/api/v1/geral/vendedores/
- Usuários CRM: https://app.omie.com.br/api/v1/crm/usuarios/

---

## Padrões de Código para Novas Integrações

### Criando novo API Route

Ao criar um novo endpoint de proxy para o Omie, seguir este padrão (ver `src/app/api/omie/vendas/route.ts`):

```typescript
// src/app/api/omie/<modulo>/route.ts
import { NextResponse } from 'next/server';
import axios from 'axios';

const OMIE_API_URL = process.env.OMIE_API_URL || 'https://app.omie.com.br/api/v1/';
const OMIE_Endpoint = `${OMIE_API_URL}<caminho_do_endpoint>/`;
const APP_KEY = process.env.APP_KEY;
const APP_SECRET = process.env.APP_SECRET;

export async function POST(req: Request) {
  try {
    const body = await req.json();
    if (!APP_KEY || !APP_SECRET) {
      return NextResponse.json(
        { error: 'Missing Omie credentials in server environment' },
        { status: 500 }
      );
    }
    const omiePayload = { ...body, app_key: APP_KEY, app_secret: APP_SECRET };
    const response = await axios.post(OMIE_Endpoint, omiePayload, {
      headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
    });
    return NextResponse.json(response.data);
  } catch (error: any) {
    console.error('Error proxying Omie request:', error.response?.data || error.message);
    return NextResponse.json(
      { error: error.response?.data?.faultstring || 'Internal Server Error', details: error.message },
      { status: error.response?.status || 500 }
    );
  }
}
```

### Chamando a API pelo Frontend (Zustand Store)

```typescript
const response = await fetch('/api/omie/<modulo>', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    call: 'NomeDoMetodo',
    param: [{ pagina: 1, registros_por_pagina: 50 }],
  }),
});
const data = await response.json();
```

---

## Estrutura do Projeto

```
src/
├── app/
│   ├── api/omie/           # API Routes (proxy para Omie)
│   │   ├── vendas/route.ts  # → /api/v1/produtos/pedido/
│   │   ├── nf/route.ts      # → /api/v1/produtos/nfconsultar/
│   │   └── clientes/route.ts# → /api/v1/geral/clientes/
│   └── ...                  # Pages (App Router)
├── components/              # Componentes React (ex: VendasTable.tsx)
├── store/                   # Zustand stores (gerência de estado)
├── lib/                     # Mapeadores (SSOT) e utilitários
│   ├── vendas-mapper.ts
│   ├── nf-mapper.ts
│   └── ...
.doc/                        # Documentação interna do projeto
.env.local                   # APP_KEY, APP_SECRET, OMIE_API_URL
```

## Regras de Workflow e Arquitetura

- **Arquitetura de Mapeamento**: Veja `.agents/rules/arquitetura-mappers.md` para o padrão de transformação de dados.
- **Criação de Módulos**: Use o workflow `/create-mapped-module` (`.agents/workflows/create-mapped-module.md`) ao adicionar novas entidades.
- **Padrão de UI**: Seguir as diretrizes de `LayoutWrapper.tsx` e componentes de tabela baseados em `@tanstack/react-table`.