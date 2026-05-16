# Adjust Default Models and Catalog

## Goal
Fix connectivity test errors by updating invalid/futuristic model names to real, existing ones across the configuration and model catalog.

## Tasks
- [ ] Task 1: Update `src/AgenticSystem.Api/appsettings.json` → Verify: `AgenticSystem:Claude:DefaultModel` is `claude-3-5-sonnet-latest`.
- [ ] Task 2: Update `src/AgenticSystem.Infrastructure/Configuration/AgenticSystemSettings.cs` → Verify: `ClaudeSettings.DefaultModel` default value is `claude-3-5-sonnet-latest`.
- [ ] Task 3: Update `src/AgenticSystem.Infrastructure/LLM/LLMManager.cs` → Verify: `ProviderModelCatalog` contains correct models for OpenAI, Gemini, and Claude.
- [ ] Task 4: Run build to ensure no syntax errors → Verify: `dotnet build` succeeds.

## Done When
- [ ] Configuration files use `claude-3-5-sonnet-latest` as default.
- [ ] `LLMManager.cs` has the updated `ProviderModelCatalog`.
- [ ] Project builds successfully.
