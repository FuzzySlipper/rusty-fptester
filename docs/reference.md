# First-person reference

rusty-fptester is the maintained place to copy first-person and action-gameplay
code from. Products share code by copying it with provenance, never as a build
dependency. When you need a first-person body, a melee/hitscan/projectile action
model or the developer tooling around them, start from the files below. Do not
start from a product's copy.

Each piece has these parts:

- what it is for;
- where it came from;
- which products copied it (an older version, unless noted);
- what to change when you copy it.

When you copy a piece, add your product to its **Copied as** line in the same
change, so the next copy can find yours.

The survey behind this consolidation (Den #10124, 2026-10-19) compared five
products:

- rusty-hotel;
- rusty-riders;
- rusty-craftsurvive;
- rusty-dungeon;
- rusty-doom (the main LoadingBay product and its RoomStudy).

## Body and camera

### `Player/PlayerController.cs`: first-person body and camera

**What it does:**
- Engine `FpsInput`/`Look` handle controls.
- One Engine character proposal moves the body per admitted step.
- One interpolated Engine camera sample is published per update.
- The accepted transform and motion live in the product's `EntityStore`.

**Folded in from the copies and the survey:**

| Behaviour | From |
| --- | --- |
| Look integrates on unscaled `HostElapsedSeconds`, so it keeps its speed while the world holds or slows | rusty-riders `Walker.ReadInput`; Engine csharp-lifecycle "Looking and controls while held" |
| Walking and sprinting configs built and validated once | rusty-hotel `HotelPlayer`, rusty-riders `Walker` |
| Movement scale and an external impulse per step, so actions can root, slow or knock the body | rusty-riders `Walker.Step` |
| `Place`, `PlaceClear` (nearest clear, floored place) and `YawDegreesToward` | rusty-hotel `HotelDeveloper.View`/`ClearNear`, rusty-riders `Walker.Place` |
| Camera disposed if `SetActiveCamera` throws | rusty-hotel `HotelPlayer` |
| FOV, far plane and eye height are authored; the eye follows crouch | rusty-hotel, rusty-riders |
| Penetration recovery when the Engine refuses a step | rusty-craftsurvive `PlayerRecovery` |
| Head bob as camera presentation only, scaled by walking speed; aim uses the unbobbed eye | rusty-dungeon `HeldAnimations.HeadBob`, re-expressed in metres walked |

**Jump buffering and coyote time belong to the Engine controller.** They are
`CharacterJumpConfig.BufferSeconds` and `CoyoteSeconds`, read back as
`CharacterMotion.JumpBufferRemaining` and `CoyoteRemaining`. The lab authors
them in `content/player.json`.

None of the surveyed products set these values. rusty-craftsurvive has no
buffer of its own: its "jump buffering" is the same pending-press latch as
here, which keeps a press until the next admitted step. Do not write a
downstream buffer or coyote timer.

**Copied as:**
- rusty-hotel `src/Hotel.Game/Player/HotelPlayer.cs`;
- rusty-riders `src/RustyRiders.Game/Player/Walker.cs`.

**When copying:**
- Replace the session, entity store and entity with your scene's.
- Compute obstacles and support from your own dynamic bodies. The lab's are in
  `LabScene.Obstacles`/`Support`.
- Keep the pending-press latch for the jump in your product entry.

### `Player/PlayerTuning.cs` and `content/player.json`: authored feel

These hold:
- body size and eye;
- speeds, jump and gravity;
- step and slope;
- jump buffer and coyote seconds;
- mouse sensitivity, FOV and far plane;
- head bob.

The file is strict JSON: missing, null or unknown fields are errors. It is
declared `RustyEngineLiveContent`, so an edit applies without a restart
(`FpTesterProduct.ReloadLive`).

**From:** the shape of rusty-riders `WalkerTuning` and its strict JSON context.

### `Player/PlayerRecovery.cs`: penetration recovery and clear placement

**Recovery:**
- Recognises the Engine's `unresolved-character-controller-penetration`
  refusal.
- Searches with Engine capsule overlaps: where the body last stood clear,
  then straight up.

**Placement:** searches rings around a requested point for developer
placement.

**From:**
- rusty-craftsurvive `Modules/Player/PlayerRecovery.cs`: the refusal and the
  upward search;
- rusty-hotel `HotelDeveloper.ClearNear`: the ring search.

### `Player/BodyEvents.cs`: footsteps, jumps and landings

Reads one step's before and after motion and reports:
- a footstep each stride;
- a jump when the body starts rising from the ground;
- a landing, or a hard landing after a fast fall, with the landing speed.

Products raise their sounds, dust, camera kick or fall damage from these
flags.

**From:** rusty-craftsurvive `PlayerFootfalls`, without water and climbing.

### `Player/InputDiagnostics.cs`: what input arrived

Gives counts per update, totals and the last event (including Clear and its
reason) as one readout. It answers "did the key arrive, or was it cleared?"
for scripted sessions.

**From:** rusty-craftsurvive `PlayerInputDiagnostics`.

## Action model

The action model is product-neutral. Damage, stats, costs, ammunition and
effects are product rules. Extend `ActionDefinition` with your own fields and
apply them where the lab's `LabCombat.Apply` pushes cubes.

### `Actions/ActionDefinition.cs` and `content/actions.json`: what an action is

Each action has:
- a delivery: `Melee` capsule sweep, `Hitscan` ray, `Projectile` cast per
  step, or `Area` sphere with line of sight;
- timing: windup, commit, recovery and cooldown;
- HUD labels;
- `busyMovementScale`;
- an impulse for struck dynamic bodies.

Validation names the file and the field.

### `Actions/ActionUser.cs`: one actor's action timing

It owns:
- phases on admitted seconds;
- the aim committed at start, so strafing out of a swing evades it;
- per-action cooldowns and interruption.

`Begin` returns the seconds the action costs, which a turn- or movement-timed
product buys with `GameplayTime.Advance`.

### `Actions/ActionResolution.cs`: what an action struck

Lands actions with Engine `CastCapsule`, `CastRay` and `CastSegment` against
explicitly offered body colliders. It also flies projectiles per step. A
melee sweep that starts inside a wall falls back to a ray along the aim.

It returns `ActionImpact`s: the target or surface, the point, and the swept
volume.

**From all three action files:**
- rusty-hotel and rusty-riders `Actions/ActionDefinition.cs`, `ActionUser.cs`
  and `ActionResolution.cs`, which were near-identical copies of each other;
- before them, rusty-dagger `AttackExecution`/`CombatResolution`.

**What the merge took from each:**
- riders: the time scale, the end-of-phase tolerance and `Duration`;
- hotel: the `Started` count and `Elapsed`;
- both: the queries, the wall-hug fallback and projectile stepping.

**Left out:**
- the stat, contribution, resistance and effect pipeline;
- track and round costs;
- the `Self` delivery, which existed only for reloads and self-effects.

**Copied as:** rusty-hotel and rusty-riders `Actions/` (pre-merge versions).

**Related patterns not taken, and why:**
- rusty-dungeon's hold-to-charge swing: product feel; it can be added as a
  phase before windup.
- rusty-doom RoomStudy's frame-table fire delay: the windup here covers it.
- rusty-doom's gamepad `AimAssist`: an Engine helper to adopt when a product
  needs controller aim.

### `Actions/ActionOverlay.cs`: action volume overlays

Publishes the Engine overlay set `actions.volumes`. It draws:
- each landed capsule, ray or sphere for 2.5 s: green for a body, amber for a
  surface, grey for a miss;
- the volume an action in windup is about to sweep;
- projectiles in flight.

It draws only while inspection shows the set; the developer shell shows it
while open. Run `engine.inspect.overlay show set actions.volumes` to see it
without the shell.

**Origin:** fptester. No product drew its melee volumes; rusty-hotel
`HeldInspection` is the overlay idiom.

### `Combat/LabCombat.cs`: the lab's use of the model

- The player uses the selected action.
- Struck cubes take the action's impulse through Dynamics.
- It keeps counts and the last impact.

This is the file a product replaces with its own rules.

## World time and lab tooling

### `Time/WorldTime.cs`: product world time

The lab's policy over Engine `GameplayTime`:
- a rate in (0, 1] for slow motion;
- hold;
- a bounded step, then hold.

Selecting it makes every host observation an update. Look, camera and HUD
therefore keep moving while the world holds.

This is the product's world time, distinct from the Engine's inspection time
(`engine.time.mode manual`, `engine.time.advance`), which a harness uses and
which overrides it.

**From:**
- rusty-riders `Time/TimeFlow`: one owner over GameplayTime;
- rusty-craftsurvive: menu hold via GameplayTime.

The developer rate, hold and step controls are fptester's.

**Keys and commands:**
- `P` holds and resumes; `O` steps one frame.
- `fptester.time`, `.rate R`, `.hold`, `.run`, `.step S`.

### `Developer/LabDeveloper.cs`: developer commands and spatial readouts

**Commands:**
- `fptester.inspect`;
- `fptester.dev.view x y z yaw pitch`: a repeatable viewpoint at the nearest
  clear, floored place;
- the `fptester.time.*` commands;
- `fptester.action.select ID`.

**Delegates for Engine modules:**
- `InspectionTargetsDebugModule` targets:
  - `eye`;
  - `reach`: the selected action's reach in camera space;
  - `spawn`;
  - `last-impact`;
  - `held-cube`.
- `SpatialInspectionDebugModule`: grid, probe and jump plan.
- `SpatialClearanceDebugModule`.

**From:**
- rusty-hotel `HotelDeveloper.View` (`hotel.dev.view`). This version adds
  height and requires a floor.
- rusty-doom RoomStudy `LoadingBayPlaytestSpatial`: the grid, probe,
  jump-plan and clearance adapters.

### `Developer/LabTunables.cs`: the Engine tunables page

The page served by `TunablesDebugModule` has four groups:
- body and view, bound to `player.json` pointers;
- the chosen action, bound to `actions.json`, with a chooser for which action;
- world rate and hold, live only.

Controls apply at once through their owners. Save writes through the Engine
content source.

**From:** rusty-hotel `Interface/Workbench/HotelWorkbench` (the page and
chooser shape).

### `FpTesterProduct.cs`: composition and lifecycle

Each update:
1. Reads input and turns the look on host time.
2. Keeps the press edges (jump, use, primary) pending until an admitted step.
3. On each admitted step, runs body, then focus and use/throw, then the
   action, then Dynamics.
4. Publishes the camera and HUD.

**Other duties:**
- `HandlePausedIntents` takes the reset claim from the pause menu.
- `ReloadLive` applies live content.
- It registers these modules:
  - `PlaytestDebugModule`;
  - `InteractionDebugModule`;
  - `TunablesDebugModule`;
  - `InspectionTargetsDebugModule`;
  - `SpatialInspectionDebugModule`;
  - `SpatialClearanceDebugModule`;
  - `EntityStoreDebugModule` (store `lab`, with a `CharacterMotion`
    projection after rusty-craftsurvive and rusty-doom).

## Product UI shell

### `src/ui/main.js`: HUD, pause menu and developer shell

- The HUD reads the `rusty.fptester.hud` projection.
- `M` opens a pause menu over the Engine lifecycle port. The menu follows the
  lifecycle state, so a pause from elsewhere shows it too.
- `F3` docks the developer shell beside running play, and gameplay input
  comes back when it closes.

**From:** rusty-hotel `src/ui/main.js`, reduced to two screens.

### `src/ui/pause.js`: pause request lifetime over the lifecycle port

**From:** rusty-hotel `src/ui/pause.js`.

**Copied as:** rusty-riders `src/ui/pause.js`.

### `src/ui/developer.js`: the Engine developer shell

It docks:
- an inspection view on the `reach` target;
- the tunables panel;
- the scene tree;
- the console.

While open, it shows the `actions.volumes` overlay set. The shell is offered
only when the live-debug service answers.

**From:** rusty-hotel `src/ui/developer.js`. This version uses the Engine
shell's `onClose` in place of hotel's element observer.

**Also seen:** rusty-craftsurvive `developer.ts` and rusty-doom
`loading-bay-debug.ts` mount the older inline live-debug panel and metrics
widget. `mountDeveloperShell` supersedes both.

## Not taken, and why

- **rusty-craftsurvive:**
  - its 120 Hz controller accumulator: this is a second clock; step once per
    admitted step;
  - its own input interpreter: `FpsInput` covers it;
  - swimming, climbing, boats and world-origin rebasing: product-specific.
- **rusty-dungeon:** the tile-grid movement and collision, which is not the
  Engine controller.
- **rusty-doom (main LoadingBay product):**
  - fire and use run per update, not per admitted step;
  - its hitscan casts from the body centre;
  - its camera has no interpolation.

  Its RoomStudy is the better example and was the source above.
- **Bump-to-push of loose cubes:** still a missing Engine binding; see
  [engine-contact-gap.md](engine-contact-gap.md).
