#!/usr/bin/env bash
# Revert what an Editor run rewrites on its own (runtime material values, texture metas, the scene, ProjectSettings).
# Usage: bash tools/clean_unity_dirt.sh [path-to-keep ...]
cd "$(dirname "$0")/.." || exit 1
keep=" $* "
for f in $(git diff --name-only -- apps/terrarium/Assets/Art/Materials apps/terrarium/Assets/Art/Textures apps/terrarium/Assets/Scenes apps/terrarium/ProjectSettings apps/terrarium/Packages); do
  case "$keep" in *" $f "*) continue;; esac
  git checkout -- "$f"
done
