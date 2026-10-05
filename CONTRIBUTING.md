# Contributing to Garden VR

Most changes here are made by AI agents, verified by a host session and reviewed by the owner. A person working by
hand is held to exactly the same rules. This page says where those rules live and what enforces them. It points at the
canonical files instead of copying them, so it cannot drift from them.

## Before you write code

- Read [`AGENTS.md`](AGENTS.md). Its rules are not negotiable, and its `## Commands` block is the canonical command
  list (`.ai/manifest.yaml` names it canonical; [`CLAUDE.md`](CLAUDE.md) is the Claude adapter).
- Read [`docs/PLAN.md`](docs/PLAN.md) and your app's plan in `docs/plans/`. The contest facts the plan binds on are in
  [`docs/knowledge/contest-record.md`](docs/knowledge/contest-record.md).
- Check path ownership (PLAN section 5). Every path has one owner, and `docs/`, `tools/orchestrate/`, `tools/guard/`,
  `AGENTS.md` and the `.csproj` files belong to the host.

## Development setup

- Unity 6000.6.4f1 at the path in the `AGENTS.md` Commands block, the .NET 9 SDK for `shared/core-dotnet`, and Node 20
  or newer for `tools/`.
- Install the commit hooks once per clone. `core.hooksPath` is shared by the main checkout and every agent worktree,
  so this is the owner's call ([`tools/guard/README.md`](tools/guard/README.md), "Installing the hooks"):

  ```powershell
  git config core.hooksPath tools/guard/hooks
  ```

  Until it is set, `node tools/guard/check.mjs --all` fails on `main` with rule H1.

## The verification gate

The same checks run in three places, so skipping one of them does not skip the check:

| Where | What runs | What happens when it fails |
|---|---|---|
| Your commit | `tools/guard/hooks/pre-commit` runs the guard on the staged files | the commit is refused |
| Every push and pull request | [`.github/workflows/guard.yml`](.github/workflows/guard.yml): the guard's self-test, `--all` and `--range` over the pushed commits, then `dotnet test shared/core-dotnet` on Windows | the check turns red on GitHub |
| Host verification of an agent task | the Definition of Done in [`docs/AI_HARNESS.md`](docs/AI_HARNESS.md) | the task is rejected, and the verdict is recorded in `docs/knowledge/verifications.jsonl` |

- `dotnet test shared/core-dotnet` also runs the analyzer ratchet. The rules promoted in
  `shared/core-dotnet/Directory.Build.props` fail the build. Guard rule B1 stops a rule from being demoted.
- What each guard rule protects is in [`tools/guard/README.md`](tools/guard/README.md).
- Do not bypass a hook with `--no-verify`. The CI `--range` run reads the same commits anyway.
- If a rule fires on correct code, fix the rule and add a `--self-test` fixture for that case.
- Removing a CI check, or skipping or loosening a test or gate, is reserved for the owner (PLAN section 5, merge
  policy). Guard rule C1 fails when the workflow stops running one of its checks.

## AI-assisted contributions

- Agents work one task file at a time, cut from `tools/orchestrate/prompts/task-template.md`. The task's `touches:`
  bounds its scope.
- An agent's `REPORT.md` is a self-report. It never certifies anything. A task is done only when the host has re-run
  the Definition of Done and committed a verdict line to the ledger, which guard rules V1 and V2 check.
- A pull request uses the template in `.github/pull_request_template.md`, which carries the same checklist.
- Prompts in `tools/orchestrate/prompts/` are versioned. A change to one must raise its `prompt_version` (guard rule
  P1).

## Licence

No licence has been chosen yet, so all rights are reserved and there is no contributor agreement to sign.
