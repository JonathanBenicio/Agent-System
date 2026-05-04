import { Construction } from 'lucide-react'

interface PlaceholderPageProps {
  title: string
  description?: string
}

export function PlaceholderPage({ title, description }: PlaceholderPageProps) {
  return (
    <div className="flex items-center justify-center h-full">
      <div className="text-center">
        <Construction className="w-12 h-12 text-zinc-600 mx-auto mb-4" />
        <h2 className="text-lg font-semibold text-zinc-300 mb-1">{title}</h2>
        <p className="text-sm text-zinc-500">
          {description ?? 'Em desenvolvimento — disponível em breve.'}
        </p>
      </div>
    </div>
  )
}
