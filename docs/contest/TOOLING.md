# Tooling, devices and evidence (host-commissioned web research, 2026-10-02)

Status marks: **[V]** stated by an official Meta page or primary source the researcher read; **[S]** secondary source
(press, third-party repo, search snippet); **[U]** could not be verified - design around it with a fallback.

## A. Windows-side dev and test tooling

| Tool | What it does for a hands-first app on Windows | Limits |
|---|---|---|
| **Meta XR Simulator v207** [V] (https://developers.meta.com/vr/documentation/unity/xrsim-intro/) | PC OpenXR runtime. Device profiles Quest 2/Pro/3/3S and **Meta VR Glasses** (eye gaze + hands, no controllers). Hands via mouse/keyboard pose presets (keys 1-4: aim/poke/pinch/grab) **or webcam-driven hands** with per-finger pinch strength; one tracked + one simulated hand can mix; v207 adds Look-and-Pinch. **Synthetic environments**: passthrough, scene (walls, tables), spatial anchors, depth. **Session capture**: record to VRS and replay. Works with Unity, Unreal and native OpenXR. | Not an Android layer, no device-hardware emulation. Headless CI is **not documented** [U]. |
| **Meta XR Operator** [V] (https://developers.meta.com/vr/documentation/unity/meta-xr-operator/) | Unity 6 + Core SDK v207+: an **MCP server that lets an agent drive head pose, hand gestures (pinch/poke/grab), gaze-and-pinch and read state** - in the Simulator, on a device over ADB, or over Link. The closest thing to scripted hand tests. | Unity only. |
| **IWSDK + IWER** [V/S] (https://github.com/meta-quest/immersive-web-emulation-runtime) | `npm create @iwsdk@latest`: Vite + Three.js, **IWER emulator built in**, hot reload, test on headset via LAN or ADB port-forward. IWER README lists XRHand pose presets (pinch, point), a **VR Glasses profile**, a synthetic environment module, action capture/playback. IWER 2.5 added the glasses FOV profile; IWSDK reached 1.0 [S]. Submission is a hosted URL (no APK). | **Playwright automation only via a third-party MVP fixture** (`playwright-webxr`, 2 stars; controller helpers shown, hand gestures not) [S]. Headless test tooling is immature. WebXR hand-tracking quality vs native on device [U]. |
| **Meta Spatial SDK (Kotlin) + Meta Spatial Simulator** [V] | A Quest-tuned Android Virtual Device inside the Meta VR Android Studio plugin. Good for Jetpack Compose **panels**; input is mouse/keyboard on panels plus gaze. React Native/Expo reaches Quest via Software Mansion's `expo-horizon` (local notifications work) [S]. | Weak for 3D hand gestures. |
| **Meta VR CLI (`metavr`) / hzdb** [V/S] (https://developers.meta.com/vr/discover/metavr/) | CLI + MCP server: device management, install/launch, logs, screenshots/video, Perfetto traces, docs search, Asset Library search, Store publishing. Unity, Android, WebXR. Gives an agent "eyes" on a device. | No eval or test assertions. Whether `metavr` is renamed hzdb [U]. |
| MQDH, ADB, OVR Metrics, RenderDoc, Link/Air Link, casting | Standard on-device profiling and casting. | **Need a headset.** Not re-verified for 2026 [U]. |
| Unreal | Meta XR v207 / Interaction SDK for Unreal (UE 5.4+), works with XR Simulator [V]. | **Open Meta bug: v207 plugins built against UE 5.7.0 fail to load in 5.8** [V]; Lumen/Nanite unsupported on Quest [V]. |

**Researcher's conclusion (one opinion, not a mandate):** the most testable-on-Windows loop for a hands-first app is
**Unity + Meta XR SDK v207 + XR Simulator** (Glasses + Quest 3 profiles, synthetic room + anchors) with **Operator**
scripts driving gestures and **recorded VRS sessions** for regressions, and `metavr` for on-device spot checks. The
fastest CI-able loop is **WebXR via IWSDK + IWER**, with immature headless tooling. Only a headset can certify: real
hand-tracking quality and jitter, gesture false positives, comfort, cold-start time, performance, and the true glasses FOV.

## B. Meta VR Glasses and Horizon OS

- Glasses [V]: ~100 g, ~70x66 deg FOV (Quest 3: 110x96), "5K" display, colour passthrough + depth, $1,299.99, **ship spring 2027** - **no one can test on real glasses before the 2026-11-18 deadline**; only the Simulator / IWER glasses profiles.
- Input [V]: eyes + hands + voice ("look to target, tap to select"); Touch Plus controllers optional; no neural band mentioned.
- Same OS, SDKs and Store as Quest; immersive, panel/2D, Android and web apps all run [V]. The Store surfaces hand-tracked titles first to glasses owners without controllers [S].
- **No dedicated "glanceable" widget or ambient surface was found** [U] - plan any glance as a small panel app.
- Notifications: Platform SDK user notifications (dashboard- or event-triggered) [V]; local notifications via `expo-horizon-notifications` [S]; push "still under development" [S]; background scheduling limits [U].
- **Spatial anchors persist across sessions** (can keep the garden on the user's real table) [V]; shared anchors via Bluetooth colocation [V].
- Passthrough Camera API on Quest 3/3S [V]; on the glasses [U].
- No health/wellness data API found [U].

## C. Habit science and market

- **Broken streaks hurt**: Silverman & Barasch (J. Consumer Research, 2023) - seeing a broken streak lowers further engagement, even when the break was not the person's fault [V, summary]. Supports a garden that wilts gracefully and recovers rather than a counter that resets to zero.
- **Missed days barely matter to habit formation**: Lally et al. 2010 - median 66 days to automaticity; one missed day had little effect [V].
- Implementation intentions (Gollwitzer & Sheeran 2006, d about 0.65) and Fogg's Tiny Habits: cited from memory [U - widely known, not re-checked]. Popular "63% / 71% streak" statistics are probably fabricated blog numbers - do not cite them.
- VR meditation evidence: a TRIPP pilot with university students showed lower stress/anxiety (pre/post, not an RCT) [V]; TRIPP had the highest MARS score (4.06) in a 2025 review of mindfulness VR apps [V]. **This entry may make no medical claims.**
- Quest wellness apps [S, search snippets]: TRIPP 4.1 (~3.5K ratings, free), Nature Treks 3.9 ($9.99), Maloka 3.9 (free). None is a habit tracker with a living-metaphor progress loop.
- 2025 Start: Best Lifestyle = **Inkphony** ($100K; the room becomes an instrument), runner-up Tarot Experience VR - AI Edition; judges praised "creativity and technical excellence"; separate awards for hands and for Passthrough Camera Access + AI features [V]. The 2026 Productivity track explicitly names "habits tied to a recurring context" and pays **$70K for first place** [S].
