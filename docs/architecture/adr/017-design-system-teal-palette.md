# ADR-017: Identidade Visual e Banimento da Cor Violeta (Purple Ban)

## Status
Accepted (Retrospective)

## Context
A interface do usuário estava utilizando uma paleta baseada em tons de violeta/roxo. Para alinhar com as diretrizes de design do projeto (que buscam uma estética premium, moderna e específica) e evitar clichês visuais, precisávamos redefinir a paleta de cores.

## Decision
Decidimos banir o uso da cor violeta (**Purple Ban**) e migrar toda a identidade visual e componentes do frontend para uma paleta baseada em **Teal** (Verde-azulado) e tons escuros premium.

## Rationale
1. **Diferenciação**: Evita a estética comum de muitas ferramentas de IA que usam roxo/degradê roxo.
2. **Acessibilidade e Contraste**: Tons de teal combinam muito bem com temas escuros (Dark Mode), oferecendo excelente legibilidade e contraste.
3. **Consistência**: Garante que todos os componentes sigam a mesma linguagem visual definida nas regras do projeto.

## Trade-offs
- **Esforço de UI**: Exigiu varrer os arquivos CSS e componentes para substituir as classes de cores antigas.

## Consequences
- **Positive**: Interface com visual único, limpo e profissional; total aderência às regras estéticas do projeto.
- **Negative**: Necessidade de atenção redobrada ao criar novos componentes para não reintroduzir cores banidas.
- **Mitigation**: Definição de variáveis CSS globais para as cores permitidas, facilitando o uso correto pelos desenvolvedores.
