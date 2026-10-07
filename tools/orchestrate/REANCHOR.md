# Re-anchor the agent branches after the 2026-10-05 rewrite

One-time host steps before either agent loop is relaunched. `main` was rewritten by filter-branch on 2026-10-05, so
`agent/terrarium` and `agent/sundial` share only merge-base 6c63f35 with it (482 and 484 commits ahead).
`agent-loop.mjs` now runs `divergence.mjs` before `git merge --no-edit main` and exits when a worktree is more than
`MAX_AHEAD` (100) commits ahead. The loops are stopped; a relaunch needs a Unity licence anyway.

Commits are cited by their original agent sha or through `git cherry`, never by shas from the porting branch.

1. Confirm that no `agent-loop` process and no Unity editor is running on either worktree
   (`C:/Users/kazda/kiro/gvr-terrarium`, `C:/Users/kazda/kiro/gvr-sundial`).
2. Tag the old tips, from any checkout of this repo:

   ```
   git tag pre-rewrite/agent-terrarium 7efeff1
   git tag pre-rewrite/agent-sundial e8e7e6c
   ```

3. Terrarium. The agent's two commits were ported to `main` by content. Check it first:

   ```
   git cherry -v main 7efeff1 8688f79
   ```

   Both lines must start with `-`. Then, in `gvr-terrarium`:

   ```
   git checkout -B agent/terrarium main
   ```

   The worktree's uncommitted T-TER-034 edits carry across: `JarRitualController.cs`, the splash lines in
   `ProjectSettings.asset`, and three render-pipeline `.asset` files. They carry across because those files are
   identical at 7efeff1 and on `main` after the port. If git refuses with "would be overwritten", stop; nothing is lost.

4. Sundial. T-SUN-053 is unverified art and was not ported. In `gvr-sundial`:

   ```
   git checkout -B agent/sundial main
   git cherry-pick -x 00b92d2 f3340bf 146a176 e8e7e6c
   ```

   T-SUN-053 resumes on these commits and reaches `main` only through the host's verification. Its untracked evidence
   stays on disk.

5. Check each worktree, then relaunch with `relaunch.sh`:

   ```
   node tools/orchestrate/divergence.mjs --worktree C:/Users/kazda/kiro/gvr-terrarium   # expect 0
   node tools/orchestrate/divergence.mjs --worktree C:/Users/kazda/kiro/gvr-sundial     # expect 4
   ```

   Exit 0 means under the limit; exit 3 means over it and the loop will refuse to start a task.
