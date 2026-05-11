---
description: Como criar uma nova página de Dashboard ou Detalhes
---

Este workflow descreve os passos para criar uma nova tela seguindo o padrão visual do projeto Tabatine.

### 1. Estrutura de Pastas
Crie a pasta da rota em `src/app/` (ex: `src/app/financeiro/page.tsx`).

### 2. Estrutura Básica do Código
Use o componente `use client` e importe as diretrizes de UI de `.agents/rules/front.md`.

Exemplo de layout:
```tsx
'use client';

import React from 'react';
import { SectionCard, StatCard, DataField } from '@/components/UI'; // Se existirem centralizados
import { useQuery } from '@tanstack/react-query';
import { LayoutDashboard, TrendingUp } from 'lucide-react';

export default function NovaPagina() {
  return (
    <div className="w-full max-w-6xl mx-auto space-y-8 animate-in fade-in duration-500">
      {/* Header */}
      <div>
        <h1 className="text-3xl font-bold text-white">Título da Página</h1>
        <p className="text-zinc-500 text-sm">Descrição curta da funcionalidade.</p>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <StatCard icon={TrendingUp} iconBg="bg-blue-600" label="Métrica" value="R$ 0,00" />
      </div>

      {/* Conteúdo Principal */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2">
          <SectionCard icon={LayoutDashboard} iconColor="text-blue-500" title="Dados Principais">
             {/* Tabelas ou Listas aqui */}
          </SectionCard>
        </div>
        <div className="space-y-6">
          <SectionCard icon={TrendingUp} iconBg="bg-zinc-600" title="Resumo Extra">
             <DataField label="Informação" value="Exemplo" />
          </SectionCard>
        </div>
      </div>
    </div>
  );
}
```

### 3. Integração com TanStack Query
Certifique-se de envolver o conteúdo em estados de `loading` e `error` consistentes.

### 4. Responsividade
Sempre teste o layout em diferentes tamanhos de tela, garantindo que o `grid` se ajuste corretamente.
