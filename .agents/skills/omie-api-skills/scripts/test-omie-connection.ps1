<#
.SYNOPSIS
    Testa a conectividade com a API da Omie e valida as credenciais.

.DESCRIPTION
    Este script realiza uma chamada 'ListarClientes' (1 registro) para verificar se o AppKey e AppSecret estão corretos e se o IP não está bloqueado.

.PARAMETER AppKey
    A chave da aplicação Omie. Se omitido, tenta ler de $env:OMIE_APP_KEY.

.PARAMETER AppSecret
    O segredo da aplicação Omie. Se omitido, tenta ler de $env:OMIE_APP_SECRET.

.EXAMPLE
    pwsh test-omie-connection.ps1 -AppKey '123' -AppSecret 'abc'
#>
param(
    [string]$AppKey = $env:OMIE_APP_KEY,
    [string]$AppSecret = $env:OMIE_APP_SECRET
)

# Se não houver parâmetros nem env vars, tenta ler do appsettings.json local (se existir no contexto do Worker)
if (-not $AppKey -or -not $AppSecret) {
    Write-Host "[!] Credenciais não encontradas em variáveis de ambiente." -ForegroundColor Yellow
    Write-Host "Uso: pwsh test-omie-connection.ps1 -AppKey '...' -AppSecret '...'"
    exit 1
}

$Url = "https://app.omie.com.br/api/v1/geral/clientes/"
$Body = @{
    call = "ListarClientes"
    app_key = $AppKey
    app_secret = $AppSecret
    param = @(
        @{
            pagina = 1
            registros_por_pagina = 1
        }
    )
} | ConvertTo-Json -Compress

try {
    Write-Host "[*] Testando conexão com Omie API (ListarClientes)... " -NoNewline
    $Response = Invoke-RestMethod -Uri $Url -Method Post -Body $Body -ContentType "application/json"
    
    if ($Response.faultstring) {
        Write-Host "FALHA!" -ForegroundColor Red
        Write-Host "Erro Omie: $($Response.faultstring)" -ForegroundColor Yellow
    } else {
        Write-Host "SUCESSO!" -ForegroundColor Green
        Write-Host "------------------------------------------------"
        Write-Host "Status: Conectado"
        Write-Host "Total de Registros Encontrados: $($Response.total_de_registros)"
    }
} catch {
    Write-Host "FALHA CRÍTICA!" -ForegroundColor Red
    Write-Host "Erro: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $ErrorBody = $_.Exception.Response.GetResponseStream() | ForEach-Object { (New-Object System.IO.StreamReader($_)).ReadToEnd() }
        Write-Host "Detalhes: $ErrorBody" -ForegroundColor Gray
    }
}
