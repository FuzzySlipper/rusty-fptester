using System.Numerics;
using System.Text.Json;
using FpTester.Game.Interaction;
using FpTester.Game.Player;
using FpTester.Game.Scene;
using FpTester.Game.Ui;
using Rusty.Engine;
using Rusty.Engine.Debugging;
using Rusty.Engine.Input;

namespace FpTester.Game;

public sealed class FpTesterProduct : IEngineProduct, IDebugCommandModuleSource
{
    private readonly IEngineContext engine;
    private readonly LabScene scene;
    private readonly PlayerController player;
    private readonly CubeInteraction cubes;
    private readonly UiStream hud;
    private ulong uiSequence;
    private ulong generation;
    private double sampleTime;
    private bool started;
    private bool paused;
    private bool disposed;
    private bool jumpPending;
    private bool usePending;
    private bool throwPending;

    public FpTesterProduct(ProductCreateContext context)
    {
        engine = context.Engine;
        scene = new LabScene(engine, LabDefinition.Load(engine));
        player = new PlayerController(engine, scene);
        cubes = new CubeInteraction(engine, scene, player);
        hud = engine.Ui.OpenStream(new UiStreamRequest("rusty-fptester", "rusty.fptester.hud"));
    }

    public void Start()
    {
        started = true;
        paused = false;
        Publish();
    }

    public ProductUpdateResult Update(ProductUpdate update)
    {
        if (!started || paused || disposed) return ProductUpdateResult.None;
        generation = update.Facts.Generation;
        bool resetRequested = false;
        foreach (ProductInputEvent input in update.Input)
        {
            if (input.Kind == InputEventKind.Clear) ClearPending();
            resetRequested |= input.Kind == InputEventKind.DirectDigital && input.Intent.Span.SequenceEqual("reset"u8) && input.X > .5f;
        }
        float delta = (float)update.Facts.FixedDeltaSeconds;
        FpsInputFrame inputFrame = player.ReadInput(update.Input, delta * update.Facts.AdmittedStepCount);
        if (resetRequested || player.Input.Physical.Pressed(KeyboardControl.KeyR))
        {
            Restart();
            inputFrame = default;
        }
        else
        {
            jumpPending |= inputFrame.JumpPressed;
            usePending |= inputFrame.UsePressed;
            throwPending |= player.Input.Physical.Pressed(PointerButton.Primary) || player.Input.Physical.Pressed(ControllerButton.Button7);
        }
        for (uint step = 0; step < update.Facts.AdmittedStepCount; step++)
        {
            scene.SimulationStep = checked(update.Facts.SimulationStep + step);
            player.Step(inputFrame, jumpPending, delta, cubes.Held?.Entity.Value);
            jumpPending = false;
            cubes.Interaction.Update();
            if (usePending) cubes.Use();
            if (throwPending) cubes.Throw();
            usePending = throwPending = false;
            scene.Step(delta, cubes.PrepareStep());
        }
        // Facts identify the first admitted step; the published state is at the end boundary.
        scene.SimulationStep = checked(update.Facts.SimulationStep + update.Facts.AdmittedStepCount);
        sampleTime = scene.SimulationStep * update.Facts.FixedDeltaSeconds;
        cubes.Interaction.Update();
        Publish();
        return ProductUpdateResult.None;
    }

    public void Pause() { paused = true; player.Input.Physical.Clear(); ClearPending(); }
    public void Resume() { paused = false; }
    public void Restart()
    {
        if (disposed) return;
        cubes.Reset();
        scene.Reset();
        player.Reset();
        ClearPending();
        paused = false;
        Publish();
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
        registrar.Register(new PlaytestDebugModule(Observe, Action,
            new[] { "forward", "back", "left", "right", "jump", "crouch", "sprint", "use", "throw", "reset" }, LookBy));
        registrar.Register(new InteractionDebugModule(cubes.Interaction));
    }

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
        "throw" => new(id, "Primary", 100, false, cubes.Held is not null, cubes.Held is null ? "No cube held" : null),
        "reset" => new(id, "KeyR", 100, false),
        _ => new(id, "", 0, false, false, "Unknown lab action")
    };

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
        return new LabObservation(new LabStamp(generation, scene.SimulationStep), paused ? "paused" : "playing",
            new LabPlayerFacts(Vec(player.Position), Vec(player.Feet), Vec(motion.ControlledVelocity + motion.ExternalVelocity),
                player.LookState.YawRadians * 180 / MathF.PI, player.LookState.PitchRadians * 180 / MathF.PI, motion.Grounded,
                motion.Stance.ToString(), motion.SupportEntityPresent ? motion.SupportEntity : null,
                receipt?.Contact.Kind.ToString() ?? "None", receipt?.Contact.Present == true && receipt.Value.Contact.SourceEntity != 0 ? receipt.Value.Contact.SourceEntity : null,
                receipt?.BlockFlags.ToString() ?? "None"),
            scene.Cubes.Select(cube => new LabCubeFacts(cube.Entity.Value, cube.Definition.Label, cube.Definition.Size, cube.Definition.Mass,
                Vec(scene.Pose(cube).Translation), Vec(scene.Motion(cube).LinearVelocity), scene.Motion(cube).Sleeping, cube == cubes.Held)).ToArray(),
            cubes.TargetLabel, cubes.Message, cubes.Held?.Entity.Value, cubes.Pickups, cubes.Throws, cubes.Drops, false);
    }

    private void Publish()
    {
        player.Publish(sampleTime);
        scene.Publish();
        engine.Ui.PublishProjection(new UiProjection(hud, ++uiSequence, LabHud.Create(Observation())));
    }

    private void ClearPending() => jumpPending = usePending = throwPending = false;
}
