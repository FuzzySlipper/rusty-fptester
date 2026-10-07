# Engine request: C# character / rigid-body contact response

## Blocked behavior

The lab needs ordinary first-person movement to push a dynamic cube when the
character capsule contacts it, and to receive the corresponding body response.
Body/body and body/world physics, capsule blocking, pickup and throwing are
independently available.

## Verified gap

At the SDK/runtime pair pinned in `Directory.Build.props`, safe `CharacterStepRequest`
accepts CharacterObstacle poses/velocities; CharacterStepReceipt and
ReadCharacterController report contacts, but expose no dynamic contact impulses
or retained-body associations. The matching Engine bridge's CharacterCallWorld
returns None from rigid_body_linear_velocity. The internal Engine controller has
`dynamic_impulse_proposals`, but those proposals are not available to this C# product.
This is a missing binding/integration mechanism, not a downstream helper gap.

Checked the public installed assembly's CharacterStepRequest, CharacterStepReceipt,
CharacterControllerResult, IDynamicsService and CharacterExternalMotionConfig.
Also inspected `rust/crates/csharp-engine-services/src/spatial.rs` and
`rust/crates/engine-spatial/src/character_controller.rs` at the pinned revision.
The current sibling Engine source has the same missing body association; it was
read-only evidence, never a build dependency.

## Narrow request

**Owner:** Engine Spatial character bridge and Dynamics integration.

Expose safe, call-local character/body associations and Engine-computed contact
reaction proposals that a downstream can apply once during its already-admitted
Dynamics step. Preserve actual body identity, contact point, mass/inertia response
and axis locks; expose current refusal/eligibility facts. Keep product pushiness
and action ordering downstream. Do not require a second character, physics loop,
handwritten ABI or manually reconstructed collision response.

Acceptance: the same dynamic cuboid can be pushed by normal C# character input,
with Engine-owned contacts/impulses and no teleportation, fake kinematic body,
local solver or diagnostic-only behavior. Include a focused C# fixture, and verify
this lab's ordinary controls on the published matched pair.

No external Engine task has been filed by this change. This document is the
request-ready handoff; no bump-to-push acceptance is claimed. The HUD and live
observation advertise the limitation explicitly.
