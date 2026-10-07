# Product architecture

> The product decides. The Engine guarantees.

| Owner | Responsibility |
| --- | --- |
| `src/FpTester.Game/FpTesterProduct.cs` | Explicit composition, lifecycle, pending actions and Engine-admitted step order |
| `Scene/LabDefinition.cs`, `content/lab.json` | Typed authored geometry and adjustable values, read through Engine Content |
| `Scene/LabScene.cs` | Session, voxel admission, static fixtures, dynamic body/entity bindings and presentation lifetimes |
| `Scene/LabGeometry.cs` | Authored triangular-prism and box geometry; no collision solver or renderer |
| `Player/PlayerController.cs` | Controller configuration, look policy and accepted character continuation in Engine EntityStore |
| `Interaction/CubeInteraction.cs` | Pickup/drop/throw policy, selected body and Engine tether lifetime |
| `Ui/` | Typed observation and HUD projection layout |
| `src/ui/main.js` | DOM telemetry, instructions and reset intent |
| Engine SDK/runtime | FpsInput/PhysicalInputState, Look, admitted time, controller, collision, Dynamics, entities, content, rendering, camera, UI transport and host |

## One update path

Each callback consumes Engine input through `FpsInput`. Pointer look remains free
on zero-step updates; controller look uses admitted simulation seconds. Press
edges remain pending until an admitted step and Clear/Pause/Reset cancels them.
For every Engine-admitted fixed step:

1. Propose the character step using current dynamic cube poses/velocities and
   Engine support continuation; retain only the accepted transform and motion.
2. Refresh Engine WorldInteraction focus; apply pending pickup, drop or throw.
3. Update the held body's Engine fixed tether, apply a pending launch impulse,
   and step/read Dynamics through EntityDynamicsAdapter once.

Engine EntityStore holds player continuation and the body readout projection;
Dynamics remains authoritative for loose cubes. Scene fixtures remain immutable
between reloads. Camera samples, graphics facts and the UI derive from those
owners. There is no downstream accumulator, second clock, collision solver,
input transport, browser renderer or handwritten interop.

Voxel cells and static ramp/block meshes are admitted into one SpatialSession.
Dynamics binds that same collision scene. Dynamic cubes reach the character as
call-local CharacterObstacle values; Engine applies current support translation
and rotation through CharacterSupport. The held cube is excluded from character
obstacles to avoid self-collision during carrying. The body remains dynamic and
collides with the world and other cubes while tethered.

Engine WorldInteraction owns focus, reach, visibility eligibility and fresh use
checks. Product facts label cubes and mark them unavailable while holding one.
Engine InteractionVisibilityQuery uses the current collider projection, ignoring
the target body's own collider. A reset changes interaction incarnation so old
target observations cannot activate a reset cube.

Dispose interaction constraints, camera, bodies/world, voxel projection,
appearances, mesh/material resources and session in dependency order. The SDK
emits bindings under ignored `obj/`; ordinary CoreCLR builds use only the pinned
package/runtime. NativeAOT is optional and separately evidenced.

Character/body contact momentum exchange is a missing C# Engine binding, not
implemented here. See [engine-contact-gap.md](engine-contact-gap.md).
