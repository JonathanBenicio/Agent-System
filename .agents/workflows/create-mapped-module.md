---
description: Como criar um novo módulo mapeado (Mapper -> Store -> Hook)
---

Siga este workflow para adicionar uma nova funcionalidade que consome dados da API/Supabase, garantindo consistência através do padrão **Single Source of Truth**.

### 1. Definir a Interface Plana
No arquivo da store (`src/store/use<Entidade>Store.ts`), defina uma interface que represente os campos exatamente como a UI precisa (ex: `EntidadePlana`).
- Evite aninhamento excessivo.
- Use nomes de campos que reflitam o que ele exibe (ex: `statusVencimento` em vez de `etapa`).

### 2. Criar o Mapeador Centralizado
Crie o arquivo em `src/lib/<entidade>-mapper.ts`.

// turbo
#### Passo 2.1: Estruturar as funções de mapeamento
Crie as funções `mapSupabaseTo<Entidade>` e `mapSupabaseTo<Entidades>`.
- Use a regra `.agents/rules/arquitetura-mappers.md` como referência.
- Trate sempre valores nulos ou campos de fallback.
- Se houver lógica de status (ex: "Em Aberto", "Cancelado"), implemente como uma constante/função dentro do mapeador.

### 3. Implementar na Store (Zustand)
No arquivo `src/store/use<Entidade>Store.ts`:
- Importe as funções do mapeador.
- Dentro de `fetch<Entidade>`, chame o mapeador imediatamente após receber o JSON da API.
```typescript
const data = await response.json();
const mapped = mapSupabaseTo<Entidades>(data.registros);
set({ <entidade>s: mapped });
```

### 4. Implementar no Query Hook (TanStack Query)
No arquivo `src/hooks/use<Entidade>Query.ts`:
- Utilize o mapeador dentro da `queryFn`.
- Isso garante que os dados em cache e os dados da store sigam exatamente o mesmo formato.

### 5. Consumir na UI
Utilize os campos planos da interface.
- Se precisar dos dados brutos para um "Ver Detalhes JSON", use o campo `omieData` (opcional).
- As tabelas e dashboards devem consumir exclusivamente os campos mapeados (ex: `venda.statusComissao`).

### Checklist de Conclusão:
- [ ] O mapeamento não está duplicado (inline) na store ou hook?
- [ ] O arquivo `src/lib/<entidade>-mapper.ts` foi criado e exportado corretamente?
- [ ] Todos os campos na tabela usam nomes legíveis vindos do mapper?
- [ ] A lógica de cores/status está centralizada ou é derivada dos campos mapeados?
