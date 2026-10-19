using System.Text.Json.Serialization;

namespace FpTester.Game.Ui;

internal sealed record LabObservation(LabStamp Stamp, string Readiness, string WorldTime, LabPlayerFacts Player, LabActionFacts Action,
    LabCubeFacts[] Cubes, string Target, string Message, ulong? HeldCube, uint Pickups, uint Throws, uint Drops,
    bool BumpPushSupported, string Input, string Notice);
internal sealed record LabStamp(ulong Generation, ulong Step);
internal sealed record LabPlayerFacts(float[] Position, float[] Feet, float[] Eye, float[] Velocity, float YawDegrees, float PitchDegrees,
    bool Grounded, string Stance, ulong? SupportEntity, string Contact, ulong? ContactEntity, string BlockFlags,
    float JumpBufferRemaining, float CoyoteRemaining, uint Jumps, uint Landings, uint HardLandings, float LastLandingSpeed,
    uint Recoveries, string LastRecovery);
internal sealed record LabActionFacts(string Selected, string Delivery, string Phase, float PhaseProgress, string? Current,
    float Cooldown, uint Uses, uint Hits, uint Misses, LabImpactFacts? LastImpact, int ProjectilesInFlight);
internal sealed record LabImpactFacts(string Action, ulong? Target, bool Surface, float[] Point, float[] Start, float[] End, float Radius);
internal sealed record LabCubeFacts(ulong Id, string Label, float Size, float Mass, float[] Position, float[] Velocity, bool Sleeping, bool Held);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(LabObservation))]
internal sealed partial class ObservationJson : JsonSerializerContext;
