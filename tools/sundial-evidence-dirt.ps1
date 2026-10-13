# Instrument: how many tracked evidence files does one full Sundial PlayMode run dirty?
# Resets orchestration/runs, runs the PlayMode suite in the foreground, prints the count of
# `git status --porcelain -- orchestration/runs` lines. Set GARDENVR_EVIDENCE_DIR=1 to measure the opt-in path.
param([string]$Out = "C:/t/logs-evidence", [string]$Unity = "C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe")
$root = (git rev-parse --show-toplevel)
git -C $root checkout -- orchestration/runs
New-Item -ItemType Directory -Force $Out | Out-Null
$p = Start-Process -PassThru -Wait -FilePath $Unity -ArgumentList "-batchmode","-projectPath","$root/apps/sundial","-runTests","-testPlatform","PlayMode","-testResults","$Out/dirt-play.xml","-logFile","$Out/dirt-play.log"
$n = @(git -C $root status --porcelain -- orchestration/runs).Count
"unity_exit=$($p.ExitCode) dirty_evidence_files=$n"
