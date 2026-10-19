# Rusty FP Tester

A small first-person controller and action lab for Rusty Engine downstream projects,
and the **maintained first-person reference to copy from**: the body and camera, a
product-neutral melee/hitscan/projectile/area action model, world time controls and
developer tooling. [docs/reference.md](docs/reference.md) names each piece, what it
is for, where it came from and which products copied it. C# selects the test
geometry, controller tuning, actions and cube interaction rules. The pinned Engine
owns input, character collision (including jump buffering and coyote time),
rigid-body physics, rendering and the host.

## Run

Install the .NET 10 SDK and the Engine's `rusty` command, then:

```bash
rusty install
rusty dev --project src/FpTester.Game/FpTester.Game.csproj --port 8799 --live-debug
```

Open the URL printed by the host and click the scene to capture the mouse.

| Control | Action |
| --- | --- |
| WASD / left stick | Move |
| Mouse / right stick | Look |
| Space / gamepad A | Jump |
| Ctrl / gamepad B | Crouch |
| Shift / left stick click | Sprint |
| E / gamepad X | Pick up the focused cube; drop the held cube |
| Primary mouse / right trigger | Use the selected action; throw the held cube |
| 1–5 / Q | Choose the action (jab, heavy, shot, bolt, burst) / the next one |
| P / O | Hold or resume the world / step it one frame |
| R / HUD Reset lab button | Reset player, cubes and action counts |
| M | Pause menu (Engine lifecycle pause) |
| F3 | Developer shell (needs `--live-debug`) |
| Escape | Release pointer lock |

## Scene

Three connected voxel rooms share a level floor and open overhead space:

- **Slopes and clearance:** smooth 15°, 30°, 50° and 65° ramps, steps at 0.2,
  0.3, 0.4 and 0.6 metres, and a 1.5-metre-high crouch tunnel.
- **Jump course:** blocks from 0.5 to 2 metres high and raised voxel landings at
  1, 2 and 3 metres, separated by gaps.
- **Physics sandbox:** 0.5-, 1- and 1.5-metre dynamic cubes with different masses,
  plus a small stack. A second set near spawn makes pickup tests quick.

`content/lab.json` owns the scene, spawn, world gravity, masses and hold/throw
values; edits restage through `rusty dev`. `content/player.json` (body and camera
feel, including the Engine jump buffer and coyote seconds) and `content/actions.json`
(the actions) are live content: edits, and the developer shell's tunables panel,
apply without a restart. The HUD reports feet, velocity, stance, grounding, support,
contacts, blocking, jumps and landings, the action and its phase, hits and world
time. Struck cubes take the action's impulse. `R` resets the current lab without
rewinding Engine simulation time.

**Known Engine gap:** character collision with the cubes is available, but the
pinned C# bridge does not expose rigid-body contact push impulse proposals.
Walking into a cube therefore tests blocking, not physical pushing. There is
also no reciprocal rigid-body character collider in this integration. Pickup
uses an Engine fixed tether; throws and body/body/world contacts use Dynamics.
See [the narrow upstream request](docs/engine-contact-gap.md) before treating
this lab as evidence for bump-to-push behavior.

## Downstream workflow

`Directory.Build.props` contains the exact matched SDK/runtime pin. No sibling
Engine checkout is used to build or launch the product.

```bash
rusty status
rusty build --project src/FpTester.Game/FpTester.Game.csproj
rusty update --check
# Deliberate upgrade: read the listed release notes, then build and playtest.
rusty update
# Optional NativeAOT fidelity check:
rusty build --project src/FpTester.Game/FpTester.Game.csproj --aot
```

The inherited `engine-pair` GitHub workflow now targets this product and validates
its bootstrap identity before advancing the pin. Normal development uses CoreCLR.

## Playtesting

`.den-serve.json` and `scripts/serve-playtest.sh` allow crew-services to start a
private host per session. The local workstation profile is `rusty-fptester-hosted`:

```bash
playtest start rusty-fptester-hosted
playtest assist SESSION --json '{"op":"discover"}'
playtest assist SESSION --json '{"op":"time","mode":"action-driven"}'
playtest assist SESSION --json '{"op":"observe"}'
playtest assist SESSION --json '{"op":"act","id":"jump","capture":true}'
playtest stop SESSION
```

The runtime's generated debug catalog exposes `playtest.*`, `interaction.*`,
`tunables.*`, `inspect.targets`, `spatial.grid|probe|clearance`, `playtest.jump-plan`,
`entity.*` and the lab's own commands:

| Command | Does |
| --- | --- |
| `fptester.inspect` | Full observation: body, look, last step, Engine jump buffer/coyote remaining, body events, input readout, action and last impact, world time, cubes |
| `fptester.dev.view X Y Z YAW PITCH` | Stand with feet at a point (nearest clear, floored place within 2 m) facing a view, for repeatable captures |
| `fptester.time`, `.rate R`, `.hold`, `.run`, `.step S` | The lab's world time (GameplayTime): slow motion, hold, bounded step |
| `fptester.action.select ID` | Choose the primary action |

Show the action volumes without the shell with
`engine.inspect.overlay show set actions.volumes`. Observations include an
explicit `bumpPushSupported: false`. Target-ID interaction and relative debug
look are assistance; they do not establish unaided aiming. See
[architecture](docs/architecture.md), [donor evidence](docs/donor-consultation.md)
and [verification](docs/verification.md).
