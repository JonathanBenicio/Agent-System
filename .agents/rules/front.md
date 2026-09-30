# Regras de Frontend (UI Patterns)

Este guia define os padrões visuais e de componentes para garantir uma interface consistente e de alta qualidade.

---

## 🎨 Design System
- **Paleta de Cores**: Utilize as cores do Tailwind CSS ou as variáveis definidas no tema do projeto.
- **Dark Mode**: Garanta suporte total ao tema escuro em todos os componentes.
- **Bordas e Curvas**: O padrão visual do projeto utiliza cantos arredondados e curvas suaves por padrão (`rounded-xl` / 12px a `rounded-3xl` / 24px) para cards, inputs, botões e barras laterais. Evite cantos vivos de 0px ou estilos brutalistas secos, mantendo um design amigável e refinado.
- **Interatividade**: Utilize transições e estados de hover para melhorar a experiência do usuário.

---

## 📦 Componentes e Padrões
- **Reuso**: Centralize componentes comuns em `src/components/ui`.
- **Composição**: Prefira a composição de componentes em vez de grandes blocos de código.
- **Acessibilidade**: Siga as práticas de acessibilidade (ARIA, contraste, semântica HTML).

---

## 🚥 Convenções de Status
- **Sucesso**: Tons de verde (`emerald`).
- **Alerta**: Tons de amarelo/laranja (`amber`).
- **Erro**: Tons de vermelho (`rose`).
- **Info**: Tons de azul (`blue`).

---

## 🛠️ Tecnologias Recomendadas
- **Icons**: Lucide React.
- **Tables**: TanStack Table.
- **Forms**: React Hook Form + Zod.
