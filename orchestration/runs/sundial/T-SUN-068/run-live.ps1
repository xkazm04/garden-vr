$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot/../../../..").Path
$env:COACH_BACKEND = 'cli'
$env:COACH_CLAUDE_PATH = (Get-Command claude.exe).Source
if (Get-NetTCPConnection -LocalPort 8787 -State Listen -ErrorAction SilentlyContinue) {
    throw 'Port 8787 is already owned; do not replace another relay.'
}
$relay = Start-Process -PassThru -WindowStyle Hidden -FilePath (Get-Command node.exe).Source `
    -WorkingDirectory "$repo/tools/relay" -ArgumentList 'server.mjs' `
    -RedirectStandardOutput "$PSScriptRoot/relay.log" -RedirectStandardError "$PSScriptRoot/relay-stderr.log"
try {
    $health = $null
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        try { $health = Invoke-RestMethod 'http://localhost:8787/healthz'; break } catch { Start-Sleep -Milliseconds 250 }
    }
    if (!$health.ok -or $health.backend -ne 'cli') { throw 'Relay health is not backend=cli' }
    $health | ConvertTo-Json -Compress | Set-Content "$PSScriptRoot/health.json"
    $player = Start-Process -Wait -PassThru -FilePath "$repo/apps/sundial/Build/sundial-development/Sundial.exe" `
        -ArgumentList "--relay-live-check $PSScriptRoot -screen-fullscreen 0 -screen-width 1600 -screen-height 1000 -logFile $PSScriptRoot/player.log"
    Write-Output "PlayerExit=$($player.ExitCode)"
    Select-String -Path "$PSScriptRoot/player.log" -Pattern '\[RelayLive\]' | ForEach-Object { $_.Line }
    if ($player.ExitCode -ne 0) { throw "Player failed: $($player.ExitCode)" }
} finally {
    if (!$relay.HasExited) { Stop-Process -Id $relay.Id; $relay.WaitForExit() }
    Write-Output "RelayStopped=$($relay.HasExited)"
}
