---
trigger: always_on
---

# Padrão de Mapeamento Centralizado (Single Source of Truth)

Este documento define o padrão arquitetural para transformação de dados brutos (vindos do Omie ou Supabase) para as interfaces do frontend da Tabatine.

## 🎯 Objetivo
Evitar a duplicação de lógica de transformação em stores, hooks e componentes. Garantir que uma mudança na estrutura de dados da API precise de alteração em apenas um local (o mapeador).

## 🏗️ Estrutura do Padrão

### 1. Localização
Todos os mapeadores devem residir em `src/lib/<entidade>-mapper.ts`.

### 2. Funções Principais
Cada arquivo de mapeador deve exportar, no mínimo:
- `mapSupabaseTo<Entidade>(raw: any): Entidade`: Mapeia um único registro.
- `mapSupabaseTo<Entidades>(rawArray: any[]): Entidade[]`: Mapeia um array de registros.

### 3. Responsabilidades do Mapeador
- **Flattening**: Achatar objetos aninhados (ex: `det.produto.descricao` -> `produto`).
- **Conversão de Tipos**: Converter strings 'S'/'N' para booleans, ou números para strings formatadas.
- **Formatação de Datas**: Converter strings de data (ISO ou BR) para o formato esperado pelo componente.
- **Lógica de Status**: Definir labels de status (ex: "Faturado", "Atrasado") baseados em campos múltiplos (etapa, data vencimento, etc).
- **Tratamento de Nulos**: Fornecer valores padrão (Null-safe) para evitar erros de renderização.

## 💻 Exemplo de Implementação

```typescript
// src/lib/exemplo-mapper.ts
import { ExemploInterface } from '@/store/useExemploStore';

export function mapSupabaseToExemplo(raw: any): ExemploInterface {
  if (!raw) return { id: 0, status: 'Vazio' }; // Safe default

  return {
    id: raw.OmieId || 0,
    descricao: raw.Descricao || 'Sem Descrição',
    valor: Number(raw.Valor || 0),
    ativo: raw.Ativa === 'S',
    dataFormated: new Date(raw.Data).toLocaleDateString('pt-BR')
  };
}

export function mapSupabaseToExemplos(rawArray: any[]): ExemploInterface[] {
  if (!Array.isArray(rawArray)) return [];
  return rawArray.map(mapSupabaseToExemplo);
}
```

## 🛠️ Como Utilizar
Sempre que precisar buscar dados da API ou do Supabase nas **Stores** ou **Query Hooks**, utilize o mapeador correspondente imediatamente após a resposta:

```typescript
// useExemploStore.ts
const data = await response.json();
const mappedData = mapSupabaseToExemplos(data.lista);
set({ itens: mappedData });
```

## 🚫 O que EVITAR
- **Mapeamento Inline**: Nunca faça `.map(item => ({ ... }))` diretamente na store ou hook.
- **Lógica de Status no Componente**: A lógica de se uma venda está "Atrasada" deve estar no Mapper, não no JSX da tabela.
- **Acesso Direto ao `omieData`**: Embora o objeto bruto seja mantido no campo `omieData` para drill-down, a UI deve sempre preferir os campos mapeados na interface plana.
