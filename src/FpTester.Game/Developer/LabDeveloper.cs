using System.Numerics;
using System.Text.Json;
using FpTester.Game.Combat;
using FpTester.Game.Interaction;
using FpTester.Game.Player;
using FpTester.Game.Scene;
using FpTester.Game.Time;
using Rusty.Engine;
using Rusty.Engine.Debugging;

namespace FpTester.Game.Developer;

/// <summary>
/// The developer and agent access behind the generated command catalog: a repeatable viewpoint, world time, action
/// choice, inspection targets and the Engine's spatial readouts for the player's body. Every change goes through the
/// lab's owners; the readouts query the Engine and change nothing.
/// Provenance: rusty-hotel <c>HotelDeveloper.View</c> (<c>hotel.dev.view</c>, here with height and a clear-floor
/// search shared with recovery); rusty-doom RoomStudy <c>LoadingBayPlaytestSpatial</c> (grid, probe, jump plan and
/// clearance adapters).
/// </summary>
internal sealed class LabDeveloper(IEngineContext engine, LabScene scene, PlayerController player, CubeInteraction cubes,
    LabCombat combat, WorldTime time, Func<DebugCommandResult> observe, Action changed) : IDebugCommandModule
{
    private const float ProbeLimit = 8, ClearanceLimit = 8;
    // The most cells one grid readout answers.
    private const int GridCells = 8192;

    [DebugCommand("fptester.inspect", Description = "Read the lab: player body and look, last step, input, action, world time, cubes and counts.")]
    public DebugCommandResult Inspect() => observe();

    [DebugCommand("fptester.dev.view", Description = "Developer override: stand with feet at x y z facing yaw and pitch in degrees (0 yaw faces -Z, positive turns right), at the nearest clear, floored place within 2 m, for repeatable captures from one viewpoint.")]
    public DebugCommandResult View(float x, float y, float z, float yaw, float pitch)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z) || !float.IsFinite(yaw) || !float.IsFinite(pitch))
            return DebugCommandResult.Failure(DebugCommandStatus.InvalidArguments, "The point and angles must be finite numbers.");
        if (player.PlaceClear(new Vector3(x, y, z), yaw, pitch, scene.Colliders()) is null)
            return DebugCommandResult.Failure(DebugCommandStatus.InvalidArguments, FormattableString.Invariant($"No clear, floored place for the body near [{x}, {y}, {z}]."));
        cubes.Interaction.Update();
        changed();
        return observe();
    }

    [DebugCommand("fptester.time", Description = "Read the lab's world time: realtime, slow, held or stepping.")]
    public DebugCommandResult Time() => DebugCommandResult.Success(FormattableString.Invariant($"{time.Describe()}; rate {time.Rate:0.###}; step {scene.SimulationStep}"));

    [DebugCommand("fptester.time.rate", Description = "Run the world at RATE of realtime, above 0 and at most 1 (slow motion below 1). Look stays at full speed.")]
    public DebugCommandResult Rate(double rate) => Result(time.SetRate(rate));

    [DebugCommand("fptester.time.hold", Description = "Hold the world: no simulation steps, while look, camera and HUD keep updating.")]
    public DebugCommandResult Hold() { time.Hold(); return Time(); }

    [DebugCommand("fptester.time.run", Description = "Run the world again at the chosen rate.")]
    public DebugCommandResult Run() { time.Run(); return Time(); }

    [DebugCommand("fptester.time.step", Description = "Run SECONDS of world time (whole fixed steps, at the chosen rate), then hold.")]
    public DebugCommandResult Step(double seconds) => Result(time.Step(seconds));

    [DebugCommand("fptester.action.select", Description = "Choose the action the primary button uses, by id (see content/actions.json).")]
    public DebugCommandResult Select(string id)
    {
        int index = Array.FindIndex(combat.Catalog.Actions, a => a.Id == id);
        if (index < 0)
            return DebugCommandResult.Failure(DebugCommandStatus.InvalidArguments, $"Unknown action '{id}'. Actions: {string.Join(", ", combat.Catalog.Actions.Select(a => a.Id))}.");
        if (combat.User.Busy) return DebugCommandResult.Failure(DebugCommandStatus.InvalidArguments, "An action is under way.");
        combat.Select(index);
        changed();
        return DebugCommandResult.Success($"{combat.Action.Name} ({combat.Action.Delivery.Kind}) selected.");
    }

    /// <summary>
    /// The lab's named inspection points (Engine <see cref="InspectionTargetsDebugModule"/>): the eye, the selected
    /// action's reach in camera space (so an inspection view frames the swing volume as the player turns), the last
    /// impact, the held cube and the spawn.
    /// </summary>
    internal IReadOnlyList<InspectionTarget> Targets()
    {
        float reach = Math.Max(.5f, combat.Action.Delivery.Range);
        List<InspectionTarget> targets =
        [
            new("eye", OverlayPlace.World, player.Eye) { Radius = 1, Framing = "side" },
            new("reach", OverlayPlace.Camera, new Vector3(0, 0, -reach / 2)) { Radius = reach / 2 + combat.Action.Delivery.Width + .5f, Framing = "side" },
            new("spawn", OverlayPlace.World, LabDefinition.Vector(scene.Definition.SpawnFeet)) { Radius = 2, Framing = "top" },
        ];
        if (combat.LastImpact is { } impact) targets.Add(new("last-impact", OverlayPlace.World, impact.Point) { Radius = 1.5f, Framing = "side" });
        if (cubes.Held is { } held) targets.Add(new("held-cube", OverlayPlace.Model(held.Entity.Value), Vector3.Zero) { Radius = held.Definition.Size + .5f, Framing = "front" });
        return targets;
    }

    internal DebugCommandResult Grid(int radius, int verticalRadius, double cellSize)
    {
        if (radius is < 0 or > 15 || verticalRadius is < 0 or > 15 || !double.IsFinite(cellSize) || cellSize < .125 || cellSize > 2 ||
            (2 * radius + 1) * (2 * radius + 1) * (2 * verticalRadius + 1) > GridCells)
            return DebugCommandResult.Failure(DebugCommandStatus.InvalidArguments, $"Grid: radii 0..15, cell size .125..2, at most {GridCells} cells.");
        Vector3 origin = player.Feet - new Vector3((float)((radius + .5) * cellSize), (float)((verticalRadius + .5) * cellSize), (float)((radius + .5) * cellSize));
        uint size = (uint)(2 * radius + 1);
        SpatialMapRequest request = new(scene.Session, origin, cellSize, size, size, origin.Y, origin.Y + cellSize, origin.Y, origin.Y + cellSize, scene.Colliders());
        return DebugCommandResult.Success(SpatialGridSnapshot.Capture(engine.Spatial, request, (uint)(2 * verticalRadius + 1),
            new SpatialMapObservation($"step:{scene.SimulationStep}", player.Position, player.Forward)));
    }

    internal DebugCommandResult Probe(double distance)
    {
        if (!double.IsFinite(distance) || distance <= 0 || distance > ProbeLimit)
            return DebugCommandResult.Failure(DebugCommandStatus.InvalidArguments, $"Probe distance must be in (0, {ProbeLimit}].");
        using JsonDocument rays = JsonDocument.Parse(PlaytestTraversal.Probe(engine.Spatial, scene.Session, player.Feet, player.Height,
            player.Tuning.MaximumStepHeight, (float)distance, scene.Colliders()));
        return DebugCommandResult.Success(JsonSerializer.Serialize(new ProbeReadout(scene.SimulationStep, LastStep(), rays.RootElement), DeveloperJson.Default.ProbeReadout));
    }

    internal DebugCommandResult Jump(double x, double y, double z)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z))
            return DebugCommandResult.Failure(DebugCommandStatus.InvalidArguments, "Target feet must be finite world XYZ.");
        PlaytestJumpPlan plan = PlaytestTraversal.JumpToward(player.Feet, player.Forward, new Vector3((float)x, (float)y, (float)z),
            player.Config, player.Motion.Grounded, "Space", "KeyW");
        return DebugCommandResult.Success(JsonSerializer.Serialize(plan, DeveloperJson.Default.PlaytestJumpPlan));
    }

    internal DebugCommandResult Clearance(double x, double y, double z)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z))
            return DebugCommandResult.Failure(DebugCommandStatus.InvalidArguments, "Target feet must be finite world XYZ.");
        Vector3 target = new((float)x, (float)y, (float)z);
        if (Vector3.Distance(player.Feet, target) > ClearanceLimit)
            return DebugCommandResult.Failure(DebugCommandStatus.InvalidArguments, $"Clearance target must be within {ClearanceLimit} m of the player's feet.");
        return DebugCommandResult.Success(SpatialClearanceSnapshot.Capture(engine.Spatial, scene.Session, player.Position, target,
            player.Height, player.Config, scene.Colliders()));
    }

    private string LastStep() => player.LastStep is not { } r ? "none" : FormattableString.Invariant(
        $"attempted={r.Step.Attempted};accepted={r.Step.Accepted};wish={r.WishVelocity.X:F3},{r.WishVelocity.Y:F3},{r.WishVelocity.Z:F3};displacement={r.Displacement.X:F3},{r.Displacement.Y:F3},{r.Displacement.Z:F3};blocked={r.BlockFlags};contact={r.Contact.Kind};casts={r.CastCount}");

    private DebugCommandResult Result(string? refusal)
    {
        if (refusal is not null) return DebugCommandResult.Failure(DebugCommandStatus.InvalidArguments, refusal);
        changed();
        return Time();
    }
}

internal sealed record ProbeReadout(ulong Step, string LastStep, JsonElement Rays);

[System.Text.Json.Serialization.JsonSourceGenerationOptions(PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase)]
[System.Text.Json.Serialization.JsonSerializable(typeof(ProbeReadout))]
[System.Text.Json.Serialization.JsonSerializable(typeof(PlaytestJumpPlan))]
internal sealed partial class DeveloperJson : System.Text.Json.Serialization.JsonSerializerContext;
