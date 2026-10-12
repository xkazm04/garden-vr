$ErrorActionPreference = 'Stop'
$repo = 'C:/Users/kazda/kiro/gvr-sundial'
$runDir = "$repo/orchestration/runs/sundial/T-SUN-070"
$unity = 'C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe'
$summary = @()
$runs = @(1..10 | ForEach-Object { 'coach-{0:00}' -f $_ }) + @('playmode-full')
foreach ($name in $runs) {
    $active = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" |
        Where-Object { $_.CommandLine -match 'gvr-sundial[/\\]apps[/\\]sundial' }
    if ($active) { throw 'Another Unity instance is using this project.' }
    $filter = if ($name -like 'coach-*') { ' -testFilter GardenVR.Sundial.Tests.PlayMode.CoachTests' } else { '' }
    Write-Output "Starting $name"
    $process = Start-Process -Wait -PassThru -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -projectPath $repo/apps/sundial -runTests -testPlatform PlayMode$filter -testResults $runDir/$name.xml -logFile $runDir/$name.log"
    $log = Get-Content -LiteralPath "$runDir/$name.log"
    $log | Select-String 'Test run completed\.|error CS\d+|Aborting batchmode|Fatal error' | ForEach-Object { $_.Line }
    if (!(Test-Path -LiteralPath "$runDir/$name.xml")) { throw "$name produced no XML; exit $($process.ExitCode)" }
    [xml]$xml = Get-Content -LiteralPath "$runDir/$name.xml"
    $result = $xml.'test-run'
    $relay = $xml.SelectSingleNode("//test-case[@name='Relay_NeverAnsweringSocket_IsAbortedAfterFlowBudget']")
    $line = "$name exit=$($process.ExitCode) result=$($result.result) total=$($result.total) passed=$($result.passed) failed=$($result.failed) skipped=$($result.skipped) relay=$($relay.result) relaySeconds=$($relay.duration)"
    $summary += $line
    $summary | Set-Content -LiteralPath "$runDir/verification.txt"
    Write-Output $line
    if ($process.ExitCode -ne 0 -or $result.result -ne 'Passed' -or $relay.result -ne 'Passed' -or ($name -like 'coach-*' -and $result.total -ne '8')) {
        $xml.SelectNodes('//failure') | ForEach-Object { Write-Output $_.InnerText }
        throw "$name failed"
    }
}
