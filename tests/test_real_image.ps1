Write-Host "1. Baixando imagem placeholder real-house..."
Invoke-WebRequest -Uri "https://picsum.photos/800/600.jpg" -OutFile "C:\Users\Jonathan\Documents\Developer\GitHub\Agent-System\tests\real-house.jpg"

Write-Host "2. Copiando imagem para o Container da API..."
docker cp C:\Users\Jonathan\Documents\Developer\GitHub\Agent-System\tests\real-house.jpg agent-system-agentic-api-1:/app/real-house.jpg

Write-Host "3. Enviando ordem de orquestração via API..."
$payload = @{
    imagePath = "/app/real-house.jpg"
    price = 320000
    bedrooms = 3
    location = "Jardim São Paulo"
    phone = "15 991903262"
} | ConvertTo-Json

if ([string]::IsNullOrWhiteSpace($env:AGENT_SYSTEM_API_KEY)) {
    throw "Set AGENT_SYSTEM_API_KEY before running this script."
}

$headers = @{
    "Content-Type" = "application/json"
    "X-Tenant-Id" = "admin"
    "X-Api-Key" = $env:AGENT_SYSTEM_API_KEY
}

Write-Host "Iniciando Workflow na API Estatica (que executa C# nativo)..."
$response = Invoke-RestMethod -Uri "http://localhost:8080/api/workflow/executions/start/banner-production" -Method Post -Headers $headers -Body $payload
$executionId = $response.executionId

Write-Host "Resposta da API: $($response | ConvertTo-Json)"

Write-Host "4. Aguardando a execução do Workflow Engine e da Pipeline LLM..."
$status = "Running"
while ($status -notin @("Completed", "Failed", "Cancelled")) {
    Start-Sleep -Seconds 5
    $execStatus = Invoke-RestMethod -Uri "http://localhost:8080/api/workflow/executions/$executionId" -Method Get -Headers $headers
    $status = $execStatus.status
    Write-Host "Status atual: $status"
}

Write-Host "5. Extraindo resultados..."
docker cp agent-system-agentic-api-1:/app/clean_real-house.jpg C:\Users\Jonathan\Documents\Developer\GitHub\Agent-System\tests\clean_real-house.jpg
docker cp agent-system-agentic-api-1:/app/banner_clean_real-house.jpg C:\Users\Jonathan\Documents\Developer\GitHub\Agent-System\tests\banner_clean_real-house.jpg

Write-Host "Verificando se chegaram no host:"
Get-ChildItem -Path C:\Users\Jonathan\Documents\Developer\GitHub\Agent-System\tests\*real-house*
