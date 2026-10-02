# 0003 - Shared UPM packages, one branch and worktree per agent

Date: 2026-10-02. Status: accepted.

**Decision.** Code and assets both apps use live in `shared/packages/com.gardenvr.*` (local UPM, referenced by
`file:` paths) and `shared/assets/`; app-specific work lives in `apps/<app>/`. Each Grok agent works in its own git
worktree on `agent/<app>`; only the host merges into `main`, hourly, after checking reports and evidence. Each shared
package has one owning agent (see `docs/PLAN.md`); the other agent consumes and may propose changes in its report.

**Why.** Two agents committing to one checkout sweeps each other's work (measured repeatedly in the Personas repo).
Worktrees give physical isolation; single ownership keeps shared packages from diverging.
