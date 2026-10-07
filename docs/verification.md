# Verification

## Build and installed surface

- Ordinary `rusty build --project src/FpTester.Game/FpTester.Game.csproj`: passed
  restore, compile with warnings as errors, SDK binding generation and CoreCLR staging.
- Advanced the inherited SDK/runtime pair with `rusty update`, read the listed
  release notes and API diffs, rebuilt on the matched pair. The newer runtime
  supports crew-services' explicit diagnostics-log argument. Exact version is in
  `Directory.Build.props`; no sibling source build or runtime override was used.
- `node --check src/ui/main.js`, `bash -n scripts/serve-playtest.sh` and
  `git diff --check`: passed.
- NativeAOT has not been run. Build/staging alone does not establish interaction.

## Engine and existing-product reuse checks

Outcome: no duplicated Engine mechanism found in the implemented slice; one
explicit upstream binding gap blocks bump-to-push acceptance.

- Engine FpsInput and Look handle physical state, edge facts, controller shaping
  and orientation. Product selects standard controls and authored sensitivity.
- Only Engine-admitted steps advance character and Dynamics. No extra clock or
  accumulator. EntityDynamicsAdapter copies native body facts into EntityStore.
- One SpatialSession supplies voxel and static-mesh collision to character and
  Dynamics; voxel presentation, camera and graphics use safe SDK services.
- WorldInteraction/InteractionVisibilityQuery perform fresh use eligibility;
  Engine fixed tethers carry cubes and Engine Dynamics applies throw impulses.
- Scene definitions and controller/interaction tuning are product-authored
  content loaded via Engine Content. Generated bindings remain ignored.
- The template counter was removed. Player continuation, dynamic body binding,
  cube carrying policy and DOM facts each have their named owner, without a
  competing counter, gameplay state or DOM update lane.
- Verified the safe public contact surface against the installed assembly at
  the final executable pin, and the matching Engine bridge at its source
  revision. Body push impulses remain unavailable; see engine-contact-gap.md.
- Adjacent donor/Engine repositories were read only. No commit/push was requested.

## Browser evidence

Hosted browser sessions used private worlds. The independent playtester and
implementation check used ordinary keyboard/mouse delivery. Relative debug look
and target-ID inspection assisted aiming; unaided aiming is not certified.

| Check | Observed result |
| --- | --- |
| Movement and look | WASD changed position; clicking the canvas captured the mouse and relative pointer movement changed view |
| Jump and landing | Space captured airborne at feet Y 0.587 m, vertical velocity 4.833 m/s; advancing 700 ms captured grounded at feet Y 0.02 m |
| Crouch | Ctrl captured Crouched with capsule centre Y 0.57 m; camera visibly lowered |
| Pickup, drop, throw | E acquired a dynamic cube, carrying lifted it in front of the camera, E dropped it; primary mouse released it with forward velocity and it subsequently landed |
| Reset | R restored player/cubes and cleared interaction counts; ordinary HUD button click succeeded in a fresh unlocked browser |
| Scene | Original captures show voxel walls/floor, four differently inclined ramps, steps and differently sized loose cubes |
| Final step stamps | Final jump/pickup/throw observations and captured Engine frame steps agree; generation comes from lifecycle facts |

The first independent jump/crouch samples were taken after landing or release.
Action-driven time with captures during the key hold resolved those uncertainties.
HUD clicks attempted while the canvas owned pointer lock were intercepted;
the fresh unlocked-session button check passed. Full course traversal, every
slope/step threshold, gamepad behavior and reciprocal body/character response
are not certified by these focused checks.

Original evidence, preserved without image editing:

- Final scene/jump: `/home/agent/.local/state/crew-playtest-local/slots/slot-3/browser/d25fb105-5050-4760-875b-1def3e06beae/action-44604dea-d18f-4dba-a052-5fdf75124658.png`
  (frame and observation step 1036).
- Final pickup: same session, `action-eb5d94c1-6c9d-4315-b8e7-9c177161e570.png`
  (held entity 14; frame and observation step 1096).
- Final throw: same session, `action-f9a9723c-dea1-4ece-9cb2-59463d7aa7dc.png`
  (held entity cleared, throw count 1; frame and observation step 1126).
- Independent crouch: `/home/agent/.local/state/crew-playtest-local/browser/64105de8-d59e-4e80-90e0-ed8eec233da9/action-e53d1fea-7f95-463d-acb5-6ddd2c088f82.png`.
- Independent HUD reset: `/home/agent/.local/state/crew-playtest-local/browser/bb02eadf-07fe-49e0-9400-f67d268e6616/1e40251d-2575-4420-bfa4-36486a76721a.png`.

Temporary hosted test sessions were stopped. A normal development host was left
running on port 8799 for the user. No native release or CI execution is claimed.
