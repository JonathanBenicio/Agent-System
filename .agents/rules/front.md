---
description: Padrões visuais, UI/UX, Design System (Tailwind v4) e Componentes Reutilizáveis
trigger: model_decision
---

# Tabatine — Regras de Frontend (UI Patterns)

Este guia define os padrões visuais e componentes do projeto para garantir uma interface premium e consistente.

## 🎨 Design System
- **Paleta**: Use as cores do Tailwind CSS (principalmente `zinc` para neutros).
- **Dark Mode**: Tudo deve ser construído pensando em fundo escuro (`bg-black` ou `bg-zinc-950`).
- **Bordas**: Use `border-zinc-800/50` para separar elementos no fundo escuro.
- **Interatividade**: Sempre adicione `transition-all` e efeitos de hover como `hover:bg-zinc-800` ou `hover:text-white`.

## 📦 Componentes Reutilizáveis (Padrão de Código)

### SectionCard
Usado para agrupar informações relacionadas com um título e ícone.
```tsx
function SectionCard({ icon: Icon, iconColor, title, children }: {
  icon: any; iconColor: string; title: string; children: React.ReactNode;
}) {
  return (
    <div className="p-6 rounded-2xl bg-zinc-900/30 border border-zinc-800/50 backdrop-blur-xl">
      <h2 className="text-lg font-bold text-white mb-6 flex items-center gap-2">
        <Icon className={iconColor} size={20} />
        {title}
      </h2>
      {children}
    </div>
  );
}
```

### StatCard
Usado para exibir métricas importantes de forma destacada.
```tsx
function StatCard({ icon: Icon, iconBg, label, value, subValue }: {
  icon: any; iconBg: string; label: string; value: string; subValue?: string;
}) {
  return (
    <div className="p-4 rounded-xl bg-zinc-900/50 border border-zinc-800/50 flex items-center gap-4">
      <div className={`p-3 rounded-xl ${iconBg}`}>
        <Icon size={20} className="text-white" />
      </div>
      <div>
        <p className="text-[10px] uppercase font-bold text-zinc-500 tracking-wider">{label}</p>
        <p className="text-lg font-black text-white">{value}</p>
        {subValue && <p className="text-[10px] text-zinc-500">{subValue}</p>}
      </div>
    </div>
  );
}
```

### DataField
Usado para exibir pares chave-valor de forma limpa.
```tsx
function DataField({ label, value, className = 'text-zinc-300', large = false }: {
  label: string; value: React.ReactNode; className?: string; large?: boolean;
}) {
  return (
    <div className="space-y-1">
      <span className="text-[10px] uppercase font-bold text-zinc-500 tracking-wider">{label}</span>
      <p className={`${large ? 'text-xl font-black' : 'font-medium'} ${className}`}>{value || '--'}</p>
    </div>
  );
}
```

## 🚥 Convenções de Cores para Status
- **Sucesso (Ativo/Pago/Faturado)**: `text-emerald-400`, `bg-emerald-500/10`, `border-emerald-500/20`.
- **Alerta (Pendente/Aviso)**: `text-amber-400`, `bg-amber-500/10`, `border-amber-500/20`.
- **Erro (Inativo/Cancelado/Erro)**: `text-rose-400`, `bg-rose-500/10`, `border-rose-500/20`.
- **Info (Neutro/Destaque)**: `text-blue-400`, `bg-blue-500/10`, `border-blue-500/20`.

## 🛠️ Tecnologias
- **Lucide React**: Para todos os ícones.
- **TanStack Table**: Para todas as listas de dados.
- **Tailwind CSS v4**: Para estilização rápida e responsiva.

## ⚛️ Next.js & React 19.2 (UI Patterns)
- **Otimização**: Use `next/image` e `next/font/google` para todas as mídias e tipografia de forma rigorosa. Isso previne Cumulative Layout Shift (CLS) e otimiza o Time to Interactive.
- **Transições Suaves**: Abrace a API de View Transitions (React 19.2) encapsulada no React UI.
- **Preservação de Estado**: Utilize o novo componente `<Activity>` do React 19.2 para lidar com visibilidade tab-like, pausando render sem custo de re-mount/un-mount.
- **Acessibilidade Absoluta**: Use HTML semântico, atributos ARIA nativos e defina focos em inputs, mantendo compatibilidade com WCAG 2.1 AA.
- **Desempenho no Compilador**: Codifique com clareza; o React Compiler automatiza boa parte da memoização (`useMemo`, `useCallback`). Apenas otimize loops densos.