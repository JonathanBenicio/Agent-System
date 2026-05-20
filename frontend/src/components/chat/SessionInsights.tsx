import { Brain, CheckCircle2, Lightbulb, Target } from 'lucide-react'
import type { SessionInsightsDto } from '@/types/api'

interface SessionInsightsProps {
  insights: SessionInsightsDto
}

export function SessionInsights({ insights }: SessionInsightsProps) {
  const hasInsights = 
    insights.facts.length > 0 || 
    insights.decisions.length > 0 || 
    insights.preferences.length > 0 || 
    insights.actionItems.length > 0

  if (!hasInsights) {
    return (
      <div className="p-8 text-center text-zinc-500 text-sm italic">
        Nenhum insight extraído desta sessão ainda.
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-6 p-4">
      {insights.facts.length > 0 && (
        <section>
          <div className="flex items-center gap-2 text-blue-400 mb-2 font-semibold text-xs uppercase tracking-wider">
            <Brain className="w-3.5 h-3.5" /> Fatos Aprendidos
          </div>
          <ul className="space-y-2">
            {insights.facts.map((fact, i) => (
              <li key={i} className="text-sm text-zinc-300 bg-zinc-850/50 p-2 rounded-lg border border-zinc-800/50">
                {fact}
              </li>
            ))}
          </ul>
        </section>
      )}

      {insights.decisions.length > 0 && (
        <section>
          <div className="flex items-center gap-2 text-emerald-400 mb-2 font-semibold text-xs uppercase tracking-wider">
            <CheckCircle2 className="w-3.5 h-3.5" /> Decisões Tomadas
          </div>
          <ul className="space-y-2">
            {insights.decisions.map((decision, i) => (
              <li key={i} className="text-sm text-zinc-300 bg-zinc-850/50 p-2 rounded-lg border border-zinc-800/50">
                {decision}
              </li>
            ))}
          </ul>
        </section>
      )}

      {insights.preferences.length > 0 && (
        <section>
          <div className="flex items-center gap-2 text-amber-400 mb-2 font-semibold text-xs uppercase tracking-wider">
            <Lightbulb className="w-3.5 h-3.5" /> Preferências do Usuário
          </div>
          <ul className="space-y-2">
            {insights.preferences.map((pref, i) => (
              <li key={i} className="text-sm text-zinc-300 bg-zinc-850/50 p-2 rounded-lg border border-zinc-800/50">
                {pref}
              </li>
            ))}
          </ul>
        </section>
      )}

      {insights.actionItems.length > 0 && (
        <section>
          <div className="flex items-center gap-2 text-purple-400 mb-2 font-semibold text-xs uppercase tracking-wider">
            <Target className="w-3.5 h-3.5" /> Próximos Passos
          </div>
          <ul className="space-y-2">
            {insights.actionItems.map((item, i) => (
              <li key={i} className="text-sm text-zinc-300 bg-zinc-850/50 p-2 rounded-lg border border-zinc-800/50">
                {item}
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  )
}
