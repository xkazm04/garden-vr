$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot/../../../..").Path
$unity = 'C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe'
foreach ($phase in @('EditMode', 'PlayMode')) {
    $name = $phase.ToLowerInvariant()
    $graphics = if ($phase -eq 'EditMode') { '-nographics' } else { '' }
    Start-Process -Wait -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode $graphics -projectPath $repo/apps/sundial -runTests -testPlatform $phase -testResults $PSScriptRoot/$name.xml -logFile $PSScriptRoot/$name-final.log"
    $log = Get-Content "$PSScriptRoot/$name-final.log" -Raw
    $log -split "`n" | Select-String 'error CS|Shader error|Test run completed'
    [xml]$results = Get-Content "$PSScriptRoot/$name.xml"
    $results.'test-run' | Select-Object result,total,passed,failed
    if ([int]$results.'test-run'.total -le 0 -or [int]$results.'test-run'.failed -ne 0) {
        $results.SelectNodes('//test-case[@result="Failed"]') | ForEach-Object { $_.name; $_.failure.InnerText }
        throw "$phase failed"
    }
}
Start-Process -Wait -WindowStyle Hidden -FilePath $unity -ArgumentList "-batchmode -nographics -quit -projectPath $repo/apps/sundial -executeMethod GardenVR.Sundial.Editor.Build.Windows -logFile $PSScriptRoot/build-final.log"
$buildLog = Get-Content "$PSScriptRoot/build-final.log" -Raw
$buildLog -split "`n" | Select-String 'error CS|Shader error|Build Succeeded|Build Failed'
if ($buildLog -notmatch 'Build Succeeded') { throw 'Windows build did not succeed' }
