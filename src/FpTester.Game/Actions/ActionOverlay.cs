using System.Numerics;
using Rusty.Engine;

namespace FpTester.Game.Actions;

/// <summary>
/// Draws action volumes as an Engine inspection overlay set: each landed delivery's swept capsule, ray or sphere (green
/// where it struck a body, amber at a surface, grey for a miss) for a few seconds, the volume an action in windup is
/// about to sweep, and projectiles in flight. Drawn only while inspection shows the set
/// (<c>engine.inspect.overlay show set actions.volumes</c>; the developer shell shows it while open). It owns only
/// the recent impacts it draws. Presentation, on host seconds; nothing here decides a hit.
/// Origin: rusty-fptester (no product drew its melee volumes; rusty-hotel's <c>HeldInspection</c> is the overlay idiom).
/// </summary>
internal sealed class ActionOverlay(IEngineContext engine)
{
    internal const string Set = "actions.volumes";
    private const double ShowSeconds = 2.5;
    private const int Ring = 4;
    private static readonly Color Struck = new(.25f, .95f, .45f, 1), AtSurface = new(1, .7f, .2f, 1),
        Missed = new(.6f, .65f, .72f, 1), Pending = new(.45f, .7f, 1, 1), Flying = new(1, .85f, .35f, 1);
    private readonly List<(ActionImpact Impact, double Until)> recent = [];
    private double now;

    internal void Record(IEnumerable<ActionImpact> impacts)
    {
        foreach (ActionImpact impact in impacts) recent.Add((impact, now + ShowSeconds));
    }

    internal void Clear() => recent.Clear();

    /// <param name="hostSeconds">Unscaled host seconds since the last publish, so volumes fade while the world holds too.</param>
    /// <param name="preview">The action in windup and where it would be swept from now, if any.</param>
    internal void Publish(double hostSeconds, (ActionDefinition Action, Vector3 Eye, Vector3 Aim)? preview, IReadOnlyList<Projectile> flying)
    {
        now += hostSeconds;
        recent.RemoveAll(r => r.Until <= now);
        List<OverlayItem> items = [];
        foreach ((ActionImpact impact, _) in recent)
        {
            Color color = impact.Target is not null ? Struck : impact.Surface ? AtSurface : Missed;
            Volume(items, impact.Start, impact.End, impact.Radius, color);
            if (impact.Target is not null || impact.Surface) items.Add(OverlayItem.Marker(OverlayPlace.World, impact.Point, .15f, color, onTop: true));
            items.Add(OverlayItem.Label(OverlayPlace.World, impact.End + Vector3.UnitY * (impact.Radius + .1f),
                impact.Target is { } target ? $"{impact.Action.Id} → {target}" : impact.Action.Id, color, onTop: true));
        }
        if (preview is ({ } action, var eye, var aim))
        {
            ActionDelivery d = action.Delivery;
            if (d.Kind == DeliveryKind.Area) Volume(items, eye + aim * d.Range, eye + aim * d.Range, d.Radius, Pending);
            else Volume(items, eye, eye + aim * d.Range, d.Kind == DeliveryKind.Melee ? d.Width / 2 : 0, Pending);
        }
        foreach (Projectile shot in flying)
            items.Add(OverlayItem.WireSphere(OverlayPlace.World, shot.Position, Math.Max(.05f, shot.Action.Delivery.Width / 2), Flying, onTop: true));
        engine.Graphics.PublishOverlaySet(new OverlaySetRequest(Set, items.ToArray(), []));
    }

    // A capsule (spheres at each end joined by lines along its sides), a sphere where start and end meet, or a segment.
    private static void Volume(List<OverlayItem> items, Vector3 start, Vector3 end, float radius, Color color)
    {
        if (radius <= 0)
        {
            items.Add(OverlayItem.Segment(OverlayPlace.World, start, end, color, onTop: true));
            return;
        }
        items.Add(OverlayItem.WireSphere(OverlayPlace.World, end, radius, color, onTop: true));
        Vector3 along = end - start;
        if (along.LengthSquared() < 1e-6f) return;
        items.Add(OverlayItem.WireSphere(OverlayPlace.World, start, radius, color, onTop: true));
        Vector3 direction = Vector3.Normalize(along);
        Vector3 side = Vector3.Cross(direction, MathF.Abs(direction.Y) > .95f ? Vector3.UnitX : Vector3.UnitY);
        side = Vector3.Normalize(side);
        Vector3 up = Vector3.Cross(side, direction);
        for (int i = 0; i < Ring; i++)
        {
            float a = i * 2 * MathF.PI / Ring;
            Vector3 offset = (side * MathF.Cos(a) + up * MathF.Sin(a)) * radius;
            items.Add(OverlayItem.Segment(OverlayPlace.World, start + offset, end + offset, color, onTop: true));
        }
    }
}
