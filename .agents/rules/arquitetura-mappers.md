# Padrão Arquitetural: Mapeadores (SSOT)

Este documento define o padrão arquitetural para transformação de dados brutos (vindos de APIs ou Banco de Dados) para as interfaces consumidas pela UI.

---

## 🏗️ Objetivo
Garantir uma **Fonte Única de Verdade (SSOT)**, onde a lógica de transformação de dados é centralizada, facilitando a manutenção e garantindo que diferentes partes da UI consumam dados no mesmo formato.

---

## 🛠️ Regras de Implementação

1. **Localização**: Os mapeadores devem residir em `src/lib/<entidade>-mapper.ts` ou pasta similar.
2. **Interface Plana**: O mapeador deve retornar uma interface "plana" otimizada para a UI, evitando que componentes precisem lidar com a complexidade de objetos aninhados.
3. **Tratamento de Nulos**: Sempre forneça valores de fallback para campos opcionais ou nulos.
4. **Lógica de Status**: Derivações de status, cores e rótulos amigáveis devem ser calculadas no mapeador.

---

## 📝 Exemplo de Padrão

```typescript
export interface MyEntityFlat {
  id: string;
  name: string;
  statusLabel: string;
  statusColor: string;
  raw: any; // Opcional: manter referência ao original para depuração
}

export function mapRawToFlat(raw: any): MyEntityFlat {
  return {
    id: raw.uuid,
    name: raw.full_name || 'Sem Nome',
    statusLabel: mapStatusLabel(raw.status),
    statusColor: mapStatusColor(raw.status),
    raw: raw
  };
}
```

---

## ✅ Benefícios
- **Desacoplamento**: A UI não quebra se o payload da API mudar; apenas o mapeador precisa ser atualizado.
- **Testabilidade**: Mapeadores são funções puras e fáceis de testar.
- **Reuso**: A mesma lógica de mapeamento é usada em stores (Zustand) e hooks (React Query).
