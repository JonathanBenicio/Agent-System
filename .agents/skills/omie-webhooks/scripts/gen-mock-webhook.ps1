<#
.SYNOPSIS
    Gera um payload JSON de exemplo de um webhook da Omie.

.DESCRIPTION
    Este script gera um objeto JSON pronto para ser enviado via Postman ou cURL para testar o Ingestion do Tabatine Engine.

.PARAMETER Event
    O tipo de evento para o qual o payload será gerado.
    Valores suportados: produto.alterado, cliente.alterado, pedido.venda.alterado.

.EXAMPLE
    pwsh gen-mock-webhook.ps1 -Event 'produto.alterado'
#>
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("produto.alterado", "cliente.alterado", "pedido.venda.alterado")]
    [string]$Event
)

$Payload = @{
    message_id = [guid]::NewGuid().ToString()
    topic = "omie.webhooks"
    event = $Event
    author = @{
        email = "integration@omie.com.br"
        name = "Omie Webhook System"
    }
    content = @{
        id = (Get-Random -Minimum 1000 -Maximum 9999)
        timestamp = [DateTimeOffset]::Now.ToUnixTimeSeconds()
    }
}

# Adicionar detalhes específicos por tipo de evento
switch ($Event) {
    "produto.alterado" {
        $Payload.content += @{
            codigo = "PRD-" + (Get-Random -Minimum 100 -Maximum 999)
            descricao = "Produto de Teste Antigravity"
            valor_unitario = 150.50
        }
    }
    "cliente.alterado" {
        $Payload.content += @{
            codigo_cliente_omie = (Get-Random -Minimum 10000 -Maximum 99999)
            razao_social = "Cliente de Teste S.A."
            cnpj_cpf = "00.000.000/0001-91"
        }
    }
    "pedido.venda.alterado" {
        $Payload.content += @{
            numero_pedido = (Get-Random -Minimum 1000 -Maximum 9999)
            codigo_pedido_omie = (Get-Random -Minimum 100000 -Maximum 999999)
            valor_total = 1250.00
            status = "F"
        }
    }
}

Write-Host "--- Generated Payload for $Event ---" -ForegroundColor Cyan
$Payload | ConvertTo-Json -Depth 10
Write-Host "------------------------------------" -ForegroundColor Cyan
