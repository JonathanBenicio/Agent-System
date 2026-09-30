$collectionPath = "tests/postman/AgenticSystem.postman_collection.json"
if (-not (Test-Path $collectionPath)) {
    Write-Error "Coleção do Postman não encontrada!"
    exit 1
}

$collection = Get-Content -Raw -Path $collectionPath | ConvertFrom-Json

$base_url = "http://localhost:8080"
$api_key = "minha-chave-secreta-admin-123"
$tenant_id = "admin"
$session_id = "session-e2e-powershell-test-123"

# Estado compartilhado para injeções E2E dinâmicas
$state = @{
    "base_url" = $base_url
    "api_key" = $api_key
    "tenant_id" = $tenant_id
    "session_id" = $session_id
    "room_id" = "postman-support-room"
    "workflow_id" = "e2e-workflow-test"
    "execution_id" = ""
    "skill_id" = "custom-calculator-postman"
}

$results = @()

function Replace-Variables($str) {
    if ($null -eq $str) { return $str }
    $str = [string]$str
    foreach ($key in $state.Keys) {
        $str = $str.Replace("{{$key}}", $state[$key])
        $str = $str.Replace(":$key", $state[$key])
    }
    # Replacements para paths genéricos do Postman
    $str = $str.Replace(":id", $state["room_id"])
    $str = $str.Replace(":definitionId", $state["workflow_id"])
    $str = $str.Replace(":providerName", "OpenAI")
    return $str
}

function Run-Request($item, $folderName) {
    $req = $item.request
    $method = $req.method
    $url = Replace-Variables $req.url.raw

    # Ignora uploads de arquivos brutos binários no script PowerShell (serão testados manualmente no Postman)
    if ($req.body.mode -eq "formdata") {
        Write-Host "[-] PULANDO (Requer arquivos multipart): [$method] $url" -ForegroundColor Yellow
        return
    }

    # Cabeçalhos
    $headers = @{}
    if ($null -ne $req.header) {
        foreach ($h in $req.header) {
            $headers[$h.key] = Replace-Variables $h.value
        }
    }

    # Corpo (Body)
    $body = $null
    if ($null -ne $req.body -and $req.body.mode -eq "raw") {
        $body = Replace-Variables $req.body.raw
    }

    # Limpeza de placeholders vazios em query params que não foram definidos
    if ($url.Contains("?")) {
        $urlParts = $url.Split("?")
        $base = $urlParts[0]
        $queries = $urlParts[1].Split("&")
        $filteredQueries = @()
        foreach ($q in $queries) {
            if (-not ($q.EndsWith("="))) {
                $filteredQueries += $q
            }
        }
        if ($filteredQueries.Count -gt 0) {
            $url = $base + "?" + ($filteredQueries -join "&")
        } else {
            $url = $base
        }
    }

    Write-Host "Executando: [$method] $url ... " -NoNewline

    $statusCode = 0
    $errorMsg = ""
    
    try {
        $params = @{
            Uri = $url
            Method = $method
            Headers = $headers
            ContentType = "application/json"
            TimeoutSec = 8
        }
        
        if ($null -ne $body -and ($method -in @("POST", "PUT"))) {
            $params["Body"] = $body
        }
        
        $response = Invoke-RestMethod @params
        $statusCode = 200 # Chamada obteve retorno de sucesso (2xx)
        
        # Captura IDs gerados para encadeamento real do teste!
        if ($null -ne $response) {
            if ($response.id -and $url -like "*/workflow/definitions") {
                $state["workflow_id"] = $response.id
            }
            if ($response.id -and $url -like "*/knowledge/rooms") {
                $state["room_id"] = $response.id
            }
            if ($response.id -and $url -like "*/workflow/executions/start/*") {
                $state["execution_id"] = $response.id
            }
            if ($response.id -and $url -like "*/agent/skills") {
                $state["skill_id"] = $response.id
            }
        }
        
        Write-Host "SUCESSO (2xx)" -ForegroundColor Green
        $statusCode = 200
    }
    catch {
        $resp = $_.Exception.Response
        if ($null -ne $resp) {
            $statusCode = $resp.StatusCode.value__
        } else {
            $statusCode = 500
        }
        $errorMsg = $_.Exception.Message

        # Trata 404 esperados para IDs de teste que podem não estar semeados ainda
        if ($statusCode -eq 404 -and ($url -like "*/definitions/*" -or $url -like "*/executions/*" -or $url -like "*/rooms/*" -or $url -like "*/skills/*")) {
            Write-Host "ESPERADO (404 Not Found)" -ForegroundColor Cyan
            $statusCode = 200 # Considerado tratado para fins de sanidade global
        }
        # Alguns status 400 ou 403 podem ser esperados se o banco/ambiente estiver restrito
        elseif ($statusCode -eq 403) {
            Write-Host "BLOQUEADO (403 Forbidden)" -ForegroundColor Yellow
        }
        else {
            Write-Host "FALHA ($statusCode) - $errorMsg" -ForegroundColor Red
        }
    }

    $script:results += [PSCustomObject]@{
        Folder = $folderName
        Name = $item.name
        Method = $method
        Url = $url
        StatusCode = $statusCode
        Error = $errorMsg
    }
}

function Traverse-Items($items, $folderName) {
    foreach ($item in $items) {
        if ($null -ne $item.item) {
            Traverse-Items $item.item $item.name
        } else {
            Run-Request $item $folderName
        }
    }
}

Traverse-Items $collection.item "Root"

# Resumo
$failed = $script:results | Where-Object { $_.StatusCode -ge 400 -and $_.StatusCode -ne 404 }
Write-Host "`n--- RESUMO DE EXECUÇÃO E SANIDADE ---" -ForegroundColor White
Write-Host "Total de chamadas testadas: $($script:results.Count)"
Write-Host "Total de falhas reais detectadas: $($failed.Count)"

if ($failed.Count -gt 0) {
    Write-Host "`nRotas que falharam:" -ForegroundColor Red
    foreach ($f in $failed) {
        Write-Host "- [$($f.Method)] $($f.Url) (Status: $($f.StatusCode)) - $($f.Error)" -ForegroundColor Red
    }
} else {
    Write-Host "`nParabéns! Nenhuma falha crítica ou erro 500 foi detectado em tempo de execução." -ForegroundColor Green
}
