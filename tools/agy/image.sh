#!/usr/bin/env bash
# Generate (or edit) one image with the Antigravity CLI's built-in image tool (Gemini / Nano Banana), headless.
# Use when your own image_gen is rate-limited or quota-exhausted.
#   bash tools/agy/image.sh "<prompt>" <out.png> [<input image to edit>]
# Writes <out.png> plus <out.png>.prompt.txt (provenance sidecar, required by AGENTS.md). Exit 1 if no file appeared.
set -u
prompt="$1"; out="$2"; src="${3:-}"
agy="${AGY:-$LOCALAPPDATA/agy/bin/agy.exe}"
mkdir -p "$(dirname "$out")"
if [ -n "$src" ]; then ask="Edit the image at $src with your image tool: $prompt. Save the result as $out (PNG). Reply with the saved path only."
else ask="Generate one image with your image tool: $prompt. Save it as $out (PNG). Reply with the saved path only."; fi
"$agy" -p "$ask" --model "${AGY_MODEL:-gemini-3.8-flash-high}" --output-format json > "$out.agy.json" 2>/dev/null
printf 'generator: agy (Antigravity CLI image tool)\nmodel: %s\nsource: %s\nprompt: %s\n' "${AGY_MODEL:-gemini-3.8-flash-high}" "${src:-none}" "$prompt" > "$out.prompt.txt"
rm -f "$out.agy.json"
[ -s "$out" ] && echo "ok $out" || { echo "agy produced no file at $out" >&2; exit 1; }
