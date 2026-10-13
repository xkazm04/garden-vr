# Cue probe for a built Windows player (card: built players are silent).
# Optionally builds the development player, launches it for -Seconds, kills it, and reports
#   placeholders = number of "PLACEHOLDER cue" lines in the player's own log
#   cuelines     = number of lines in the cue log (one per cue that actually started)
# Exit code 0 when the probe ran; -ExpectZeroPlaceholders makes a nonzero placeholder count exit 1.
param(
    [string]$Exe = "",
    [string]$OutDir = "C:/t/logs-audio",
    [int]$Seconds = 25,
    [string]$Build = "",   # "sundial" to build the development player first
    [switch]$ExpectZeroPlaceholders
)
$ErrorActionPreference = "Stop"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
New-Item -ItemType Directory -Force $OutDir | Out-Null
$unity = "C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe"
if ($Build -ne "") {
    $p = Start-Process -PassThru -Wait -FilePath $unity -ArgumentList "-batchmode","-quit","-projectPath","$repo/apps/$Build","-executeMethod","GardenVR.Sundial.Editor.Build.WindowsDevelopment","-logFile","$OutDir/build-$Build.log"
    if ($p.ExitCode -ne 0) { Write-Output "build failed exit $($p.ExitCode)"; exit 2 }
    if ($Exe -eq "") { $Exe = "$repo/apps/$Build/Build/$Build-development/" + (Get-Culture).TextInfo.ToTitleCase($Build) + ".exe" }
}
if (-not (Test-Path $Exe)) { Write-Output "no player at $Exe"; exit 2 }
$playerLog = "$OutDir/player.log"; $cueLog = "$OutDir/cues.jsonl"
Remove-Item $playerLog, $cueLog -ErrorAction SilentlyContinue
$env:GARDEN_CUE_LOG = $cueLog
$proc = Start-Process -PassThru -FilePath $Exe -ArgumentList "-screen-fullscreen","0","-screen-width","800","-screen-height","600","-logFile",$playerLog
Start-Sleep -Seconds $Seconds
if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
$ph = 0; if (Test-Path $playerLog) { $ph = @(Select-String -Path $playerLog -Pattern "PLACEHOLDER cue" -SimpleMatch).Count }
$cl = 0; if (Test-Path $cueLog) { $cl = @(Get-Content $cueLog | Where-Object { $_.Trim() -ne "" }).Count }
Write-Output "placeholders=$ph cuelines=$cl"
if ($ExpectZeroPlaceholders -and $ph -ne 0) { exit 1 }
exit 0
