# Donor consultation

- **Doom corpus:** `/home/agent/dev/rusty-doom`, inspected checkout
  `fa023f577a21ec32d0f43ced8ea467b0926f2540`.
  `LoadingBayEngineServices.cs` (player Update, ApplyInput, ResolvePlatformSupport),
  `LoadingBayPlayerInput.cs`, `LoadingBayTuning.cs`, and room-study playtest adapter.
- **CraftSurvive corpus:** `/home/agent/dev/rusty-craftsurvive`, inspected checkout
  `adb5cc0e5526c2a8535da2692b29b7320add2c3d`.
  `Modules/Player/PlayerController.cs`, PlayerBody, PlayerCamera and PlayerInputState;
  DungeonCollision, TerrainPresentation and ProductUiProjection.
- **Searches:** FirstPerson, CharacterStep, CharacterMotion, CharacterSupport,
  Kinematic, Dynamics, VoxelScenePresentation, pickup/throw and PlaytestDebugModule.
- **Outcome:** adapted. Adopted Engine-admitted fixed-step character proposals,
  accepted motion continuation, Engine camera/look and voxel collision/presentation.
  Used the newer shared Engine FpsInput helper rather than copying CraftSurvive's
  physical key state/deadzone implementation. Used WorldInteraction for use eligibility
  and Engine Dynamics/EntityDynamicsAdapter for loose bodies.
- **Deviations:** no Doom gameplay, imported maps, persistence, survival systems,
  streaming world, donor-specific cadence or controller accumulator. Fixed test
  geometry and tuning live in this product's content. Pickup uses the Engine's
  fixed tether, not a pose teleport or local physics controller.
- **Primary verification:** inspected the installed SDK assembly at the executable
  pin using reflection in a temporary tool outside the repository; consulted the
  pinned Engine revision for collision/contact bridge behavior. No adjacent source
  checkout is a build dependency and no adjacent repository was modified.
- **Index limitation:** no Codebase Memory MCP was available; inspected exact local
  donor files with direct search and reads instead.
