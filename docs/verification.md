# Verification

This file records the latest focused evidence, from the first-person reference
consolidation (Den #10124, 2026-10-19). The exact Engine pair is in
`Directory.Build.props`.

## Build and installed surface

**Pin.** `rusty update` advanced the pin from `0.1.0-dev.e84dc3354efc` to the
newest published pair. The listed release notes were checked, and the
installed assembly's surface was read by reflection.

- One break: `UiProjection` lost its sequence argument.
- New surfaces adopted:
  - `CharacterJumpConfig` buffer and coyote;
  - GameplayTime;
  - TunablesDebugModule;
  - InspectionTargetsDebugModule;
  - SpatialInspectionDebugModule and SpatialClearanceDebugModule;
  - overlay sets;
  - the developer shell's `onClose`.

**Checks that passed:**
- `rusty build --project src/FpTester.Game/FpTester.Game.csproj` (warnings as
  errors), with CoreCLR staging;
- `node --check` on every `src/ui/*.js`;
- `git diff --check`.

NativeAOT was not run.

## Engine and existing-product reuse checks

- **Jump buffering and coyote time.** These are the Engine controller's
  (`CharacterJumpConfig`); the lab authors only the seconds. No downstream
  timer was added. None of the five surveyed products set them. See
  [reference.md](reference.md#playerplayercontrollercs-first-person-body-and-camera).
- **World time.** It uses Engine GameplayTime (`SetRate`, `Hold`, `Advance`).
  There is no downstream clock or accumulator. Engine inspection time
  (`engine.time.*`) is separate and untouched.
- **Overlays, inspection, tunables and spatial readouts** use the Engine's
  modules. The product supplies only its explicit lists and delegates.
- **Action resolution** uses Engine spatial queries in the existing session,
  and impacts become Dynamics impulses in the one Dynamics step.
- **Existing product code.** Pieces were copied from rusty-hotel,
  rusty-riders, rusty-craftsurvive and rusty-doom with provenance recorded
  per file and in [reference.md](reference.md). Those repositories were read
  only, and none was modified.
- **Remaining gap.** Body push impulses for the character are still the
  missing binding in [engine-contact-gap.md](engine-contact-gap.md).

## Hosted runtime evidence

**Setup:**
- Session: crew-playtest `rusty-fptester-hosted`, `27422393-31f1-4edc-90a6-49f4df80e794`.
- Backend: browser.
- World: the session's own host.

**Method:**
- Inputs were real browser key, pointer-lock mouse and click events.
- Debug commands went through `/__rusty/product/runtime/debug/execute`.
- Engine action-driven time was used where noted.
- `fptester.dev.view` placed the player as assistance; it is not unaided
  navigation.

| Check | Observed |
| --- | --- |
| Jab (melee) | Click at feet (9, 0, 3.82), pitched −25°. Impact on cube 15 at (9, 1.00, 2.92); swept capsule start (9, 1.67, 3.82), end (9, 1.46, 3.37), radius 0.25; the cube moved; the overlay drew the green capsule |
| Bolt (projectile), burst (area), heavy (melee), shot (hitscan) | Each struck a cube. The burst struck cubes 14, 15 and 16, and the 1 kg cube was pushed about 4 m. The shot struck cube 16 at range |
| World rate 0.25 | 30 steps in 2 s of wall time (15/s at 60 Hz) |
| World hold | 0 steps in 1.5 s. A 200-unit mouse move still turned yaw to 22.9° while held |
| World step 0.5 s | 30 steps, then held. `fptester.time` read "stepping · 7 steps left" midway |
| Rate 2 | Refused: the Engine does not run faster than realtime |
| Tunables | `tunables.set speed 6` gave walking velocity 6.0 m/s. `tunables.diff` listed the unsaved pointer. `tunables.save` wrote `player.json` (the file was restored afterwards) |
| Live content | Hand edit of `content/player.json`: `rusty dev` restaged assets only, the host logged `content-reloaded player.json`, and the HUD notice read "Reloaded player.json" |
| Engine jump buffer | Jump pressed one frame (y 0.44 m) before landing from 3 m. With 0.12 s: a second jump started on landing (feet 1.11 m, 300 ms later). With 0: the press was lost and the body stayed grounded |
| Body events | Walking off the 3 m landing gave a hard landing at 10.8 m/s; a jump and landing were counted |
| `fptester.dev.view` | A point inside the west wall moved to the nearest clear floor (0.95, 0, 12). A point on the 2 m landing stood there. A point under the floor was refused. That last case was found and fixed in this change: before the fix, empty space counted as clear |
| Pickup and throw | E held cube 15. Primary threw it, with throw count 1, and no action started while holding |
| Pause menu | M paused the Engine lifecycle. The menu showed Resume, Reset lab and Developer shell (live-debug available) |
| Developer shell | Opened from the menu while play resumed. It docked the reach inspection view, the lab tunables page, the scene tree and the console |
| Inspection targets, spatial readouts, entity store | `inspect.targets` listed eye, reach, spawn and last-impact. `spatial.probe`, `spatial.grid`, `spatial.clearance` (with its 8 m bound) and `playtest.jump-plan` (refused while airborne) answered. `entity.get lab 1` showed Transform and CharacterMotion |

**Original evidence:**
- Jab overlay:
  `/home/agent/.local/state/crew-playtest-local/browser/27422393-31f1-4edc-90a6-49f4df80e794/ecb94b96-a2fe-4a2f-8053-c7b247f7e1e2.png`
- Pause menu: same directory, `1f129caa-f3e3-40b6-893c-15a42a1538ff.png`.
- Developer shell: same directory, `424996da-ac72-4cb9-ac97-f8d069f9098d.png`.

**Not certified:**
- **Primary through `assist act`.** The harness's `act` with the `Primary`
  binding delivered no pointer-button event in two tries, while a raw
  `playtest input` click did. This is a harness observation, not a product
  change.
- **Penetration recovery.** It was not triggered: ordinary play did not
  produce a deep penetration.
- **Coyote time.** It was read back (`coyoteRemaining`) but not timed by hand.
- **Other gaps:**
  - gamepad;
  - full course traversal;
  - closing the shell by key and its return of input;
  - head bob visuals.

## Earlier evidence (previous pin)

The original lab checks are in git history (`docs/verification.md` before
this change):
- movement and look;
- jump and crouch;
- pickup, drop and throw;
- HUD reset;
- the scene.

This change rechecked movement, jump, pickup and throw on the new pin.
