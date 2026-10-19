# Product architecture

> The product decides. The Engine guarantees.

fptester is the maintained first-person reference. [reference.md](reference.md)
lists each piece with its provenance and the products that copied it.

| Owner | Responsibility |
| --- | --- |
| `src/FpTester.Game/FpTesterProduct.cs` | Explicit composition, lifecycle, pending press edges, live content reload and Engine-admitted step order |
| `Content/Authored.cs` | Strict authored-file reads through Engine Content, with file-and-field errors |
| `Scene/LabDefinition.cs`, `content/lab.json` | Typed authored geometry, spawn, world gravity and pickup tuning (edits restage) |
| `Scene/LabScene.cs` | Session, voxel admission, static fixtures, dynamic body/entity bindings, cube obstacles and support, presentation lifetimes |
| `Scene/LabGeometry.cs` | Authored triangular-prism and box geometry; no collision solver or renderer |
| `Player/PlayerController.cs`, `PlayerTuning.cs`, `content/player.json` | Body and camera: look, controller configurations, accepted continuation in EntityStore, placement, recovery, head bob (live content) |
| `Player/PlayerRecovery.cs`, `BodyEvents.cs`, `InputDiagnostics.cs` | Clear-place search, step body events, input readout |
| `Actions/`, `content/actions.json` | Product-neutral action definitions, per-actor timing, spatial resolution and volume overlay (live content) |
| `Combat/LabCombat.cs` | The lab's use of actions: selection, cube impulses, counts |
| `Interaction/CubeInteraction.cs` | Pickup/drop/throw policy, selected body and Engine tether lifetime |
| `Time/WorldTime.cs` | The lab's gameplay-time selection: rate, hold, bounded step |
| `Developer/` | Debug commands, tunables page, inspection targets, spatial readout adapters |
| `Ui/` | Typed observation and HUD projection layout |
| `src/ui/` | DOM HUD, pause menu over the lifecycle port, developer shell mounting |
| Engine SDK/runtime | FpsInput/PhysicalInputState, Look, admitted time, GameplayTime, character controller (including jump buffer and coyote time), collision and spatial queries, Dynamics, entities, content and live content, rendering, overlays, camera, UI transport, debug modules and host |

## One update path

The product selects gameplay time at start, so every host observation delivers
an update. Each update does the following:

1. Reads input through `FpsInput` and integrates look on unscaled
   `HostElapsedSeconds`, so look keeps its speed while the world holds or
   slows.
2. Keeps the press edges (jump, use, primary) pending until an admitted step.
   Clear, Pause, Resume and Reset cancel them.
3. Applies selection and world-time keys at once.

For every Engine-admitted fixed step:

1. Propose the character step with the current dynamic cube poses and
   velocities as obstacles and the supporting cube as support. Pass the
   action's movement scale. Retain only the accepted transform and motion.
   The Engine buffers a jump pressed in the air and allows coyote time
   (`CharacterJumpConfig`). A deep-penetration refusal stands the body clear
   instead.
2. Refresh Engine WorldInteraction focus. Apply a pending pickup or drop.
   A pending primary throws a held cube, or starts the selected action.
3. Step the action: land it from the current eye along its committed aim
   when its windup ends, and fly projectiles. Impacts on cubes become
   Dynamics impulses.
4. Update the held body's Engine fixed tether, then step and read Dynamics
   once with the launch and impact impulses (EntityDynamicsAdapter).

After the steps, the update publishes:
- the camera sample at world time;
- the scene snapshot;
- the action-volume overlay (on host time);
- the HUD projection.

There is no downstream accumulator, second clock, collision solver, input
transport, browser renderer or handwritten interop.

## Who owns what

- **EntityStore** holds the player continuation and the body readout
  projection.
- **Dynamics** is authoritative for loose cubes.
- **Scene fixtures** stay immutable between reloads.
- **Live content** (`player.json`, `actions.json`) reaches the product as a
  content change. The product reads it again, validated as at load, and hands
  it to its owner. A file that does not load is reported in the HUD notice
  and the last values are kept.
- **The tunables page** applies values through the same owners.

## Collision, interaction and actions

- Voxel cells and the static ramp and block meshes are admitted into one
  SpatialSession, and Dynamics binds that same collision scene.
- Dynamic cubes reach the character as call-local `CharacterObstacle`
  values. The held cube is excluded, so carrying does not collide with
  itself. It is also excluded from what actions may strike.
- Engine WorldInteraction owns focus, reach, visibility eligibility and fresh
  use checks.
- Action resolution queries the same session, offering the cube colliders as
  the bodies that may be struck.

## Disposal

Dispose these in dependency order:
1. interaction constraints;
2. camera;
3. bodies and world;
4. voxel projection;
5. appearances;
6. mesh and material resources;
7. session.

## Build and known gap

The SDK emits bindings under ignored `obj/`. Ordinary CoreCLR builds use only
the pinned package and runtime. NativeAOT is optional and separately
evidenced.

Character/body contact momentum exchange is a missing C# Engine binding, not
implemented here. See [engine-contact-gap.md](engine-contact-gap.md).
