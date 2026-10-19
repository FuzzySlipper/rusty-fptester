using System.Text.Json.Serialization;
using FpTester.Game.Content;
using Rusty.Engine;

namespace FpTester.Game.Actions;

/// <summary>How an action reaches what it affects; each is resolved by an Engine spatial query.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DeliveryKind>))]
internal enum DeliveryKind
{
    /// <summary>A capsule swept along the aim from the eye: the first body it meets within reach.</summary>
    Melee,
    /// <summary>A ray along the aim: the first body or surface within range.</summary>
    Hitscan,
    /// <summary>A shot that travels along the aim at a speed, cast segment by segment on admitted time.</summary>
    Projectile,
    /// <summary>Every body within a radius of a point along the aim (0 is the user's eye) with a clear line to it.</summary>
    Area,
}

/// <summary>
/// One action an actor can use: how it is delivered, its timing, and the impulse it gives a struck dynamic body. The
/// labels name its windup and commit phases for a HUD. Product rules (damage, costs, effects, ammunition) are not
/// here: a product extends this record with its own fields and applies them to the impacts
/// <see cref="ActionResolution"/> returns.
/// Provenance: the shared shape of rusty-hotel and rusty-riders <c>Actions/ActionDefinition.cs</c> (themselves from
/// rusty-dagger), with their stats, costs, damage packets and effects left to the product.
/// </summary>
/// <param name="BusyMovementScale">How much of the movement input moves the user while the action is under way (1 free, 0 rooted).</param>
/// <param name="Impulse">Newton-seconds along the aim given to a struck dynamic body; 0 for none.</param>
internal sealed record ActionDefinition(string Id, string Name, ActionDelivery Delivery, ActionTiming Timing,
    string WindupLabel, string CommitLabel, float BusyMovementScale, float Impulse)
{
    /// <summary>Seconds from start to ready, before the user's time scale.</summary>
    internal float Duration => Timing.Windup + Timing.Commit + Timing.Recovery;
}

/// <param name="Range">Reach (melee), distance (hitscan, projectile), or how far along the aim an area is centred.</param>
/// <param name="Width">A melee sweep's or a projectile's width.</param>
/// <param name="Radius">An area's radius.</param>
/// <param name="Speed">A projectile's speed, metres per second.</param>
internal sealed record ActionDelivery(DeliveryKind Kind, float Range = 0, float Width = 0, float Radius = 0, float Speed = 0);

/// <summary>Seconds of windup before it lands, of commitment after, of recovery, and before it may be used again.</summary>
internal sealed record ActionTiming(float Windup, float Commit, float Recovery, float Cooldown);

/// <summary>The authored actions, from <c>content/actions.json</c> (live content: edits apply without a restart).</summary>
internal sealed record ActionCatalog(ActionDefinition[] Actions)
{
    internal const string Path = "actions.json";

    internal static ActionCatalog Load(IEngineContext engine) => Validate(Authored.Read(engine, Path, ActionJson.Default.ActionCatalog));

    internal static ActionCatalog Validate(ActionCatalog catalog)
    {
        Authored.Require(catalog.Actions.Length > 0, Path, "actions", "must name at least one action.");
        string? repeated = catalog.Actions.GroupBy(a => a.Id).FirstOrDefault(g => g.Count() > 1)?.Key;
        Authored.Require(repeated is null, Path, "actions", $"id '{repeated}' appears more than once.");
        for (int i = 0; i < catalog.Actions.Length; i++) Validate(catalog.Actions[i], $"actions[{i}]");
        return catalog;
    }

    internal ActionDefinition? Action(string id) => Actions.FirstOrDefault(a => a.Id == id);

    private static void Validate(ActionDefinition a, string at)
    {
        ActionDelivery d = a.Delivery;
        switch (d.Kind)
        {
            case DeliveryKind.Melee:
                Authored.Positive(Path, $"{at}.delivery.range", d.Range);
                Authored.Positive(Path, $"{at}.delivery.width", d.Width);
                break;
            case DeliveryKind.Hitscan:
                Authored.Positive(Path, $"{at}.delivery.range", d.Range);
                break;
            case DeliveryKind.Projectile:
                Authored.Positive(Path, $"{at}.delivery.range", d.Range);
                Authored.Positive(Path, $"{at}.delivery.speed", d.Speed);
                break;
            case DeliveryKind.Area:
                Authored.AtLeast(Path, $"{at}.delivery.range", d.Range, 0);
                Authored.Positive(Path, $"{at}.delivery.radius", d.Radius);
                break;
        }
        Authored.AtLeast(Path, $"{at}.timing.windup", a.Timing.Windup, 0);
        Authored.Positive(Path, $"{at}.timing.commit", a.Timing.Commit);
        Authored.AtLeast(Path, $"{at}.timing.recovery", a.Timing.Recovery, 0);
        Authored.AtLeast(Path, $"{at}.timing.cooldown", a.Timing.Cooldown, 0);
        Authored.Within(Path, $"{at}.busyMovementScale", a.BusyMovementScale, 0, 1);
        Authored.AtLeast(Path, $"{at}.impulse", a.Impulse, 0);
    }
}

// Authored: missing constructor values, nulls in non-nullable fields and unknown members are errors, not defaults.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(ActionCatalog))]
internal sealed partial class ActionJson : JsonSerializerContext;
