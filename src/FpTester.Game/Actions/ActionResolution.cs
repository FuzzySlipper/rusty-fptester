using System.Numerics;
using Rusty.Engine;

namespace FpTester.Game.Actions;

/// <summary>
/// What one landed action met: the body it struck (<see cref="Target"/>), or a surface (<see cref="Surface"/>), or
/// nothing. <see cref="Start"/>, <see cref="End"/> and <see cref="Radius"/> are the volume the query swept (a melee
/// capsule, a ray or projectile segment, an area sphere), so a product can draw exactly what was tested.
/// </summary>
internal sealed record ActionImpact(ActionDefinition Action, ulong User, ulong? Target, Vector3 Start, Vector3 End, float Radius,
    Vector3 Point, Vector3 Normal, bool Surface);

/// <summary>
/// A projectile in flight: who shot it, its action, where it is, its heading, how far it has gone, and the bodies it
/// may hit (those eligible when it was loosed).
/// </summary>
internal sealed class Projectile(ActionDefinition action, ulong user, Vector3 position, Vector3 heading, IReadOnlySet<ulong> eligible)
{
    internal ActionDefinition Action { get; } = action;
    internal ulong User { get; } = user;
    internal IReadOnlySet<ulong> Eligible { get; } = eligible;
    internal Vector3 Position { get; set; } = position;
    internal Vector3 Heading { get; } = heading;
    internal float Travelled { get; set; }
}

/// <summary>
/// Lands actions with Engine spatial queries in one session and reports what each met. Bodies are offered explicitly
/// as the colliders each query may hit (world-space boxes, as an Engine collider projection reads them); the user's
/// own entity is ignored. Resolution decides what was struck, not what that does: the caller applies its own rules
/// (damage, effects, an impulse) to the returned impacts. Projectiles fly on the admitted seconds passed to <see cref="Step"/>.
/// Provenance: rusty-hotel and rusty-riders <c>Actions/ActionResolution.cs</c> (from rusty-dagger
/// <c>CombatResolution</c>): the queries, the wall-hug melee fallback and the projectile stepping, without the
/// stat, contribution and effect pipeline.
/// </summary>
internal sealed class ActionResolution(IEngineContext engine, SpatialSession session)
{
    // Every query sees every collider; bodies are offered explicitly as the entities each query may hit.
    private static readonly SpatialQueryFilter Everything = new(0, uint.MaxValue);
    // A sweep meeting a surface this close to its start began in contact with it.
    private const double StartContact = .05;
    private readonly List<Projectile> projectiles = [];

    internal IReadOnlyList<Projectile> Projectiles => projectiles;

    /// <summary>Lands an action whose windup has ended. A projectile takes flight instead and lands in a later step.</summary>
    internal ActionImpact[] Land(ActionDefinition action, ulong user, Vector3 eye, Vector3 aim, ReadOnlyMemory<SpatialEntityCollider> bodies)
    {
        ActionDelivery d = action.Delivery;
        switch (d.Kind)
        {
            case DeliveryKind.Hitscan:
                return [First(action, user, eye, aim * d.Range, 0, Ray(eye, aim, d.Range, bodies, user))];
            case DeliveryKind.Melee:
            {
                float radius = d.Width / 2;
                SpatialHit hit = engine.Spatial.CastCapsule(new(session, eye, 0, radius, aim * d.Range, 0, Everything, bodies, new[] { user }));
                // A user hugging a wall starts the sweep in it; then the swing is judged along its line alone.
                if (hit.Present && hit.Kind != SpatialHitKind.Entity && (hit.StartSolid || hit.Distance < StartContact))
                {
                    radius = 0;
                    hit = Ray(eye, aim, d.Range, bodies, user);
                }
                return [First(action, user, eye, aim * d.Range, radius, hit)];
            }
            case DeliveryKind.Projectile:
                HashSet<ulong> eligible = [];
                foreach (SpatialEntityCollider body in bodies.Span) if (body.Entity != user) eligible.Add(body.Entity);
                projectiles.Add(new(action, user, eye, aim, eligible));
                return [];
            default:
                return Area(action, user, eye + aim * d.Range, bodies);
        }
    }

    /// <summary>Moves every projectile by admitted seconds, casting each step's segment; returns what they met.</summary>
    internal ActionImpact[] Step(float seconds, ReadOnlyMemory<SpatialEntityCollider> bodies)
    {
        List<ActionImpact> impacts = [];
        foreach (Projectile shot in projectiles.ToArray())
        {
            ActionDelivery d = shot.Action.Delivery;
            float travel = Math.Min(d.Speed * seconds, d.Range - shot.Travelled);
            Vector3 from = shot.Position, next = from + shot.Heading * travel;
            SpatialEntityCollider[] eligible = bodies.ToArray().Where(b => shot.Eligible.Contains(b.Entity)).ToArray();
            SpatialHit hit = engine.Spatial.CastSegment(new(session, from, next, Everything, eligible, new[] { shot.User },
                ReadOnlyMemory<SpatialEntityCollider>.Empty));
            shot.Position = next;
            shot.Travelled += travel;
            if (hit.Present || shot.Travelled >= d.Range - 1e-4f)
            {
                impacts.Add(First(shot.Action, shot.User, from, next - from, d.Width / 2, hit));
                projectiles.Remove(shot);
            }
        }
        return impacts.ToArray();
    }

    internal void Clear() => projectiles.Clear();

    /// <summary>Whether a straight line between two points meets no surface.</summary>
    internal bool LineClear(Vector3 from, Vector3 to) => !engine.Spatial.CastSegment(new(session, from, to, Everything,
        ReadOnlyMemory<SpatialEntityCollider>.Empty, ReadOnlyMemory<ulong>.Empty, ReadOnlyMemory<SpatialEntityCollider>.Empty)).Present;

    private SpatialHit Ray(Vector3 eye, Vector3 aim, float range, ReadOnlyMemory<SpatialEntityCollider> bodies, ulong user) =>
        engine.Spatial.CastRay(new(session, eye, aim, range, Everything, bodies, new[] { user }, bodies));

    // The body a query met, if any, is the target; a surface or nothing ends the action there.
    private static ActionImpact First(ActionDefinition action, ulong user, Vector3 start, Vector3 sweep, float radius, SpatialHit hit)
    {
        Vector3 end = hit.Present ? start + Vector3.Normalize(sweep) * (float)hit.Distance : start + sweep;
        bool body = hit.Present && hit.Kind == SpatialHitKind.Entity;
        return new(action, user, body ? hit.Entity : null, start, end, radius, hit.Present ? hit.Point : start + sweep, hit.Normal,
            hit.Present && !body);
    }

    // Every body whose box overlaps the area's sphere and has a clear line from its centre.
    private ActionImpact[] Area(ActionDefinition action, ulong user, Vector3 centre, ReadOnlyMemory<SpatialEntityCollider> bodies)
    {
        float radius = action.Delivery.Radius;
        List<ActionImpact> impacts = [];
        foreach (SpatialEntityCollider body in bodies.Span)
        {
            if (body.Entity == user) continue;
            Vector3 nearest = Vector3.Clamp(centre, body.Min, body.Max);
            if (Vector3.Distance(nearest, centre) > radius) continue;
            Vector3 middle = (body.Min + body.Max) / 2;
            if (!LineClear(centre, middle) && !LineClear(centre, nearest)) continue;
            Vector3 toward = middle - centre;
            impacts.Add(new(action, user, body.Entity, centre, centre, radius, nearest,
                toward.LengthSquared() > 0 ? Vector3.Normalize(toward) : Vector3.UnitY, false));
        }
        return impacts.Count == 0 ? [new(action, user, null, centre, centre, radius, centre, Vector3.UnitY, false)] : impacts.ToArray();
    }
}
