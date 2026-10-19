using System.Numerics;
using System.Text.Json;
using FpTester.Game.Actions;
using FpTester.Game.Combat;
using FpTester.Game.Developer;
using FpTester.Game.Interaction;
using FpTester.Game.Player;
using FpTester.Game.Scene;
using FpTester.Game.Time;
using FpTester.Game.Ui;
using Rusty.Engine;
using Rusty.Engine.Debugging;
using Rusty.Engine.Entities;
using Rusty.Engine.Input;

namespace FpTester.Game;

/// <summary>
/// The product entry: lifecycle, and each update's composition of input, the player's body, pickup, actions, loose
/// cubes, world time and what is published. Every update reads input, turns the view and publishes the camera; the
/// world advances only per Engine-admitted step.
/// </summary>
public sealed class FpTesterProduct : IEngineProduct, IDebugCommandModuleSource
{
    private static readonly KeyboardControl[] ActionKeys =
        [KeyboardControl.Digit1, KeyboardControl.Digit2, KeyboardControl.Digit3, KeyboardControl.Digit4, KeyboardControl.Digit5];

    private readonly IEngineContext engine;
    private readonly LabScene scene;
    private readonly PlayerController player;
    private readonly CubeInteraction cubes;
    private readonly LabCombat combat;
    private readonly WorldTime time;
    private readonly InputDiagnostics diagnostics = new();
    private readonly LabDeveloper developer;
    private readonly UiStream hud;
    private ulong generation;
    private double worldSeconds;
    private string notice = "";
    private bool started;
    private bool paused;
    private bool disposed;
    private bool jumpPending;
    private bool usePending;
    private bool primaryPending;

    public FpTesterProduct(ProductCreateContext context)
    {
        engine = context.Engine;
        scene = new LabScene(engine, LabDefinition.Load(engine));
        player = new PlayerController(engine, scene.Session, scene.Entities, scene.PlayerEntity, PlayerTuning.Load(engine));
        cubes = new CubeInteraction(engine, scene, player);
        combat = new LabCombat(engine, scene.Session, ActionCatalog.Load(engine));
        time = new WorldTime(engine);
        developer = new LabDeveloper(engine, scene, player, cubes, combat, time, Observe, Publish);
        hud = engine.Ui.OpenStream(new UiStreamRequest("rusty-fptester", "rusty.fptester.hud"));
        PlaceAtSpawn();
    }

    public void Start()
    {
        started = true;
        paused = false;
        time.Apply(); // selecting gameplay time makes every host observation an update, admitted steps or not
        Publish();
    }

    public ProductUpdateResult Update(ProductUpdate update)
    {
        if (!started || paused || disposed) return ProductUpdateResult.None;
        ProductUpdateFacts facts = update.Facts;
        generation = facts.Generation;
        time.Observe(facts);
        diagnostics.Capture(update.Input, facts.SimulationStep);
        ReloadLive(update.ContentChanges);
        bool resetRequested = false;
        foreach (ProductInputEvent input in update.Input)
        {
            if (input.Kind == InputEventKind.Clear) ClearPending();
            resetRequested |= ResetClaimed(input);
        }
        // Look and controls on host time, so they keep their speed while the world holds or slows; the body per step.
        FpsInputFrame frame = player.ReadInput(update.Input, (float)facts.HostElapsedSeconds);
        PhysicalInputState physical = player.Input.Physical;
        if (resetRequested || physical.Pressed(KeyboardControl.KeyR))
        {
            ResetLab();
            frame = default;
        }
        else
        {
            jumpPending |= frame.JumpPressed;
            usePending |= frame.UsePressed;
            primaryPending |= physical.Pressed(PointerButton.Primary) || physical.Pressed(ControllerButton.Button7);
            for (int i = 0; i < ActionKeys.Length; i++) if (physical.Pressed(ActionKeys[i])) combat.Select(i);
            if (physical.Pressed(KeyboardControl.KeyQ)) combat.SelectNext();
            if (physical.Pressed(KeyboardControl.KeyP)) time.Toggle();
            if (physical.Pressed(KeyboardControl.KeyO)) time.Step(WorldTime.StepSeconds);
        }
        float delta = (float)facts.FixedDeltaSeconds;
        for (uint step = 0; step < facts.AdmittedStepCount; step++)
        {
            scene.SimulationStep = checked(facts.SimulationStep + step);
            ulong? held = cubes.Held?.Entity.Value;
            ReadOnlyMemory<SpatialEntityCollider> bodies = scene.Colliders();
            player.Step(frame, jumpPending, delta, scene.Obstacles(held), scene.Support(player.Motion, held), bodies, combat.MovementScale);
            jumpPending = false;
            cubes.Interaction.Update();
            if (usePending) cubes.Use();
            if (primaryPending)
            {
                if (cubes.Held is not null) cubes.Throw();
                else combat.Use(player.Forward);
            }
            usePending = primaryPending = false;
            SpatialEntityCollider[] strikable = bodies.ToArray().Where(b => b.Entity != cubes.Held?.Entity.Value).ToArray();
            DynamicsEntityAction[] pushes = combat.Step(delta, scene.PlayerEntity.Value, player.Eye, strikable, scene.CubeEntity);
            DynamicsEntityAction[] actions = [.. cubes.PrepareStep(), .. pushes];
            scene.Step(delta, actions);
        }
        // Facts identify the first admitted step; the published state is at the end boundary.
        scene.SimulationStep = checked(facts.SimulationStep + facts.AdmittedStepCount);
        worldSeconds = scene.SimulationStep * facts.FixedDeltaSeconds;
        cubes.Interaction.Update();
        combat.Present(facts.HostElapsedSeconds, player.Eye);
        Publish();
        return ProductUpdateResult.None;
    }

    /// <summary>A reset claimed from a paused menu applies at once.</summary>
    public void HandlePausedIntents(ReadOnlySpan<ProductInputEvent> intents)
    {
        if (disposed) return;
        foreach (ProductInputEvent input in intents)
            if (ResetClaimed(input)) { ResetLab(); return; }
    }

    public void Pause() { paused = true; player.Input.Physical.Clear(); ClearPending(); }
    public void Resume() { paused = false; player.Input.Physical.Clear(); ClearPending(); }

    /// <summary>An Engine restart returns time to realtime; the lab resets and selects its world time again.</summary>
    public void Restart()
    {
        if (disposed) return;
        ResetLab();
        paused = false;
        time.Apply();
    }

    public void Shutdown() => Dispose();

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        cubes.Dispose();
        player.Dispose();
        scene.Dispose();
        hud.Dispose();
    }

    public void RegisterDebugCommands(IDebugCommandModuleRegistrar registrar)
    {
        registrar.Register(new PlaytestDebugModule(Observe, Action, PlaytestActions, LookBy));
        registrar.Register(new InteractionDebugModule(cubes.Interaction));
        registrar.Register(developer);
        registrar.Register(new LabTunables(engine.ContentSource, player, combat, time).Module());
        registrar.Register(new InspectionTargetsDebugModule(engine.Graphics, developer.Targets));
        registrar.Register(new SpatialInspectionDebugModule(developer.Grid, developer.Probe, developer.Jump));
        registrar.Register(new SpatialClearanceDebugModule(developer.Clearance));
        EntityStoreDebugModule entities = new();
        entities.RegisterProjection(EngineComponentTypes.CharacterMotion, static (in CharacterMotion m) => FormattableString.Invariant(
            $"grounded={m.Grounded};stance={m.Stance};velocity={m.ControlledVelocity.X:F2},{m.ControlledVelocity.Y:F2},{m.ControlledVelocity.Z:F2};jumpBuffer={m.JumpBufferRemaining:F3};coyote={m.CoyoteRemaining:F3}"));
        entities.RegisterStore("lab", scene.Entities);
        registrar.Register(entities);
    }

    private static readonly string[] PlaytestActions =
        ["forward", "back", "left", "right", "jump", "crouch", "sprint", "use", "attack", "throw", "next-action",
         "action-1", "action-2", "action-3", "action-4", "action-5", "hold-world", "step-world", "reset"];

    private PlaytestAction Action(string id) => id switch
    {
        "forward" => new(id, "KeyW", 200, true),
        "back" => new(id, "KeyS", 200, true),
        "left" => new(id, "KeyA", 200, true),
        "right" => new(id, "KeyD", 200, true),
        "jump" => new(id, "Space", 150, false),
        "crouch" => new(id, "ControlLeft", 200, true),
        "sprint" => new(id, "ShiftLeft", 200, true, HeldKeys: new[] { "KeyW" }),
        "use" => new(id, "KeyE", 100, false),
        // Long enough to see the whole action land and recover on realtime.
        "attack" => new(id, "Primary", Math.Ceiling(combat.Action.Duration * 1000) + 100, false, cubes.Held is null && !combat.User.Busy,
            cubes.Held is not null ? "Holding a cube: primary throws it" : combat.User.Busy ? "An action is under way" : null, combat.Action.Id),
        "throw" => new(id, "Primary", 100, false, cubes.Held is not null, cubes.Held is null ? "No cube held" : null),
        "next-action" => new(id, "KeyQ", 100, false),
        "action-1" or "action-2" or "action-3" or "action-4" or "action-5" => Slot(id, id[^1] - '1'),
        "hold-world" => new(id, "KeyP", 100, false),
        "step-world" => new(id, "KeyO", 100, false),
        "reset" => new(id, "KeyR", 100, false),
        _ => new(id, "", 0, false, false, "Unknown lab action")
    };

    private PlaytestAction Slot(string id, int index) => index < combat.Catalog.Actions.Length
        ? new(id, $"Digit{index + 1}", 100, false, Equipment: combat.Catalog.Actions[index].Id)
        : new(id, $"Digit{index + 1}", 100, false, false, "No action in that slot");

    private DebugCommandResult LookBy(double yaw, double pitch)
    {
        if (!double.IsFinite(yaw) || !double.IsFinite(pitch) || Math.Abs(yaw) > 360 || Math.Abs(pitch) > 180)
            return DebugCommandResult.Failure(DebugCommandStatus.InvalidArguments, "Look degrees exceed bounds.");
        player.LookBy(yaw, pitch);
        cubes.Interaction.Update();
        Publish();
        return Observe();
    }

    private DebugCommandResult Observe() => DebugCommandResult.Success(JsonSerializer.Serialize(Observation(), ObservationJson.Default.LabObservation));

    private LabObservation Observation()
    {
        static float[] Vec(Vector3 value) => [value.X, value.Y, value.Z];
        CharacterMotion motion = player.Motion;
        CharacterStepReceipt? receipt = player.LastStep;
        BodyEvents body = player.Body;
        ActionUser user = combat.User;
        LabImpactFacts? impact = combat.LastImpact is { } i
            ? new(i.Action.Id, i.Target, i.Surface, Vec(i.Point), Vec(i.Start), Vec(i.End), i.Radius) : null;
        return new LabObservation(new LabStamp(generation, scene.SimulationStep), paused ? "paused" : "playing", time.Describe(),
            new LabPlayerFacts(Vec(player.Position), Vec(player.Feet), Vec(player.Eye), Vec(motion.ControlledVelocity + motion.ExternalVelocity),
                player.LookState.YawRadians * 180 / MathF.PI, player.LookState.PitchRadians * 180 / MathF.PI, motion.Grounded,
                motion.Stance.ToString(), motion.SupportEntityPresent ? motion.SupportEntity : null,
                receipt?.Contact.Kind.ToString() ?? "None", receipt?.Contact.Present == true && receipt.Value.Contact.SourceEntity != 0 ? receipt.Value.Contact.SourceEntity : null,
                receipt?.BlockFlags.ToString() ?? "None", motion.JumpBufferRemaining, motion.CoyoteRemaining,
                body.Jumps, body.Landings, body.HardLandings, body.LastLandingSpeed, player.Recoveries, player.LastRecovery),
            new LabActionFacts(combat.Action.Id, combat.Action.Delivery.Kind.ToString(), user.Phase.ToString(), user.PhaseProgress,
                user.Current?.Id, user.Cooldown(combat.Action.Id), combat.Uses, combat.Hits, combat.Misses, impact, combat.Projectiles.Count),
            scene.Cubes.Select(cube => new LabCubeFacts(cube.Entity.Value, cube.Definition.Label, cube.Definition.Size, cube.Definition.Mass,
                Vec(scene.Pose(cube).Translation), Vec(scene.Motion(cube).LinearVelocity), scene.Motion(cube).Sleeping, cube == cubes.Held)).ToArray(),
            cubes.TargetLabel, combat.User.Busy || cubes.Held is null && combat.Message.Length > 0 ? combat.Message : cubes.Message,
            cubes.Held?.Entity.Value, cubes.Pickups, cubes.Throws, cubes.Drops, false, diagnostics.Readout(), notice);
    }

    private void Publish()
    {
        player.Publish(worldSeconds);
        scene.Publish();
        engine.Ui.PublishProjection(new UiProjection(hud, LabHud.Create(Observation())));
    }

    // Under rusty dev an edited (or tunables-saved) live file arrives here instead of a restage: read it again,
    // validated as at load, and hand it to its owner. A file that does not load is reported and the last values kept.
    private void ReloadLive(ReadOnlySpan<ContentReferenceInfo> changes)
    {
        foreach (ContentReferenceInfo change in changes)
        {
            try
            {
                if (change.Path == PlayerTuning.Path) player.Retune(PlayerTuning.Load(engine));
                else if (change.Path == ActionCatalog.Path) combat.Retune(ActionCatalog.Load(engine));
                else continue;
                notice = $"Reloaded {change.Path}";
            }
            catch (Exception error) when (error is InvalidOperationException or JsonException or EngineCallException)
            {
                notice = $"Not reloaded: {error.Message}";
                Console.Error.WriteLine(notice);
            }
        }
    }

    private static bool ResetClaimed(ProductInputEvent input) =>
        input.Kind == InputEventKind.DirectDigital && input.Intent.Span.SequenceEqual("reset"u8) && input.X > .5f;

    private void ResetLab()
    {
        cubes.Reset();
        scene.Reset();
        combat.Reset();
        PlaceAtSpawn();
        player.ResetCounters();
        ClearPending();
        Publish();
    }

    private void PlaceAtSpawn()
    {
        Vector3 feet = LabDefinition.Vector(scene.Definition.SpawnFeet);
        player.Place(feet + Vector3.UnitY * (player.Tuning.Height / 2 + player.Config.Shape.ContactSkin), scene.Definition.SpawnYawDegrees);
    }

    private void ClearPending() => jumpPending = usePending = primaryPending = false;
}
