#!/usr/bin/env bash
# Swap an agent loop to the current code without killing its running task:
# drop a STOP file, wait for the old loop to exit after its task, then start a fresh detached loop.
#   bash tools/orchestrate/relaunch.sh sundial            (Codex, GPT 6 Astra; the owner's choice 2026-10-12)
#   ENGINE=claude bash tools/orchestrate/relaunch.sh sundial
# With no loop.pid (first launch) it starts the loop straight away.
set -u
app=$1; engine=${ENGINE:-codex}; root=/c/Users/kazda/kiro/garden-vr; pidf=$root/orchestration/runs/$app/loop.pid
touch $root/orchestration/STOP-$app
pid=$(tr -d '\r\n ' < $pidf 2>/dev/null); pid=${pid:-0}
# Wait only on a live agent-loop: a stale loop.pid can name an unrelated process days later.
while [ "$pid" != 0 ] && powershell -NoProfile -Command "\$p = Get-CimInstance Win32_Process -Filter 'ProcessId=$pid'; if (\$p -and \$p.CommandLine -match 'agent-loop') { exit 0 } else { exit 1 }"; do sleep 30; done
rm -f $root/orchestration/STOP-$app
powershell -NoProfile -Command "\$p = Start-Process -FilePath node -ArgumentList 'tools/orchestrate/agent-loop.mjs','--app','$app','--worktree','C:/Users/kazda/kiro/gvr-$app','--engine','$engine','--codex-model','gpt-6-astra','--codex-effort','high','--claude-model','claude-sonnet-5-5','--claude-effort','high','--timeout-min','240' -WorkingDirectory 'C:/Users/kazda/kiro/garden-vr' -WindowStyle Hidden -RedirectStandardOutput 'C:/Users/kazda/kiro/garden-vr/orchestration/runs/$app/loop.out' -RedirectStandardError 'C:/Users/kazda/kiro/garden-vr/orchestration/runs/$app/loop.err' -PassThru; \$p.Id" > $pidf
echo "$app relaunched as pid $(cat $pidf)"
