# 0001 - Unity 6.6 URP for both apps

Date: 2026-10-02. Status: accepted (owner).

**Context.** The habit-garden contest ran three rounds: design (three reports), spikes (Unity vs IWSDK) and a fidelity
gate (both stacks rendering both chosen styles against the owner's Leonardo frames). Round 3 found no visual ceiling in
either stack on desktop; Quest frame time is unmeasured for both. Unity carries the stronger Quest toolset (SpaceWarp,
foveation, Depth API, OVR Metrics) and the proven agent-driven hand test loop (Meta XR Operator).

**Decision.** Both apps are Unity 6.6 (6000.6.4f1) URP projects. The owner: "On both sides Unity would probably lead us
further." The IWSDK seat's assets (drawn dial textures, glTF) are kept as seeds in `shared/assets/seed-textures/`.

**Consequences.** One engine, shared packages, one toolchain to harden. The hosted-URL submission path is given up.
