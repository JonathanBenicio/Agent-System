// Catálogo de capacidades sugeridas em PT-BR com descrições técnicas para Tooltip
export const CAPABILITY_CATALOG: Record<string, Record<string, string>> = {
  'Inteligência': {
    'planejamento': 'Permite que o orquestrador solicite a criação de planos de ação estruturados.',
    'raciocínio-lógico': 'Sinaliza alta capacidade analítica para resolução de problemas complexos.',
    'análise-de-dados': 'Otimiza o processamento de tabelas, CSVs e estruturas JSON.',
    'sumarização': 'Especialidade em condensar grandes volumes de texto preservando pontos chave.'
  },
  'Técnico': {
    'execução-de-código': 'Autoriza o uso de ferramentas de sandbox para rodar scripts (Python/C#).',
    'integração-api': 'Foco em chamadas REST/GraphQL e orquestração de serviços externos.',
    'gestão-de-arquivos': 'Permite leitura, escrita e organização de sistemas de arquivos.',
    'busca-web': 'Habilita a navegação ativa para extração de dados atualizados da internet.'
  },
  'Conhecimento': {
    'acesso-rag': 'Especialista em busca semântica na base de conhecimento vetorial.',
    'memória-longo-prazo': 'Permite ao agente persistir e recuperar fatos entre diferentes sessões.',
    'multimodal': 'Capacidade de interpretar imagens, áudio ou documentos complexos.',
    'extração-entidades': 'Foco em identificar e isolar dados específicos em textos não estruturados.'
  }
};

/**
 * Procura a descrição de uma capacidade no catálogo.
 */
export function getCapabilityDescription(tag: string): string | undefined {
  for (const category of Object.values(CAPABILITY_CATALOG)) {
    if (category[tag]) return category[tag];
  }
  return undefined;
}
