$ErrorActionPreference = 'Stop'
$taskRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskDll = Join-Path $taskRoot 'src/AgenticSystem.Api/bin/Release/net10.0/AgenticSystem.Api.dll'
$taskListener = Get-NetTCPConnection -LocalPort 5188 -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
if (!$taskListener) { Write-Output 'API de validação já está parada.'; exit 0 }
$taskProcess = Get-CimInstance Win32_Process -Filter "ProcessId = $($taskListener.OwningProcess)"
if ($taskProcess.Name -ne 'dotnet.exe' -or !$taskProcess.CommandLine.Contains($taskDll)) {
    throw 'Porta 5188 pertence a outro processo; parada recusada.'
}
Stop-Process -Id $taskProcess.ProcessId
Write-Output 'API isolada parada; serviços e dados preservados.'
