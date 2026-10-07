# Stack-grounded opportunity research - Garden VR (2026-10-07)

Charter `stack-grounded-opportunity-research`, written 2026-10-07 by a headless builder. It covers the stack the repo
actually pins (Unity 6000.6.4f1, URP 17.6.0, Input System 1.20.0, Test Framework as resolved) and the Quest packages
Phase 2 (10-24 to 11-11) plans to adopt. It does not repeat `docs/research/asset-pipeline-and-eval.md` (asset
generation, the judge, the fidelity lab).

**Method and limits.** Web access was available (search and page fetch). Every upstream fact below was fetched on
**2026-10-07** from the URL next to it. A fact I could not fetch from a source is marked **[unverified]**; nothing is
stated from memory. Unity was not run: this machine has no Unity licence, so nothing here claims a Unity build, compile
or test passed. Repo citations are `path:line` at commit `e3a8abe`. Paths under `C:/Users/kazda/kiro/personas/.contest/`
are the contest record the plans cite (outside the repo, read only).

**Where this file lives.** `docs/research/` is git-ignored since `e3a8abe` (`.gitignore:63`). The charter asked for a
commit, so this file was force-added. Whether it stays tracked is the host's call.

## The eight findings at a glance

| # | Finding | Lands | Confidence |
|---|---|---|---|
| 1 | The rules allow **one entry per individual**: PLAN D9 (two entries) cannot be done by the owner alone | before the gate 10-23 | high |
| 2 | `activeInputHandler: 0` leaves the PC player with no keyboard or mouse, and OpenXR needs the Input System. Pick **Input System Package (New)**, not "Both", which Android does not support | before R1 10-09, at the latest the gate | high |
| 3 | The URP assets render the gate frames with **HDR on** (both apps) and **Opaque Texture on** (Terrarium). Unity and Meta say to turn both off on Quest, so the gate would certify a look Quest will not show | capture rule before the gate; Quest asset in Phase 2 | medium-high |
| 4 | **PalmOpen "palm toward the head"** is the pose of Meta's reserved system gesture (VRC.Quest.Input.8, required) | plan text before the gate; provider in Phase 2 | high (rule), medium (collision) |
| 5 | Phase 2 package set: **OpenXR 1.18.0 + XR Hands 1.9.0** (not the spike's 1.7.2) + Meta XR Core SDK 207; Operator and Simulator v207 requirements | first Phase 2 task, 10-24 | medium-high |
| 6 | Manifest for the Competition upload: **targetSdkVersion 34 is enforced at upload**, minSdk must be 29-34 (repo: 26 / automatic); fix the package id before the first upload | Phase 2, before the first upload (H1 10-27); hard stop at submission | high |
| 7 | **Application SpaceWarp**: 16 custom shaders, none with the `XRMotionVectors` pass Meta and Unity require. Drop it from Phase 2 unless H2 measures a frame-rate miss | Phase 2 (decide at H2 11-03) | medium |
| 8 | Test Framework is **1.8.0**, not 1.6.0: core package, fixed to the Editor | before the gate (doc fix) | high |

---

## Finding 1 - One entry per individual: PLAN D9 cannot be done by the owner alone

1. **Dependency as found.** PLAN D9 takes "Two separate competition entries" as the default, with "verify" against the
   rules (`docs/PLAN.md:283`). Host action "Before 10-23: Verify in the official rules whether one team may submit two
   entries (D9)" (`docs/PLAN.md:295`), and risk "Two entries not allowed | unknown" (`docs/PLAN.md:311`). The owner is
   also "building a second, harder VR entry in parallel"
   (`C:/Users/kazda/kiro/personas/.contest/staging/habit-garden/data/OWNER-INTENT.md:17`; `docs/PLAN.md:307`).
2. **Upstream fact.** Official rules, https://start-developer-competition-26.devpost.com/rules (fetched 2026-10-07),
   Section 2: "Each individual is limited to submitting one (1) Entry in this Contest. For the avoidance of doubt, an
   Entry is submitted either (i) by an individual on their own behalf, or (ii) by a Team Representative on behalf of a
   Team or Organization. Participation as a non-Representative Team member does not constitute submitting an Entry."
   And: "An individual may not both submit an Entry as a solo entrant and serve as a Team Representative, as this would
   result in two Entries being attributed to that individual. Each individual may serve as the Representative of only
   one (1) Team or Organization." Entry period ends "November 18, 2026 at 12:00:00 PM PT". The submission is "An APK
   uploaded to the Meta VR Developer Dashboard under a new release channel named 'Competition'" plus a video "less than
   three (3) minutes" that may show "footage of your Project as viewed on a Meta Quest device or via XR Simulator".
   Judging (Section 6, 25% each) names "strategic use of platform tools" including "gaze interactions, hand tracking,
   passthrough, spatial anchoring", and "High-scoring entries must be performant on Meta Quest hardware (min 60 fps)".
3. **What it changes.** D9's default is not possible for one person. The owner has **one entry in total**, and the
   harder parallel VR entry counts against it too. Two Garden VR entries need a second individual who registers as
   Representative of a team. The owner may be a non-Representative member of that team, but cannot also submit solo or
   represent another team. The rules fetch says only the Representative must meet eligibility, including Start Program
   membership by submission time. Concretely:
   - Rewrite D9 and the risk row (`docs/PLAN.md:283`, `:311`) and record the answer in `docs/decisions/0004-quest-gate.md`.
   - The gate on 10-23 decides "which app is the entry" (plus, optionally, "who represents the second one"), not "which
     apps go to Quest". If only one app can be entered, the Phase 2 schedule (`docs/PLAN.md:63`) needs only one Quest port.
   - The owner must also choose between Garden VR and the parallel VR entry, or find a second Representative for one of them.
   - The deadline is 12:00 PT on 11-18, not end of day. The 11-18 buffer (`docs/PLAN.md:64`) ends at noon Pacific
     (21:00 CET if PT is UTC-8 that day [unverified offset]).
4. **When.** Before the gate, **2026-10-23**. The gate decision depends on it.
5. **Confidence.** High. The fact is quoted from the official rules page, fetched twice.

## Finding 2 - `activeInputHandler: 0`: no keyboard or mouse in the PC player now, no OpenXR input later

1. **Dependency as found.** `activeInputHandler: 0` in `apps/terrarium/ProjectSettings/ProjectSettings.asset:721` and
   `apps/sundial/ProjectSettings/ProjectSettings.asset:721`, unchanged since the scaffold commit `a51a96e`. Input System
   `1.20.0` (`apps/terrarium/Packages/manifest.json:43`, `apps/sundial/Packages/manifest.json:42`). The only device
   reader is the new-API provider: `Keyboard.current` / `Mouse.current`
   (`shared/packages/com.gardenvr.input/Runtime/Providers/KeyboardMouseIntentSource.cs:93-94`). The repo has already
   recorded the failure: "`Keyboard.current` and `Mouse.current` were null in the player (activeInputHandler 0) though
   every editor test passed" (`docs/knowledge/lessons.md:108-111`, T-TER-034). The package README says "Input System
   Package, or Both" (`shared/packages/com.gardenvr.input/README.md:48`), and the lesson says "the Input System (2) or
   Both".
2. **Upstream fact.**
   - Unity 6000.6 manual, Android Player settings,
     https://docs.unity3d.com/6000.6/Documentation/Manual/class-PlayerSettingsAndroid.html (fetched 2026-10-07), Active
     Input Handling: "Input Manager (Old): Use the original Input settings." / "Input System Package (New): Uses the new
     input system package" / "Both: Use both systems. This option isn't supported on Android."
   - OpenXR Plugin 1.18 manual, https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.18/manual/input.html (fetched
     2026-10-07): "Unity requires the use of the Input System package when using OpenXR."
   - XR Hands `MetaAimHand` (the device that carries the Quest aim ray and pinch flags),
     https://docs.unity3d.com/Packages/com.unity.xr.hands@1.3/api/UnityEngine.XR.Hands.MetaAimHand.html (fetched
     2026-10-07): it is "Enabled through Meta Hand Tracking Aim or by enabling hand-tracking in the Oculus plug-in if the
     Input System back-end is enabled."
   - Input System 1.20.1 (2026-10-05) changelog,
     https://docs.unity3d.com/Packages/com.unity.inputsystem@1.20/changelog/CHANGELOG.html (fetched 2026-10-07): editor
     UI fixes only. Nothing in it changes this setting.
   - Which serialized number means which option is **[unverified]**. Only a third-party pull request
     (https://github.com/Lee-GunHo/OutPost/pull/9) maps `1` to "Input System Package (New)". That disagrees with the
     lesson's "(2)". Set the value through the Player Settings UI, not by editing the number.
3. **What it changes.** Gate items U2, U4 and S6 (`docs/PLAN.md:88`, `:90`, `:102`) need a person to play the Windows
   player. With `0`, the player takes no keyboard or mouse input; the scripted-playback tests do not catch this. Phase 2's
   OpenXR provider needs the same switch.
   - Recommendation: set **Input System Package (New)** in both apps, not "Both". "Both" is unsupported on Android, and
     the repo has no legacy `Input.*` call and no `StandaloneInputModule` to keep (grep over `apps/*/Assets` and
     `shared/packages`, 2026-10-07).
   - Fix the "or Both" wording in `shared/packages/com.gardenvr.input/README.md:48` and `docs/knowledge/lessons.md:110`.
   - Add a one-key player smoke test to the gate pack (S5's player-log run is the natural place).
   - ProjectSettings is host-owned, so this is a host change.
4. **When.** Before R1, **2026-10-09** (the owner plays both PC builds). At the latest, before the gate 2026-10-23.
5. **Confidence.** High for the platform facts (fetched) and the failure (recorded in the repo). The numeric mapping is
   unverified, as marked.

## Finding 3 - The gate frames are rendered with settings Quest guidance turns off

1. **Dependency as found.** URP 17.6.0 (builtin, `apps/terrarium/Packages/packages-lock.json:124-127`). Both pipeline
   assets have `m_SupportsHDR: 1`, `m_SupportsTerrainHoles: 1` and `m_MSAA: 4`:
   - `apps/terrarium/Assets/Settings/GardenURP.asset:26`, `:25`, `:28`;
   - `apps/sundial/Assets/Settings/GardenURP.asset:26`, `:25`, `:28`.

   Terrarium also has `m_RequireOpaqueTexture: 1` (`apps/terrarium/Assets/Settings/GardenURP.asset:23`). The jar glass
   PC path samples it (`SampleSceneColor`, `shared/packages/com.gardenvr.fx/Runtime/Shaders/GlassCommon.hlsl:176`,
   `:186`, `:472-474`). A cube-map "Quest path" exists beside it (`GlassCommon.hlsl:11-13`). Nine custom shaders take
   "(HDR)" emission colours (for example `shared/packages/com.gardenvr.fx/Runtime/Shaders/FGlow.shader:10` and
   `apps/terrarium/Assets/Art/Shaders/Moss.shader:14`). Both plans assume the Quest rendering stays "same + multiview,
   fixed foveation" (`docs/plans/terrarium.md:311`).
2. **Upstream fact.**
   - Unity 6000.6 manual, "Optimize for untethered XR devices in URP",
     https://docs.unity3d.com/6000.6/Documentation/Manual/xr-untethered-device-optimization.html (fetched 2026-10-07):
     - "Disable HDR to reduce memory bandwidth and improve performance. Most untethered XR devices don't support HDR
       rendering."
     - Opaque Texture and Depth Texture "cause extra texture copy operations, which requires extra GMEM loads".
     - "A 2X MSAA value provides a good balance between visual quality and performance."
     - It recommends Multi-view, Foveated rendering and Multiview render regions through the OpenXR Plugin.
   - Unity OpenXR: Meta 2.6, https://docs.unity3d.com/Packages/com.unity.xr.meta-openxr@2.6/manual/get-started/graphics-settings.html
     (fetched 2026-10-07): "the default URP settings don't enable optimal Passthrough performance on Quest". Recommended:
     Terrain Holes Disabled, HDR Disabled, Post-processing Disabled, Intermediate Texture Auto.
   - OpenXR 1.17 foveated rendering, https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.17/manual/features/foveatedrendering.html
     (fetched 2026-10-07): the Legacy (Meta) API "does not support foveated rendering when you use intermediate render
     targets"; "use the Unity SRP Foveation API where possible". OpenXR 1.17.0 "Changed the default Foveated Rendering
     Method to the SRP API" (https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.17/changelog/CHANGELOG.html,
     fetched 2026-10-07).
   - Unity 6000.6.0f1 release notes, https://unity.com/releases/editor/whats-new/6000.6.0f1 (fetched 2026-10-07): "Updated
     the default settings for the Meta Quest Build Profile's custom Quality Setting."
3. **What it changes.**
   - The gate's art items A1-A3 (`docs/PLAN.md:108-110`) score PC frames rendered with HDR, and in Terrarium with an
     opaque-copy refraction. The Quest build should turn HDR and the Opaque Texture off. With passthrough, the camera
     clears to alpha 0 (`docs/plans/terrarium.md:306`), and the compositor owns the passthrough pixels
     (`docs/plans/terrarium.md:31`).
   - Inference, not measured: HDR emission above 1.0 and the opaque-copy glass will not reproduce in an HDR-off,
     no-opaque-copy Quest build.
   - Recommendation:
     1. Add a second URP asset (or the Meta Quest build profile's quality level) with HDR off, Opaque Texture off,
        Terrain Holes off and Intermediate Texture Auto.
     2. Capture each gate frame once more with it on PC, so the owner's 10-23 score covers the look Quest can show.
        Terrarium's capture should use the `_S2_CUBE` glass path.
     3. Keep MSAA 4x unless H2 measures otherwise (Meta's best-practice page says 4x, Unity says 2x).
   - On Quest, use the SRP Foveation API, not the Legacy one.
   - Changing the URP asset is a host-owned change; adding a capture is a task for each app.
4. **When.** The capture rule before the gate, **2026-10-23**, so the gate scores the right frames. The Quest asset in
   the Quest phase, before H2 on 11-03 (perf and art in passthrough, `docs/PLAN.md:68`).
5. **Confidence.** Medium-high. The recommendations are fetched. The visual difference is an inference from the shaders,
   not a measurement.

## Finding 4 - PalmOpen "palm toward the head" is Meta's reserved system-gesture pose

1. **Dependency as found.** `PalmOpen` is one of the six intents (`AGENTS.md:16-17`, decision 0002). The Quest mappings:
   - Sundial: "palm toward the head, 0.6 s, not during a pinch" (`docs/plans/sundial.md:127`).
   - Terrarium: "PalmOpen from a palm-toward-head pose held 0.6 s" (`docs/plans/terrarium.md:304`). The same plan's
     intent table says "open palm toward the jar for 0.6 s" (`docs/plans/terrarium.md:130`), so the plan contradicts
     itself.

   Gate S4 tests PalmOpen at any point of a ritual (`docs/PLAN.md:100`).
2. **Upstream fact.**
   - VRC.Quest.Input.8, https://developers.meta.com/vr/resources/vrc-quest-input-8/ (fetched 2026-10-07; page dated
     2024-07-31): "For apps that support hand tracking, the system gesture is reserved, and should not trigger any other
     actions within the app." It is Required for immersive apps. Test: "Raise your hand with an open palm facing the
     headset and perform a pinch". Expected: "No gesture events should be processed while the system gesture is in
     progress."
   - The VRC list, https://developers.meta.com/vr/resources/publish-quest-req/ (fetched 2026-10-07; updated 2026-08-19),
     keeps Input.8 Required.
   - XR Hands `MetaAimFlags.SystemGesture`,
     https://docs.unity3d.com/Packages/com.unity.xr.hands@1.1/api/UnityEngine.XR.Hands.MetaAimFlags.html (fetched
     2026-10-07): "Indicates whether a system gesture is being performed (when the palm of the hand is facing the
     headset)."
3. **What it changes.** Holding a palm toward the head for 0.6 s is how a user starts the system gesture. Sundial would
   dismiss the dial, and Terrarium would pause the ritual, while the user is opening the system menu. The rules do not
   say the Competition upload is VRC-reviewed. But the same behaviour is what judges see as a bug, and the judging
   criteria weigh "reliability". Recommendation:
   - Define the Quest PalmOpen pose as palm facing **away from the face**, toward the object. This is the jar wording at
     `terrarium.md:130`. Make both plans say it.
   - In the Phase 2 `XrHandsIntentSource`, emit **no intent** while `MetaAimFlags.SystemGesture` is set on either hand.
   - The PC binding (hold P) and the intent API do not change.
4. **When.** Plan text before the gate, **2026-10-23** (it costs nothing and stops the PC journey from teaching a pose
   that cannot ship). Provider code in the Quest phase, before H1 on 10-27.
5. **Confidence.** High for the rule and the flag (fetched). Medium for the collision itself: it depends on how the
   provider reads "toward the head", which is not built yet.

## Finding 5 - Phase 2 package set: OpenXR 1.18.0, XR Hands 1.9.0, Meta XR Core 207, and Operator's requirements

1. **Dependency as found.** No XR package in either app manifest (`apps/*/Packages/manifest.json`); `com.unity.modules.xr`
   only (`apps/terrarium/Packages/manifest.json:40`). The plans tell Phase 2 to reuse the round-2/3 spike ("reuse
   `RUNBOOK.md`; do not rediscover", `docs/plans/terrarium.md:312`). That spike pinned, on the same Unity 6000.6.4f1:
   - `"com.meta.xr.sdk.core": "207.0.0"`, `"com.unity.xr.openxr": "1.18.0"`, `"com.unity.xr.hands": "1.7.2"`
     (`C:/Users/kazda/kiro/personas/.contest/arena/habit-garden-r2/entries/claude-claude-opus-5-5_high-v1/spike/unity/Packages/manifest.json:13`,
     `:16`, `:17`);
   - `ProjectVersion.txt` 6000.6.4f1 in the same spike.

   Planned Phase 2 testing: "Meta XR Simulator + Operator MCP" (`docs/plans/terrarium.md:309`,
   `docs/plans/sundial.md:285`).
2. **Upstream fact.**
   - Unity 6000.6 manual, https://docs.unity3d.com/6000.6/Documentation/Manual/com.unity.xr.openxr.html (fetched
     2026-10-07): "Package version 1.18.0 is released for Unity Editor version 6000.6".
   - Unity 6000.6 manual, https://docs.unity3d.com/6000.6/Documentation/Manual/com.unity.xr.hands.html (fetched
     2026-10-07): "Package version 1.9.0 is released for Unity Editor version 6000.6".
   - XR Hands 1.9.0 changelog (2026-08-06), https://docs.unity3d.com/Packages/com.unity.xr.hands@1.9/changelog/CHANGELOG.html
     (fetched 2026-10-07), fixes "The Meta Hand Tracking Aim feature not updating the pose correctly when using the
     OpenXR Plugin (com.unity.xr.openxr) package version 1.16.0 or newer".
   - Meta XR Core SDK 207.0 was released 2026-09-22
     (https://developers.meta.com/horizon/downloads/package/meta-xr-core-sdk/, fetched 2026-10-07).
   - Meta's Unity setup page, https://developers.meta.com/vr/documentation/unity/unity-project-setup/ (fetched
     2026-10-07; updated 2026-09-09): minimum Unity "6000.0.66f2 or later", recommended 6.1 or later. It recommends
     OpenXR Plugin 1.15.1 and says "The Oculus XR Plugin is deprecated and scheduled for removal."
   - Meta XR Operator, https://developers.meta.com/horizon/documentation/unity/meta-xr-operator/getting-started/
     (fetched 2026-10-07; updated 2026-09-09):
     - it is "an experimental component of the Meta XR Core SDK";
     - it needs Unity 6000.0.x or later, Meta XR Core SDK v207 or later, and OpenXR Plugin 1.17.0 or later;
     - "The agent can reach your app only while it runs in Play mode (or while it runs on a connected headset)".
   - Meta XR Simulator 207.0 (2026-09-24), https://developers.meta.com/vr/downloads/package/meta-xr-simulator-windows/
     (fetched 2026-10-07):
     - "Look and Pinch is now available as a fully supported input mode";
     - camera-driven hand tracking with per-finger pinch strength;
     - "record the simulator's composited output to an MP4";
     - known issue: "Enabling XROperator input blocks subsequent mouse and keyboard [input] requiring a restart".
3. **What it changes.**
   - The first Phase 2 manifest change (owner-reserved, `docs/PLAN.md:181`) should pin OpenXR 1.18.0 and **XR Hands
     1.9.0**, not the spike's 1.7.2. Sundial's Quest `Look` is a hand ray (`docs/plans/sundial.md:278`). That ray is the
     Meta aim pose, which 1.7.2 gets wrong on OpenXR 1.18.
   - Keep Meta XR Core 207: Operator needs it, and Meta's recommended OpenXR 1.15.1 is too old for Operator (1.17.0 or
     later).
   - Operator e2e needs the Editor in Play mode, and so a Unity licence on the machine that runs it. That is not this
     machine today.
   - Opportunity: the Simulator's Look and Pinch mode matches Sundial's core gesture, and its webcam pinch strength can
     drive Terrarium's PinchHold. Its MP4 recorder can make the demo video, which the rules accept "via XR Simulator"
     (Finding 1). That is a fallback if H3 (11-10) slips.
4. **When.** The first Quest-phase task, **2026-10-24** (before H1 on 10-27).
5. **Confidence.** Medium-high. The versions and fixes are fetched. That the spike's set builds on 6000.6.4f1 is the
   contest's record, not something I ran.

## Finding 6 - The Competition upload: targetSdk 34 is enforced at upload, minSdk 26 is out of range, package id unset

1. **Dependency as found.** Both apps:
   - `AndroidMinSdkVersion: 26` (`apps/terrarium/ProjectSettings/ProjectSettings.asset:180`, same line in sundial);
   - `AndroidTargetSdkVersion: 0` (`:181`), which I take to be the "Automatic" setting **[unverified mapping]**;
   - `companyName: DefaultCompany` (`:15`) and `applicationIdentifier: {}` (`:172`);
   - `scriptingBackend: {}` (`:627`);
   - `AndroidTargetArchitectures: 2` (`:272`, ARM64 **[unverified mapping]**).

   The round-2 spike used `AndroidMinSdkVersion: 32` (its `ProjectSettings.asset:182`). The plan's Phase 2 check is
   "`aapt dump badging`" (`docs/plans/terrarium.md:310`).
2. **Upstream fact.**
   - Meta, "Application Manifests for Release Builds", https://developers.meta.com/horizon/resources/publish-mobile-manifest/
     (fetched 2026-10-07; updated 2026-09-30):
     - recommended minSdkVersion 32, targetSdkVersion 34, compileSdkVersion 34;
     - allowed minSdkVersion 29-34, targetSdkVersion 32-34 for immersive apps;
     - "apps created since March 1, 2026, must set their targetSdkVersion to 34";
     - "installLocation must be set to auto"; "android:debuggable must be set to false, or unset";
     - devices declared via `com.oculus.supportedDevices`;
     - non-conforming manifests fail VRC.Quest.Packaging.1 / .4.
   - Meta blog, https://developers.meta.com/horizon/blog/meta-quest-apps-android-14-march-1/ (fetched 2026-10-07; updated
     2026-02-06): "API level 34 will be enforced during binary upload, meaning you will not be able to upload binaries
     with a lower targetSdkVersion level for apps created after March 1, 2026."
   - Unity 6000.6 manual, https://docs.unity3d.com/6000.6/Documentation/Manual/android-requirements-and-compatibility.html
     (fetched 2026-10-07): Unity supports API 26 and later, and projects can target API levels 35 and 36. So
     "automatic" is not guaranteed to land on 34.
   - Meta's Unity publishing page, https://developers.meta.com/vr/documentation/unity/unity-prepare-for-publish/ (fetched
     2026-10-07; updated 2026-04-27):
     - it asks for IL2CPP, ARM64 and Minimum API Level 29 or higher;
     - it still says "Target API Level: Automatic (highest installed)", which conflicts with the manifest page;
     - the package name "must be unique within the Meta Quest ecosystem".
   - That the package name cannot change after the first upload comes only from a developer-forum thread
     (https://communityforums.atmeta.com/discussions/dev-quest/changing-package-name-on-builds/796473, via search
     2026-10-07) **[unverified in Meta docs]**.
3. **What it changes.** The Competition app will be a new Dashboard app, created after 2026-03-01. Its APK must target
   API 34 exactly, or the upload is refused. Changes for the Phase 2 Android build settings and the `aapt dump badging`
   check (`docs/plans/terrarium.md:310`):
   - set Minimum API Level 32 and Target API Level **34** explicitly, IL2CPP and ARM64;
   - choose the final `com.<company>.<app>` ids before the first upload;
   - assert `targetSdkVersion=34`, `minSdkVersion` in 29-34, `installLocation` auto, not debuggable, the
     `com.oculus.intent.category.VR` category, and `com.oculus.supportedDevices`.
4. **When.** In the Quest phase, before the first Dashboard upload (H1, 2026-10-27, is the first device install). A
   wrong targetSdk blocks **submission on 2026-11-17**.
5. **Confidence.** High for the API-level rule (two Meta pages agree, one says it is enforced at upload). The serialized
   value mappings and the package-name lock are marked unverified.

## Finding 7 - Application SpaceWarp: the shaders are not ready, and the gain is unproven at these budgets

1. **Dependency as found.** The plans list "possibly SpaceWarp (needs motion vectors in custom shaders)"
   (`docs/plans/terrarium.md:311`). There are 16 custom shaders, 10 in `shared/packages/com.gardenvr.fx/Runtime/Shaders/`
   and 6 in `apps/terrarium/Assets/Art/Shaders/`. None has an `XRMotionVectors` or `MotionVectors` pass (grep
   2026-10-07). By render queue:
   - 7 are alpha-test, for example `FGlow.shader:33` and `Moss.shader:35`;
   - 5 are transparent: `FCard.shader:43`, `FGlass.shader:27`, `JarGlass.shader:31`, `GhostHand.shader:19` and
     `FOverdraw.shader:7`.

   Only the TextMesh Pro SpaceWarp shaders have the pass (`apps/terrarium/Assets/TextMesh Pro/Shaders/TMP_SDF SpaceWarp.shader:327-328`).
   Budgets: Terrarium at most 40 draws and 60k tris, Sundial at most 30 draws and 30k tris (`docs/PLAN.md:112`).
2. **Upstream fact.**
   - Meta, "Application SpaceWarp Developer Guide", https://developers.meta.com/horizon/documentation/unity/unity-asw/
     (fetched 2026-10-07; updated 2026-05-13):
     - "AppSW is not supported under any other Graphics API" than Vulkan;
     - it "requires modifying your app's materials and render pipeline; any materials that have not been modified to
       support AppSW will produce artifacts";
     - for Unity 6, "Meta's fork of URP is still recommended, as it contains additional optimizations and support for
       transparent objects".
   - Unity OpenXR, "Shaders and SpaceWarp",
     https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.15/manual/features/spacewarp/spacewarp-shaders.html (via
     search 2026-10-07): custom shaders must add the `XRMotionVectors` pass.
   - Unity 6000.6.0f1 release notes, https://unity.com/releases/editor/whats-new/6000.6.0f1 (fetched 2026-10-07): "Added
     support for transparent and UI elements for SpaceWarp (UGUI & TMP)". This covers UGUI and TMP only.
   - URP here is a builtin package (`apps/terrarium/Packages/packages-lock.json:124-127`). Swapping in Meta's fork would
     mean embedding a forked URP; whether a fork matching 17.6.0 exists is **[unverified]**.
3. **What it changes.** SpaceWarp would mean:
   - Vulkan-only;
   - an `XRMotionVectors` pass in about 12 shaders, with the alpha-clip logic copied into it;
   - artifacts on the transparent glass, cards and glow, which are the hero of both apps.

   All of that serves a scene budgeted at 40 draws or fewer. Recommendation: remove "possibly SpaceWarp" from the Phase 2
   plan (`docs/plans/terrarium.md:311`) and keep it only as a fallback if H2 (11-03) measures a frame-rate miss with
   multiview and SRP foveation on. The rules ask for "min 60 fps" (Finding 1).
4. **When.** Quest phase. Decide at H2, **2026-11-03**.
5. **Confidence.** Medium. The requirements are fetched. "Not needed at these budgets" stays a hypothesis until H2
   measures device frame time, which no one has yet (`docs/decisions/0001-unity-for-both-apps.md:7`).

## Finding 8 - Test Framework is 1.8.0, not 1.6.0

1. **Dependency as found.** The manifests ask for `"com.unity.test-framework": "1.6.0"`
   (`apps/terrarium/Packages/manifest.json:44`, `apps/sundial/Packages/manifest.json:43`). The lock files resolve
   `"version": "1.8.0"`, `"source": "builtin"` (`apps/terrarium/Packages/packages-lock.json:159-162`, same lines in
   sundial). The charter and `AGENTS.md:42-43` treat the manifest versions as the pinned ones.
2. **Upstream fact.**
   - Unity 6000.6 manual, https://docs.unity3d.com/6000.6/Documentation/Manual/com.unity.test-framework.html (fetched
     2026-10-07): `com.unity.test-framework@1.8`, and "Core packages are fixed to a single version matching the Editor
     version."
   - Unity 6000.6.0f1 release notes, https://unity.com/releases/editor/whats-new/6000.6.0f1 (fetched 2026-10-07), list
     "com.unity.test-framework: 1.7.0 to 1.8.0" and, among others:
     - "Fixed an issue where [UnityOneTimeSetUp] was called a second time just before [UnityOneTimeTearDown] when the
       test fixture contained parameterized tests";
     - "The parent nodes of a test run where there is an exception happening in `OneTimeTearDown` are now correctly
       reported as failed in the Test Runner window."
3. **What it changes.** The PlayMode gate suites run on Test Framework 1.8.0, whatever the manifest says. These are U1
   `FirstRun_*`, S2 playback x10 and S4 `PauseAt_*` (`docs/PLAN.md:87`, `:98`, `:100`). Recommendation:
   - record 1.8.0 as the effective version in `AGENTS.md:42-43`;
   - optionally align both manifest lines to 1.8.0 (an owner-reserved manifest change, `docs/PLAN.md:181`; it changes
     nothing at runtime);
   - expect a fixture that throws in `OneTimeTearDown` to fail its parent node in results XML. The host reads those
     counts (`docs/PLAN.md:161`).
4. **When.** Before the gate, **2026-10-23** (a doc fix; the gate's PlayMode evidence is read on this version).
5. **Confidence.** High. The lock file and two Unity pages agree.

---

## Checked, nothing actionable

- **Unity 6000.6 patches after 6000.6.4f1.**
  - None published: https://unity.com/releases/editor/whats-new/6000.6.5f1 returned 404 on 2026-10-07.
  - 6000.6.4f1 (released 2026-10-01, changeset `12bfff696524`, which matches `apps/*/ProjectSettings/ProjectVersion.txt:2`)
    lists six known issues (https://unity.com/releases/editor/whats-new/6000.6.4f1, fetched 2026-10-07). None touches
    this repo:
    - the Vulkan `AcquireNextImage` crash (UUM-153744): the Windows player uses the default API, and whether the crash
      reaches OpenXR swapchains on Quest is **[unverified]**, so watch it at H1;
    - `Resources.UnloadUnusedAssets()` being slow in players (UUM-149540): no call in `apps/*/Assets` or
      `shared/packages`;
    - Play Mode frame drops with high-polling mice (UUM-142550): the gate's PlayMode suites drive scripted intents, not
      a mouse.
- **URP 17.6 shader change for Quest.** 6000.6.0f1: "`DistanceAttenuation` function now takes three arguments on the
  Meta Quest platform". No custom shader calls `DistanceAttenuation` (grep 2026-10-07).
- **Input System 1.20.1** (2026-10-05): editor UI fixes only. No reason to move off 1.20.0 (see Finding 2 for the
  setting that matters).
- **Multiview.** Every custom shader already declares `UNITY_VERTEX_OUTPUT_STEREO` (for example `FGlow.shader:69`,
  `GlassCommon.hlsl:40`). The render mode is set in the OpenXR settings that Phase 2 creates. `m_StereoRenderingPath: 0`
  (`ProjectSettings.asset:48`) is not the setting OpenXR uses **[unverified]**.
- **ASTC.**
  - Sundial overrides 85 textures for Android (formats 48 and 50, which I take to be ASTC 4x4 and 6x6 **[unverified
    enum values]**).
  - Terrarium overrides none. Its textures fall to Automatic, which Unity's format table puts at ASTC 6x6 for Normal
    quality (seen only in a search summary of an older manual, **[unverified for 6000.6]**). Either way that matches
    the plans' "ASTC 6x6".
  - The Quest splash-screen hang with texture compression targeting (UUM-131974) is fixed in 6000.6.0f1 and only
    affected app bundles with split binaries.
- **Engine and SDK support for the store.** VRC.Quest.Packaging.4 asks for "a supported SDK and engine version"
  (https://developers.meta.com/horizon/resources/vrc-quest-packaging-4/, fetched 2026-10-07). Meta's floor is 6000.0.66f2;
  6000.6.4f1 is above it. The same VRC asks for "a valid network security configuration". Check it in Phase 2's
  `aapt` step; I did not research it further.
- **APK size and 64-bit.** VRC.Quest.Packaging.5 ("less than 1 GB") and .6 ("64-bit"). Nothing in either app nears the
  size. The 64-bit setting is in Finding 6.
- **New Experience division.** The rules require work "conceived of and built within the competition window (starting
  September 24, 2026)". The repo's first commit is `a51a96e` (2026-10-02). The earliest contest files read are dated
  2026-10-02 (file times, `C:/Users/kazda/kiro/personas/.contest/arena/habit-garden/archive/*/NOTES.md`).
- **.NET for `shared/core-dotnet`.**
  - .NET 9 (tests, `shared/core-dotnet/GardenVR.Core.Tests/GardenVR.Core.Tests.csproj:3`) reaches end of support on
    **2026-11-10** (https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core, fetched 2026-10-07). That
    is a week before submission, but it ships nothing and the gate still runs.
  - Retargeting to `net10.0` (LTS to 2028-11-14) is post-MVP.
  - `netstandard2.1` for core (`GardenVR.Core.csproj:4`) needs no change.
  - `dotnet test shared/core-dotnet`: 195 passed, 0 failed (2026-10-07, this worktree).
- **Meta XR Core SDK currency.** 207.0 (2026-09-22) is the latest and is what the spike used. No newer release to chase.
