# Play Sundial on Windows

The review build is a Windows player. Close the window to quit. Esc pauses and leaves the dial on the desk.

## Launch

Double-click:

`apps/sundial/Build/sundial/Sundial.exe`

That opens a fullscreen window. For a window on the desktop:

```
apps\sundial\Build\sundial\Sundial.exe -screen-fullscreen 0 -screen-width 1280 -screen-height 720
```

A zip of the same folder, when it is present, is `apps/sundial/Build/Sundial-windows.zip`. Unzip it and run `Sundial.exe` inside. Leave out the folder whose name says not to ship it.

The first picture is the dial drawing itself. A line is written beside the exe, `first-interactive.txt`, when the dial can take a gesture.

## Reset the save

Quit the player first. The dial writes its page on the way out.

Delete this folder:

`%USERPROFILE%\AppData\LocalLow\DefaultCompany\sundial\sundial`

That is `save.json` and the older copies (`save.prev1.json`, `save.prev2.json`, `save.snapshot.json`). The next launch starts the first run again.

If the page did not load, the dial shows "This page didn't load." Click "Restore the previous page". That puts the last good copy back. It is not a reset.

## Controls

The player reads the keyboard and the mouse in one place, `KeyboardMouseIntentSource`. Each hand intent has a short hint from `HandBindings.PcHint`. The keys below are what that provider actually does.

| Intent | Hint | What to do |
|---|---|---|
| Look | mouse | Move the cursor onto a plant, tile, packet, or chip. The ink halo settles after 0.15 s. Tab steps to the next target. Shift+Tab steps back. Looking never tends on its own. |
| Pinch | click | Left click, and release before 0.30 s. Enter does the same on the focused target. |
| PinchHold | Space or mouse | Hold Space, or hold the left mouse button, for at least 0.30 s. |
| Release | release | Let go of Space or the left button after that hold. |
| Poke | F | Press F while the cursor is on the target. |
| PalmOpen | hold P | Hold P for 0.60 s. The middle mouse button does the same. It fires once per press. |

A tap of Space does not pinch. Space only counts while it is held.

Hold the right mouse button and drag to look around the desk. The view stays seated: about 40 degrees to either side and 25 degrees up or down. Press R to look straight again.

Esc, or clicking away from the window, pauses. A breath in progress holds still. A quiet hour holds still. The dial stays where it is. Come back and it continues.

Hold P to put the dial away. Hold P again to bring it back. During the morning reach, a palm on a mark is the reach, and the dial stays.

This player is not a development build. F1, F2, F3, the bracket keys, and T do nothing here.

## The first ten minutes

The garden day runs from 03:00 to 03:00. Morning is 06:00 to 11:00. Midday is 11:00 to 18:00. Wind-down is 18:00 to 03:00. From 03:00 to 06:00 no plant is due. You can still walk the first run. The midday plant accepts the first mark in that quiet stretch.

A fresh save does this on its own, then waits for you:

1. The dial draws in (about 1.2 s).
2. The shadow sweeps from 06:00 to now (about 1.5 s).
3. The caption "This is your day." inks on (about 0.9 s).
4. Three packets rise at the near edge (about 0.8 s).

Voice and the music beds are off until you turn them on. You will hear the page and, later, the tock. You will not hear a spoken line until Voice is on.

Then:

1. Click the morning packet. Click one chip: Water, Stretch, or Make the bed.
2. Click the midday packet. Click one chip: Top-3 plan, Walk a stop, or Screen-free lunch.
3. Click the wind-down packet. Click one chip: Three breaths, Phone away, or Read.
4. The seeds drop. Click the plant that is glowing, once. A tock plays and today's tile fills. A small ring sits on that tile for 6 seconds. Click the ring to take the mark back.
5. Three breaths is not a quick click. When the wind-down plant offers "Try three breaths?", hold Space or the left button on that plant to breathe in, and let go to breathe out. A breath counts when the hold lasts at least 1.5 s. Do that three times. The plant opens its next leaf and a chime plays. A finished ritual has no undo.
6. Hold P to put the dial away, and hold P again to bring it back. Press Esc once and confirm the dial stays.
7. Move the cursor to the Settings tab on the near rim and press F. Press F on a row to flip it. The rows are Mute, Beds, Voice, and Reduced motion. An ink dot is on. A pencil ring is off. Press F on Settings again to close the sheet. Voice turns on the spoken lines. Beds turn on the quiet music. Mute silences every bus. Reduced motion shows the end of a drawing at once and stops the line boil.

If you still have a minute:

- Morning, while the morning plant is not yet kept: "Reach to three marks?" Look at each mark and hold P there.
- Midday, while the midday plant is not yet kept: "One small mark?" Click one of the five ink symbols. That keeps the midday habit for today.
- "A quiet hour?" Click the gnomon. The shadow is drawn on for 25 minutes. Click the gnomon again to end early. Ending early still counts. Esc holds the hour.
- Hold Space or the left button on the near page corner for about half a second. The page turns to "Four weeks". Hold the corner again to turn back. A miss on that page stays pale.
- Hold on the rim and move backward to read earlier today. Let go. The shadow eases home. Nothing is written while you are reading.
- A pale tile with a small "?" is yesterday. Click it, or press F on it. The line is "Did it happen?" Click "Yes, it happened" once, or "Not this time". Yes draws that tile hatched, with the real time, and it stays late.
- A chip that says "Another" adds one more habit on that arc, up to three.

A second click on a plant you already kept today does nothing. Nothing on the dial counts a run of days, and a missed day leaves the plant as it is.

## Owner checklist

Fill this in while you play. A blank is fine.

| Check | Yes / no / note |
|---|---|
| The dial was up within a few seconds of launch | |
| I chose the three habits from the packets | |
| I tended the glowing plant, or I finished three breaths | |
| I could read the day at a glance | |
| Hold and release felt like a breath | |
| I put the dial away and brought it back | |
| I opened Settings with F | |
| A miss, if I saw one, stayed quiet | |
| I would open this again tomorrow | |
| Core gesture, 1 to 5 | |

## Gate rows that need you

These rows in `apps/sundial/GATE.md` stay `needs-owner` until you witness them. The host rows are already filled.

| Id | What only you can close | Where to look |
|---|---|---|
| U2 | A first time through, fresh save, a stopwatch, and no notes outside the app. The first complete moment in 10 minutes or less. | This play. The checklist above is the note. |
| U4 | Use the PC build on at least 4 of the 7 days from 2026-10-15 through 2026-10-21, and say whether you would open it tomorrow with no reminder. | The save file in the reset section. This Friday is before that week. |
| S6 | Finish the journey and rate the core gesture. | The last row of the checklist. |
| A1 | Score each gate frame at least 4 out of 5. | Side by sides `firstrun.sbs.png`, `missed.sbs.png`, `day7.sbs.png`, `settings.sbs.png` in `orchestration/runs/sundial/T-SUN-020/`. |
| A6 | Listen to the mix. | `orchestration/runs/sundial/T-SUN-019/loudness.txt` and the dusk ritual in this build. |
| A7 | Watch the reduced-motion hour. | `orchestration/runs/sundial/T-SUN-009/dusk.mp4` (60 s). Reduced motion is the last Settings row. |
