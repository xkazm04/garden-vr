#!/usr/bin/env bash
# Swap an agent loop to the current code without killing its running task:
# drop a STOP file, wait for the old loop to exit after its task, then start a fresh detached loop.
#   bash tools/orchestrate/relaunch.sh terrarium
set -u
app=$1; root=/c/Users/kazda/kiro/garden-vr; pidf=$root/orchestration/runs/$app/loop.pid
touch $root/orchestration/STOP-$app
pid=$(tr -d '\r\n ' < $pidf)
while powershell -NoProfile -Command "if (Get-Process -Id $pid -ErrorAction SilentlyContinue) { exit 0 } else { exit 1 }"; do sleep 30; done
rm -f $root/orchestration/STOP-$app
powershell -NoProfile -Command "\$p = Start-Process -FilePath node -ArgumentList 'tools/orchestrate/agent-loop.mjs','--app','$app','--worktree','C:/Users/kazda/kiro/gvr-$app','--engine','claude','--claude-model','claude-sonnet-5-5','--claude-effort','high' -WorkingDirectory 'C:/Users/kazda/kiro/garden-vr' -WindowStyle Hidden -RedirectStandardOutput 'C:/Users/kazda/kiro/garden-vr/orchestration/runs/$app/loop.out' -RedirectStandardError 'C:/Users/kazda/kiro/garden-vr/orchestration/runs/$app/loop.err' -PassThru; \$p.Id" > $pidf
echo "$app relaunched as pid $(cat $pidf)"
