# Meta VR Start Developer Competition 2026 - the rules that bind (host transcription of the official page)

Window: opened 2026-09-24, entries close **2026-11-18**, winners announced ~2026-12-11. Eight weeks; at contest
time (2026-10-02) about 6.5 weeks remain. $1M across ~20 prizes, top prizes $100,000. Top awards per track, plus
special awards for standout craft across tracks.

## Tracks (pick at least one)
- **Entertainment**: lean-back media and content (spatial cinema, interactive video, music visualization, storytelling, spatial audio, media companions).
- **Gaming**: hands-first or eyes-and-hands games; puzzle, strategy, casual, social, narrative; thrive seated, no controllers.
- **Productivity** (THIS ENTRY): apps that make you more effective anywhere or are designed around specific daily moments - multi-panel workspaces, task management, creative tools, or **habits tied to a recurring context (morning routine, commute, wind-down)**.

## Division
New Experience: conceived and built inside the window (from 2026-09-24). No pre-existing codebase.

## Hard requirements
- **Hands-first**: fully usable with hands end-to-end. Test: can someone complete the entire experience without ever pairing a controller?
- **Policy compliance**: Meta VR / Start terms, Community Standards, Code of Conduct in VR, Developer App Policies, Content Guidelines.

## Design guidelines (judged)
- **Seated-optimized**: seated, stationary; limit roomscale. *Airplane seat test*: does every interaction work in a two-foot radius?
- **Easy in, easy out**: fast cold start, clean pause/resume, something satisfying in 10 minutes or less. *One bus stop test*: a complete moment in a short ride.
- **Original, not a wrapper**: core experience is entrant-built. *Take-it-away test*: remove the third-party service - is there still a project? (Thin wrappers around YouTube, ChatGPT etc. are highly discouraged.)

## Devices
Meta Quest and **Meta VR Glasses** (see TOOLING.md for what is known about the glasses). The store surfaces hand-tracked titles first to glasses owners without controllers.

## Suggested SDKs
- Unity: Meta XR SDKs v81+ (v207) All-in-One XR
- Unreal: UE 5.8 + Oculus Integration SDK v207+
- Android: React Native via Expo, or Jetpack Compose via Meta VR Plugin for Android Studio
- Immersive Web SDK (IWSDK): Node.js >= 20.19.0, `npm create @iwsdk@latest`
- Organisers also promote AI tooling, including **Meta VR CLI**.

## What is submitted
1. The build: APK on a "Competition" release channel + invite URL (Unity/Unreal/Android), or a hosted link for IWSDK/WebXR (GitHub Pages, Vercel...). Frozen at the deadline; must stay reachable through judging.
2. A demo video under 3 minutes (headset capture, XR Simulator or emulator footage accepted), public on YouTube/Vimeo. Judges need not watch past 3 minutes - lead with the best.
3. A form: name, 140-char tagline, track, division, <=500-word description (inspiration, how built, future plans), how hand interactions are implemented, target launch date, team.

## Context from the previous competition (2025, $1.5M, 32 awards)
Lifestyle winner: Inkphony. Hand Interactions awards: Hand Survivor, Pocket Lands, Awesome Hand. Entertainment: DreamSpace. Casual: Tiny Golf.
