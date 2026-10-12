# T-SUN-070: Deflake the relay flow-budget test

Status: complete

## Cause and evidence

The task's proposed 4-second-budget explanation does not match this checkout or the original test. `git blame` attributes `_config.BudgetSeconds = 0.5f` to the original B4 commit `94f2fc96`. The production default remains 4 seconds, but this test was already using half a second.

Read `CoachTests`, `SundialCoach`, `RelayCoachClient`, `CoachFlow`, and the local T-SUN-062 `playmode-retry.log`. That log ends with:

```text
Test run completed. Exiting with code 2 (Failed). One or more tests failed.
```

The matching `../T-SUN-062/playmode-retry.xml` records the relay case as failed after **5.523058 seconds**:

```text
Task did not complete within 5 seconds
Expected: True
But was: False
CoachTests+<Complete>d__12.MoveNext ... CoachTests.cs:152
```

The next unchanged run, `../T-SUN-062/playmode.xml`, passed the case in **0.518074 seconds**. Contrary to the task's wording, `playmode-retry.xml` is the failed run, not the green retry.

The fixture starts the flow's wall-clock timer immediately after dispatching the web request, without waiting for the loopback server to receive it. If cancellation wins before connection, the client correctly aborts and the flow returns a scripted timeout, but the fixture unconditionally awaits a server task that is still accepting a connection. A listening socket is not proof of an accepted, unanswered request. The server's async operations also resume through Unity's context, while the flow timeout runs independently on the thread pool. This is a test setup race; increasing the completion deadline does not establish the missing precondition.

The historical duration is consistent with the 0.5-second flow completion followed by the 5-second server wait. The old shared assertion stack does not identify which wait failed or preserve the listener state, so that exact historical interleaving cannot be proved from the retained log. A temporary diagnostic insertion of `Thread.Sleep(750)` immediately after starting the old flow **passed** (1/1, Unity exit 0); it did not reproduce the failure. Its XML is retained as `repro-before.xml`, with its raw local log `repro-before.log`. The diagnostic sleep was removed before acceptance testing.

## Change

Only `apps/sundial/Assets/Tests/PlayMode/CoachTests.cs` changes:

- The server exposes completion of reading the full request. The test establishes an unanswered real `RelayCoachClient` request before starting the 0.5-second flow budget.
- A test-only `InFlightRelay` adapter returns that same request task to `SundialCoach`, verifies the request envelope matches, and forwards the flow's cancellation token. It does not fabricate a relay result or close the server socket. `RelayCoachClient` still performs `UnityWebRequest.Abort()` and disposal on Unity's context.
- The test checks flow timeout, `FromModel == false`, the exact scripted reflection, transport timeout completion, and server-observed socket disconnection before cleanup can close anything. `LogAssert.NoUnexpectedReceived()` explicitly checks all unhandled Unity test logs, including ordinary logs and warnings.
- Every asynchronous phase retains the existing five-second deadline and now names the phase on failure. There are no retries or ignored assertions.

Production code, the default budget, input providers and shared packages are unchanged. No shared compatibility change is needed. No visual change was made, so no new art captures or image ingestion are required.

## Verification commands and results

`verify.ps1` is the exact reproduction script. It starts one foreground Unity process at a time with `Start-Process -Wait`, reads each log before continuing, parses each XML, and stops on any failure. Command:

```powershell
& orchestration/runs/sundial/T-SUN-070/verify.ps1
```

Each coach run uses:

```text
Unity.exe -batchmode -projectPath C:/Users/kazda/kiro/gvr-sundial/apps/sundial -runTests -testPlatform PlayMode -testFilter GardenVR.Sundial.Tests.PlayMode.CoachTests -testResults <run-dir>/coach-NN.xml -logFile <run-dir>/coach-NN.log
```

The full-suite command omits `-testFilter` and writes `playmode-full.xml` / `playmode-full.log`. Unity is `C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe`. The test runner owns exit; `-quit` is not supplied to these asynchronous test runs.

All 11 acceptance processes exited 0 and reported `Test run completed. Exiting with code 0 (Ok). Run completed.`

| Results XML | Passed/total | Failed/skipped | Relay case seconds |
|---|---:|---:|---:|
| [coach-01.xml](coach-01.xml) | 8/8 | 0 | 0.602610 |
| [coach-02.xml](coach-02.xml) | 8/8 | 0 | 0.605534 |
| [coach-03.xml](coach-03.xml) | 8/8 | 0 | 0.613052 |
| [coach-04.xml](coach-04.xml) | 8/8 | 0 | 0.606716 |
| [coach-05.xml](coach-05.xml) | 8/8 | 0 | 0.608101 |
| [coach-06.xml](coach-06.xml) | 8/8 | 0 | 0.599629 |
| [coach-07.xml](coach-07.xml) | 8/8 | 0 | 0.600646 |
| [coach-08.xml](coach-08.xml) | 8/8 | 0 | 0.606533 |
| [coach-09.xml](coach-09.xml) | 8/8 | 0 | 0.621935 |
| [coach-10.xml](coach-10.xml) | 8/8 | 0 | 0.614939 |
| [playmode-full.xml](playmode-full.xml) | 94/94 | 0 | 0.602012 |

`verification.txt` contains the exact parsed summary lines. Raw Unity `.log` files remain local and untracked. Some editor logs contain native `Curl error 42: Callback aborted`; the Unity test log assertion passes, and this is not a claim that the entire editor log is empty. No transcript or install key logging was added.

## Failures, scope and next work

Acceptance passed without retries: ten consecutive coach runs (80/80 individual tests), then the full Sundial PlayMode suite (94/94), with no failed or skipped tests. The exploratory old-test frame-stall probe also passed; it is not counted toward acceptance. Code commit: `38a413c`.

The full suite regenerated 59 tracked materials and older capture artifacts. Those incidental outputs were restored to their pre-run committed versions; `restored-generated-files.txt` lists the exact paths. No new or changed PNG is committed. `git show --format= --check 38a413c` passed for the code commit. The staged evidence check reports one trailing space inside the full XML's captured `[Review] hints` output; the generated XML is retained verbatim. The pre-existing untracked scratch files and plant candidates remain untouched.

No follow-up is required for acceptance. If a future timeout occurs, its new phase-specific message distinguishes request setup, flow fallback, relay abort and server disconnect; the old historical log cannot identify that distinction.

No build or .NET suite was run: this change is confined to a Unity test fixture, and the task requires the repeated coach tests and full PlayMode suite. No task file, external worktree, secrets, production settings or pre-existing scratch/art files were edited. No push was performed.
