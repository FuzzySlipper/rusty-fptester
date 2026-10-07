using System.Numerics;
using FpTester.Game.Scene;
using Rusty.Engine;
using Rusty.Engine.Entities;
using Rusty.Engine.Input;

namespace FpTester.Game.Player;

internal sealed class PlayerController : IDisposable
{
    private readonly IEngineContext engine;
    private readonly LabScene scene;
    private readonly PlayerTuning tuning;
    private readonly CharacterControllerConfig config;
    private readonly Camera camera;
    private ulong commandSequence;
    private bool cut = true;

    internal PlayerController(IEngineContext engine, LabScene scene)
    {
        this.engine = engine;
        this.scene = scene;
        tuning = scene.Definition.Player;
        config = Configure(engine.Spatial.DefaultCharacterControllerConfig());
        engine.Spatial.ValidateCharacterControllerConfig(config);
        Input = new FpsInput(FpsInputConfig.Standard with
        {
            PointerLookConfig = FpsInputConfig.Standard.PointerLookConfig with
            { HorizontalRadiansPerUnit = tuning.PointerRadiansPerUnit, VerticalRadiansPerUnit = tuning.PointerRadiansPerUnit }
        });
        Reset();
        camera = engine.CameraView.CreateCamera(Descriptor());
        engine.CameraView.SetActiveCamera(camera);
    }

    internal FpsInput Input { get; }
    internal LookState LookState { get; private set; }
    internal Vector3 Forward => Look.IntegrateClamped(new LookRequest(LookState, Vector2.Zero, Input.Config.PointerLookConfig)).Forward;
    internal Vector3 Position => scene.Entities.Get(scene.PlayerEntity, EngineComponentTypes.Transform).Translation;
    internal CharacterMotion Motion => scene.Entities.Get(scene.PlayerEntity, EngineComponentTypes.CharacterMotion);
    internal float Height => Motion.Stance == CharacterStance.Crouched ? tuning.CrouchedHeight : tuning.Height;
    internal Vector3 Feet => Position - Vector3.UnitY * (Height / 2);
    internal Vector3 Eye => Position + Vector3.UnitY * (Height / 2 - .15f);
    internal CharacterStepReceipt? LastStep { get; private set; }
    internal CharacterControllerConfig Config => config;

    internal FpsInputFrame ReadInput(ReadOnlySpan<ProductInputEvent> events, float admittedSeconds)
    {
        FpsInputFrame frame = Input.Consume(events, admittedSeconds);
        LookState = Input.IntegrateLook(LookState, frame).After;
        return frame;
    }

    internal void Step(FpsInputFrame input, bool jumpPressed, float delta, ulong? heldCube)
    {
        CharacterMotion motion = Motion;
        CharacterSupport support = default;
        if (motion.SupportEntityPresent)
        {
            LabCube? supported = scene.Cubes.Find(cube => cube.Entity.Value == motion.SupportEntity && cube.Entity.Value != heldCube);
            support = supported is null
                ? new CharacterSupport(true, CharacterSupportLifecycle.Destroyed, motion.SupportEntity, default)
                : new CharacterSupport(true, CharacterSupportLifecycle.Active, motion.SupportEntity, scene.Pose(supported));
        }
        CharacterControllerConfig stepConfig = input.SprintHeld && !input.CrouchHeld ? config with
        {
            Ground = config.Ground with { ForwardSpeed = tuning.SprintSpeed, BackwardSpeed = tuning.SprintSpeed, StrafeSpeed = tuning.SprintSpeed }
        } : config;
        CharacterStepReceipt receipt = engine.Spatial.ProposeCharacterStep(new CharacterStepRequest(scene.Session, Position, motion,
            support, scene.Obstacles(heldCube), ReadOnlyMemory<CharacterMeshInstance>.Empty, stepConfig,
            new CharacterControllerCommand(input.Movement, LookState.YawRadians, jumpPressed, input.JumpHeld, input.CrouchHeld,
                Vector3.Zero, Vector3.Zero, delta, ++commandSequence)));
        scene.Entities.Set(scene.PlayerEntity, EngineComponentTypes.Transform, receipt.Transform);
        scene.Entities.Set(scene.PlayerEntity, EngineComponentTypes.CharacterMotion, receipt.Motion);
        LastStep = receipt;
    }

    internal void Reset()
    {
        Vector3 position = LabDefinition.Vector(scene.Definition.Spawn);
        scene.Entities.Set(scene.PlayerEntity, EngineComponentTypes.Transform, new Transform(position, Quaternion.Identity, Vector3.One));
        scene.Entities.Set(scene.PlayerEntity, EngineComponentTypes.CharacterMotion, new CharacterMotion(Vector3.Zero, Vector3.Zero,
            false, CharacterStance.Standing, 0, 0, 0, false, 0, Vector3.Zero, Vector3.Zero, Quaternion.Identity, Vector3.Zero, position.Y, position.Y, 0, 0));
        LookState = new LookState(scene.Definition.SpawnYawDegrees * MathF.PI / 180, 0);
        Input.Physical.Clear();
        LastStep = null;
        cut = true;
    }

    internal void LookBy(double yawDegrees, double pitchDegrees)
    {
        LookState = Look.IntegrateClamped(new LookRequest(LookState,
            new Vector2((float)(yawDegrees * Math.PI / 180), (float)(pitchDegrees * Math.PI / 180)),
            Input.Config.PointerLookConfig with { HorizontalRadiansPerUnit = 1, VerticalRadiansPerUnit = 1, InvertVertical = false })).After;
    }

    internal void Publish(double sampleTime)
    {
        engine.CameraView.UpdateCameraSample(new CameraSampleRequest(camera, Descriptor(), sampleTime, 1d / 60,
            CameraInterpolation.Position, cut ? (byte)1 : (byte)0));
        cut = false;
    }

    public void Dispose()
    {
        engine.CameraView.ClearActiveCamera(new ClearActiveCameraRequest(0));
        camera.Dispose();
    }

    private CameraDescriptor Descriptor() => new(new CameraPose(Eye, LookState.PitchRadians * 180 / Math.PI, LookState.YawRadians * 180 / Math.PI),
        CameraBasisMode.Derived, default, new CameraProjection(CameraProjectionKind.Perspective, 75, 0, .05, 120), new CameraViewport(0, 0, 1, 1));

    private CharacterControllerConfig Configure(CharacterControllerConfig baseline) => baseline with
    {
        Shape = baseline.Shape with { StandingHeight = tuning.Height, CrouchedHeight = tuning.CrouchedHeight, Radius = tuning.Radius },
        Ground = baseline.Ground with { ForwardSpeed = tuning.Speed, BackwardSpeed = tuning.Speed, StrafeSpeed = tuning.Speed },
        Air = baseline.Air with { MaximumSpeed = tuning.Speed, WishSpeedCap = tuning.Speed },
        Vertical = baseline.Vertical with { Gravity = tuning.Gravity, JumpSpeed = tuning.JumpSpeed },
        Surface = baseline.Surface with { MaximumStepHeight = tuning.MaximumStepHeight, MaximumSlopeRadians = tuning.MaximumSlopeDegrees * MathF.PI / 180 }
    };
}
