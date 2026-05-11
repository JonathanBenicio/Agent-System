---
description: Como adicionar um novo Proxy para a API Omie
---

Este workflow descreve os passos para criar uma nova rota de API que atua como proxy para um endpoint do Omie ERP.

### 1. Identificar o Endpoint no Omie
Consulte a documentação oficial ou o arquivo `.agents/rules/geral.md` para encontrar a URL do endpoint desejado (ex: `/geral/produtos/`).

### 2. Criar a Rota no Next.js
Crie um arquivo em `src/app/api/omie/<modulo>/route.ts`.

Exemplo de conteúdo:
```typescript
import { NextResponse } from 'next/server';
import axios from 'axios';

const OMIE_API_URL = process.env.OMIE_API_URL || 'https://app.omie.com.br/api/v1/';
const OMIE_Endpoint = `${OMIE_API_URL}<modulo>/<submodulo>/`;
const APP_KEY = process.env.APP_KEY;
const APP_SECRET = process.env.APP_SECRET;

// Cache Simples
const cache = new Map<string, { timestamp: number; data: any }>();
const CACHE_TTL = 2 * 60 * 1000; // 2 minutos

export async function POST(req: Request) {
  try {
    const body = await req.json();
    if (!APP_KEY || !APP_SECRET) {
      return NextResponse.json({ error: 'Credenciais Omie ausentes' }, { status: 500 });
    }

    // Verificar Cache
    const cacheKey = JSON.stringify(body);
    const cached = cache.get(cacheKey);
    if (cached && Date.now() - cached.timestamp < CACHE_TTL) {
      return NextResponse.json(cached.data);
    }

    const response = await axios.post(OMIE_Endpoint, {
      ...body,
      app_key: APP_KEY,
      app_secret: APP_SECRET,
    });

    cache.set(cacheKey, { timestamp: Date.now(), data: response.data });
    return NextResponse.json(response.data);
  } catch (error: any) {
    return NextResponse.json(
      { error: error.response?.data?.faultstring || 'Erro Interno' },
      { status: error.response?.status || 500 }
    );
  }
}
```

### 3. (Opcional) Criar Hook de Dados
Crie um hook em `src/hooks/use<Modulo>Query.ts` usando TanStack Query para consumir este novo proxy.

### 4. Validar
Teste a chamada usando Postman ou diretamente no frontend do projeto.
