using System.Text.Json.Serialization;

namespace FpTester.Game.Ui;

internal sealed record LabObservation(LabStamp Stamp, string Readiness, LabPlayerFacts Player,
    LabCubeFacts[] Cubes, string Target, string Message, ulong? HeldCube, uint Pickups, uint Throws, uint Drops,
    bool BumpPushSupported);
internal sealed record LabStamp(ulong Generation, ulong Step);
internal sealed record LabPlayerFacts(float[] Position, float[] Feet, float[] Velocity, float YawDegrees, float PitchDegrees,
    bool Grounded, string Stance, ulong? SupportEntity, string Contact, ulong? ContactEntity, string BlockFlags);
internal sealed record LabCubeFacts(ulong Id, string Label, float Size, float Mass, float[] Position, float[] Velocity, bool Sleeping, bool Held);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(LabObservation))]
internal sealed partial class ObservationJson : JsonSerializerContext;
