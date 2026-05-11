---
description: Revisão estruturada de Pull Requests com checklist automatizado.
---

# Workflow: Revisão de Pull Request

Este workflow descreve o processo de revisão de código para garantir qualidade, segurança e conformidade com os padrões do projeto.

### 1. Preparação
- Leia a descrição do PR para entender o objetivo.
- Verifique se o PR resolve um problema específico ou adiciona uma funcionalidade planejada.

### 2. Checklist de Revisão

| Categoria | Check |
|-----------|-------|
| **Corretude** | O código faz o que se propõe? Trata casos de borda? |
| **Segurança** | Existem vulnerabilidades óbvias ou exposição de segredos? |
| **Qualidade** | O código é legível? Segue os padrões de Clean Code? |
| **Performance** | Existem loops desnecessários ou consultas ineficientes? |
| **Testes** | Existem testes unitários para a nova lógica? |

### 3. Procedimento Automatizado
Execute os scripts de verificação disponíveis em `.agents/scripts/` para validar a qualidade do código.

```bash
python .agents/scripts/checklist.py .
```

### 4. Feedback
- Forneça comentários construtivos.
- Marque problemas críticos como bloqueantes.
- Sugira melhorias de forma clara.

---

## Finalização
O PR deve ser aprovado apenas quando todos os pontos críticos forem resolvidos e as verificações automatizadas passarem.
