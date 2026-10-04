#!/usr/bin/env bash
# Revert what an Editor run rewrites on its own (runtime material values, texture metas, the scene, ProjectSettings).
# Jar_Glass.mat is restored to HEAD too: a capture leaves its runtime keywords, _Fog and _S2Cfg in it.
cd "$(dirname "$0")/.." || exit 1
for f in $(git diff --name-only -- apps/terrarium/Assets/Art/Materials apps/terrarium/Assets/Art/Textures apps/terrarium/Assets/Scenes apps/terrarium/ProjectSettings apps/terrarium/Packages); do
  git checkout -- "$f"
done
