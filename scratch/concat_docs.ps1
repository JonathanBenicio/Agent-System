$outputFile = "CONSOLIDATED_DOCS.md"
Remove-Item -Path $outputFile -ErrorAction SilentlyContinue

Add-Content -Path $outputFile -Value "# CONSOLIDATED DOCUMENTATION`n" -Encoding UTF8

$files = @(
    "README.md",
    "docs\architecture\agent-registry.json",
    "docs\architecture\agent-registry.md",
    "docs\architecture\agent-registry.schema.json",
    "docs\architecture\backend-architecture-explained.md",
    "docs\architecture\design-philosophy.md",
    "docs\architecture\diagrams.md",
    "docs\architecture\document-pipeline.md",
    "docs\architecture\rag-flow.md",
    "docs\architecture\skills-vs-tools.md",
    "docs\bdd\agent-management.feature",
    "docs\bdd\agent-nao-encontrado.feature",
    "docs\bdd\api-key-masking-embedding.feature",
    "docs\bdd\autenticacao-multi-scheme.feature",
    "docs\bdd\backend-apis.feature",
    "docs\bdd\chat-dedicado-via-lista.feature",
    "docs\bdd\chat-interface.feature",
    "docs\bdd\contrato-api-chat-dedicado.feature",
    "docs\bdd\embedding-migration.feature",
    "docs\bdd\gateway-dashboard.feature",
    "docs\bdd\historico-separado-por-agent.feature",
    "docs\bdd\llm-providers.feature",
    "docs\bdd\mcp-plugins.feature",
    "docs\bdd\mensagem-direto-ao-agent.feature",
    "docs\bdd\rate-limiting-chat.feature",
    "docs\bdd\README.md",
    "docs\bdd\scheduled-tasks.feature",
    "docs\bdd\settings-config.feature",
    "docs\bdd\signalr-realtime.feature",
    "docs\bdd\transversal-ux.feature",
    "docs\bdd\voltar-roteamento-automatico.feature",
    "docs\planejamento\AI_Advanced_Capabilities_Roadmap.md",
    "docs\planejamento\AI_Capabilities_Gaps.md",
    "docs\planejamento\framework-first-migration-plan.md",
    "docs\planejamento\MAF_NATIVE_REFACTORING.md",
    "docs\planejamento\overengineering-assessment.md",
    "docs\planejamento\README.md",
    "docs\planejamento\REFACTORING_CHECKPOINT_PHASE1.md",
    "docs\planejamento\REFACTORING_CHECKPOINT_PHASE2.md",
    "docs\planejamento\REFACTORING_CHECKPOINT_PHASE3.md",
    "docs\planejamento\REFACTORING_CHECKPOINT_PHASE4.md",
    "docs\planejamento\REFACTORING_PROGRESS.md",
    "docs\referencia-externa\agent-framework.md",
    "docs\referencia-externa\README.md",
    "docs\agentic-design-manifesto.md",
    "docs\DSA-AgenticSystem.md",
    "docs\extension-examples.md",
    "docs\INDEX.md",
    "docs\obsidian-vault.md",
    "docs\PRD-Sistema-Agentic.md",
    "docs\TECHNICAL_ARCHITECTURE_GUIDE.md",
    "docs\USER-STORIES.md"
)

foreach ($file in $files) {
    if (Test-Path $file) {
        Add-Content -Path $outputFile -Value "`n`n---`n## File: $file`n---`n" -Encoding UTF8
        $content = Get-Content -Path $file -Raw -Encoding UTF8
        Add-Content -Path $outputFile -Value $content -Encoding UTF8
        Write-Host "Added $file"
    } else {
        Write-Host "File not found: $file" -ForegroundColor Yellow
    }
}

Write-Host "Consolidation complete: $outputFile" -ForegroundColor Green
