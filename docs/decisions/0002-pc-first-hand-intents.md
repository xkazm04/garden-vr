# 0002 - PC-first, hands mirrored through intents

Date: 2026-10-02. Status: accepted (owner).

**Decision.** For roughly 2-3 weeks there is no headset. All interaction goes through `com.gardenvr.input`
(`HandIntentKind`: Look, Pinch, PinchHold, Release, Poke, PalmOpen) with a keyboard/mouse provider. App code never
reads devices. Quest integration starts only if the app proves useful, the mechanics are seamless and the art is
beautiful (gates in `docs/PLAN.md`); it should then be a provider swap (XR Hands / Meta Interaction SDK), not a rewrite.

**Consequences.** The PC mapping must preserve the feel of each gesture (hold-to-breathe stays a hold), and the
scripted-input playback in `com.gardenvr.capture` is how agents test journeys without a human.
