# Security policy

Garden VR is a pre-release project: two PC-first Unity apps and the tooling that builds them. There are no published
releases yet, so a fix lands on `main` and there is no older version to patch.

## Reporting a vulnerability

Report it privately through GitHub: on this repository's **Security** tab, choose **Report a vulnerability**. The
report reaches the owner only. Please do not open a public issue or pull request for a security problem.

Include what you found, where (file and line or commit), how to reproduce it, and what an attacker could do with it.
A leaked credential counts: say where you saw it, and do not use it.

What to expect:
- an acknowledgement within 7 days;
- an assessment, and a fix or a mitigation plan, within 30 days for anything confirmed;
- credit in the fix commit if you want it.

## What is in scope

- Credentials. API keys (ElevenLabs, Tripo) live only in a git-ignored `.env`; `.env.example` shows the variable
  names with empty values. A key committed anywhere in history is in scope.
- The agent tooling in `tools/orchestrate/` and `tools/guard/`. AI agents commit to their own branches all day, so a way
  to bypass the guard, to reach a credential, or to get work onto `main` without the host's verification
  (`docs/AI_HARNESS.md`, Definition of Done) is in scope. Agents hold no credential that can push.
- The paid-service guards, for example `tools/audio/elevenlabs.mjs`, which enforces a credit reserve.
- The CI workflow in `.github/workflows/` and the dependencies Dependabot watches (`.github/dependabot.yml`).

Out of scope: the Unity editor and engine themselves, the third-party services' own platforms, and anything that
needs physical access to the owner's machine.

## How the repository defends itself

- The guard (`tools/guard/check.mjs`) runs on every agent commit, on host and human commits once the hooks are
  installed (`node tools/guard/install.mjs`), and in CI on every push and pull request. It refuses a committed `.env`
  and Unity build output (rule A1).
- CI runs with a read-only token (`permissions: contents: read`).
- A dependency or version change is owner-reserved (`docs/PLAN.md`, section 5, merge policy). Dependabot proposes, and
  `.github/CODEOWNERS` routes those proposals to the owner.
