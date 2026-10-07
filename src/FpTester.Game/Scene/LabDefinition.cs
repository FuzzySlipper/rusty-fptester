using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rusty.Engine;

namespace FpTester.Game.Scene;

internal sealed record LabDefinition(float VoxelSize, float[] Spawn, float SpawnYawDegrees,
    PlayerTuning Player, InteractionTuning Interaction, VolumeDefinition[] Volumes,
    RampDefinition[] Ramps, BlockDefinition[] Blocks, CubeDefinition[] Cubes)
{
    internal static LabDefinition Load(IEngineContext engine)
    {
        using ContentReference content = engine.Content.OpenReference(new ContentOpenRequest("lab.json"));
        ulong length = engine.Content.ReadReferenceInfo(content).Span[0].ByteLength;
        ReadOnlyMemory<byte> bytes = engine.Content.ReadBytes(new ContentReadBytesRequest(content, 0, checked((uint)length)));
        return JsonSerializer.Deserialize(bytes.Span, LabJson.Default.LabDefinition)
            ?? throw new InvalidOperationException("lab.json must contain the lab definition.");
    }

    internal static Vector3 Vector(float[] xyz) => xyz.Length == 3
        ? new Vector3(xyz[0], xyz[1], xyz[2])
        : throw new InvalidOperationException("Lab coordinates must have three components.");

    internal static Color Color(float[] rgb) => new(rgb[0], rgb[1], rgb[2], 1);
}

internal sealed record PlayerTuning(float Height, float CrouchedHeight, float Radius,
    float Speed, float SprintSpeed, float JumpSpeed, float Gravity,
    float MaximumStepHeight, float MaximumSlopeDegrees, float PointerRadiansPerUnit);
internal sealed record InteractionTuning(float Reach, float HoldDistance, float HoldSlack, float HoldReelSpeed, float ThrowSpeed);
internal sealed record VolumeDefinition(string Label, float[] Min, float[] Max, uint Material);
internal sealed record RampDefinition(string Label, float[] Origin, float Width, float Length, float AngleDegrees, float[] Color);
internal sealed record BlockDefinition(string Label, float[] Center, float[] Size);
internal sealed record CubeDefinition(string Label, float[] Center, float Size, float Mass, float[] Color);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(LabDefinition))]
internal sealed partial class LabJson : JsonSerializerContext;
