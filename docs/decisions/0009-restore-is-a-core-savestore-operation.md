# 0009 - Restore is a core SaveStore operation

Date: 2026-10-07. Status: accepted (App Master, 2026-10-07).

Source: `docs/architecture/review-2026-10.md` section 6, candidate 3 (line 448), and commit cf379cd.

**Constraint.** `AGENTS.md` rule 4: rules live in core, pixels in the app. Both apps restored a backup with their own
code: Sundial `TryRestoreBackup` (`apps/sundial/Assets/Scripts/Tend/SundialService.cs:400-410`) copies a backup over the
live file without parsing it, and Terrarium `GardenService.FirstBackup`
(`apps/terrarium/Assets/Scripts/Ritual/GardenService.cs:347-356`) picks the first backup that exists. The review lists the unreadable
backup that loops forever as its finding 2 (`docs/architecture/review-2026-10.md:135`).

**Decision.** `SaveStore<T>.Restore()` (`shared/packages/com.gardenvr.core/Runtime/Common/SaveStore.cs:255`) merged as
cf379cd, under 0004, on `dotnet test shared/core-dotnet` alone (239 to 251 tests).

- It tries `save.prev1.json`, then `save.prev2.json`, then `save.snapshot.json`, through Load's own read path
  (`TryReadCandidate`, `SaveStore.cs:338`). It skips a newer schema and migrates an older one in memory.
- It moves a damaged live file to `save.damaged.json`, or to the next free `save.damaged.N.json`, and never overwrites
  damaged bytes (`FreeDamagedName`, `SaveStore.cs:353`).
- It never rotates prev1 or prev2 and never touches the snapshot.
- It returns Failed `live-readable` when `save.json` reads. That includes a newer-schema `save.json`, which Load opens
  ReadOnly (`SaveStore.cs:191-206`). The App Master decided this on 2026-10-07: a newer build's file is the newest record
  of the user's days, and an older backup written over it would lose them.
- It returns Failed `restore` when no candidate reads, and `restore-write` on an IO failure during the install.
- `LoadResult.RestoredFrom` (`SaveStore.cs:19`) names the file the document came from and is null on every Load.
- `ISaveStore<T>` (`SaveStore.cs:22-26`) is unchanged.

**Alternatives that lost.**

- Keeping a copy per app: two copies of a data-path rule, and the Sundial copy trusts an unparsed file.
- Letting Restore replace a newer-schema `save.json` with an older backup: it would lose the user's newest days.

**Consequences.** Step B waits for a Unity session (0004). Step B is `SundialService.cs` at 400-410,
`FirstRunWizard.cs` at 218-225 and `GardenService.cs` at 299-356, adopting Restore and dropping `FirstBackup`. Until then
the app restore paths are frozen and no app calls Restore.
