$ErrorActionPreference = 'Stop'
$taskRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskRuntime = if ($env:BACKEND_VALIDATION_OUTPUT_DIR) { $env:BACKEND_VALIDATION_OUTPUT_DIR } else { Join-Path $taskRoot 'tests/TestResults/backend-core-remediation/current' }
$taskHistoricalOutput = [System.IO.Path]::GetFullPath((Join-Path $taskRoot 'tests/TestResults/backend-documentation/current')).TrimEnd('\', '/')
$taskListener = Get-NetTCPConnection -LocalPort 5188 -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
if ($taskListener) { throw 'Port 5188 is already occupied; refusing to launch diagnostics against an existing API.' }
if ([System.IO.Path]::GetFullPath($taskRuntime).TrimEnd('\', '/') -ieq $taskHistoricalOutput) {
  throw 'Refusing to write API logs into historical backend-documentation validation artifacts.'
}
$taskComposeProject = $env:BACKEND_VALIDATION_COMPOSE_PROJECT
$taskDatabase = $env:BACKEND_VALIDATION_DATABASE
if ([string]::IsNullOrWhiteSpace($taskComposeProject) -or $taskComposeProject -notmatch '^[a-z0-9][a-z0-9_-]*$') { throw 'Set BACKEND_VALIDATION_COMPOSE_PROJECT to the unique Compose project used for this run.' }
if ([string]::IsNullOrWhiteSpace($taskDatabase) -or $taskDatabase -notmatch '^review_pr152_[a-z0-9_]+$') { throw 'Set BACKEND_VALIDATION_DATABASE to an exclusive review_pr152_* database.' }
New-Item -ItemType Directory -Path $taskRuntime -Force | Out-Null
$taskDll = Join-Path $taskRoot 'src/AgenticSystem.Api/bin/Release/net10.0/AgenticSystem.Api.dll'
if (!(Test-Path -LiteralPath $taskDll)) { throw 'Build Release da API necessário.' }
$taskTargetManifest = [ordered]@{
  composeProject = $taskComposeProject
  database = $taskDatabase
  host = '127.0.0.1'
  port = 55432
}
[System.IO.File]::WriteAllText(
  (Join-Path $taskRuntime 'api-target.json'),
  ($taskTargetManifest | ConvertTo-Json -Compress),
  [System.Text.UTF8Encoding]::new($false))
# Synthetic credentials exclusively for the loopback validation database.
$taskConfig = @{
  ASPNETCORE_ENVIRONMENT = 'Validation'
  ASPNETCORE_URLS = 'http://127.0.0.1:5188'
  ConnectionStrings__SessionStore = "Host=127.0.0.1;Port=55432;Database=$taskDatabase;Username=validation;Password=validation_local_only"
  AgenticSystem__AdminApiKey = 'documentation-validation-bootstrap-only'
  AgenticSystem__Jwt__SecretKey = 'documentation-validation-jwt-secret-local-only-2026'
  AgenticSystem__Encryption__Key = '0123456789abcdef0123456789abcdef'
  AgenticSystem__Cors__AllowedOrigins__0 = if ($env:BACKEND_VALIDATION_CORS_ORIGIN) { $env:BACKEND_VALIDATION_CORS_ORIGIN } else { 'http://127.0.0.1:5188' }
  AgenticSystem__LocalExecution__StorageMode = 'PostgreSQL'
  AgenticSystem__Memory__VectorStoreType = 'PostgreSQL'
  AgenticSystem__Ollama__Enabled = 'true'
  AgenticSystem__Ollama__BaseUrl = 'http://127.0.0.1:11435'
  AgenticSystem__Ollama__DefaultModel = 'qwen2.5:0.5b'
  AgenticSystem__Ollama__EmbeddingModel = 'nomic-embed-text'
  AgenticSystem__Ollama__Priority = '1'
  AgenticSystem__OpenAI__Enabled = if ($env:BACKEND_VALIDATION_OPENAI_ENABLED) { $env:BACKEND_VALIDATION_OPENAI_ENABLED } else { 'false' }
  AgenticSystem__OpenAI__ApiKey = if ($env:BACKEND_VALIDATION_OPENAI_API_KEY) { $env:BACKEND_VALIDATION_OPENAI_API_KEY } else { '' }
  AgenticSystem__OpenAI__BaseUrl = if ($env:BACKEND_VALIDATION_OPENAI_BASE_URL) { $env:BACKEND_VALIDATION_OPENAI_BASE_URL } else { 'https://api.openai.com/' }
  AgenticSystem__Gemini__Enabled = 'false'
  AgenticSystem__Claude__Enabled = 'false'
  AgenticSystem__OpenRouter__Enabled = 'false'
  AgenticSystem__RAG__ReRanking__Enabled = 'false'
  ProtocolHosting__A2A__Enabled = 'false'
  ProtocolHosting__AgUI__Enabled = 'false'
}
foreach ($taskEntry in $taskConfig.GetEnumerator()) { [Environment]::SetEnvironmentVariable($taskEntry.Key, $taskEntry.Value, 'Process') }
# Empty content root avoids user appsettings/uploads; Validation environment avoids user secrets.
Push-Location $taskRuntime
try { & dotnet $taskDll *> (Join-Path $taskRuntime 'api.log'); exit $LASTEXITCODE }
finally { Pop-Location }
