using System.Numerics;
using FpTester.Game.Actions;
using Rusty.Engine;
using Rusty.Engine.Entities;

namespace FpTester.Game.Combat;

/// <summary>
/// The lab's use of the action model: the player uses the selected authored action, its windup, landing and recovery
/// run on admitted steps, and what it strikes is a loose cube, which takes the action's impulse through Dynamics. A
/// product puts its own rules (damage, costs, reactions) where <see cref="Apply"/> is. It owns the player's
/// <see cref="ActionUser"/>, the selection, the counts and the volume overlay.
/// </summary>
internal sealed class LabCombat(IEngineContext engine, SpatialSession session, ActionCatalog catalog)
{
    private readonly ActionUser user = new();
    private readonly ActionResolution resolution = new(engine, session);
    private readonly List<DynamicsEntityAction> pushes = [];

    internal ActionCatalog Catalog { get; private set; } = catalog;
    internal ActionOverlay Overlay { get; } = new(engine);
    internal int Selected { get; private set; }
    internal ActionDefinition Action => Catalog.Actions[Selected];
    internal ActionUser User => user;
    internal IReadOnlyList<Projectile> Projectiles => resolution.Projectiles;
    internal uint Uses { get; private set; }
    internal uint Hits { get; private set; }
    internal uint Misses { get; private set; }
    internal ActionImpact? LastImpact { get; private set; }
    internal string Message { get; private set; } = "";
    /// <summary>How much of the movement input moves the player now.</summary>
    internal float MovementScale => user.Current?.BusyMovementScale ?? 1;

    /// <summary>Takes a reloaded catalog; the action in progress finishes with the definition it began with.</summary>
    internal void Retune(ActionCatalog next)
    {
        string selected = Action.Id;
        Catalog = next;
        Selected = Math.Max(0, Array.FindIndex(next.Actions, a => a.Id == selected));
    }

    internal void Select(int index)
    {
        if (index < 0 || index >= Catalog.Actions.Length || user.Busy) return;
        Selected = index;
        Message = $"{Action.Name} ready";
    }

    internal void SelectNext() => Select((Selected + 1) % Catalog.Actions.Length);

    /// <summary>Starts the selected action along <paramref name="aim"/>; refused while busy or cooling down.</summary>
    internal void Use(Vector3 aim)
    {
        ActionRefusal refusal = user.Readiness(Action);
        if (refusal != ActionRefusal.None)
        {
            Message = refusal == ActionRefusal.Busy ? $"{user.Current!.Name}: {user.Phase}" : $"{Action.Name} cooling down";
            return;
        }
        user.Begin(Action, aim);
        Uses++;
        Message = Action.WindupLabel;
    }

    /// <summary>
    /// One admitted step: the action in progress (landing it from the current eye along its committed aim) and shots in
    /// flight. Returns the impulses for this step's Dynamics step.
    /// </summary>
    internal DynamicsEntityAction[] Step(float seconds, ulong self, Vector3 eye, ReadOnlyMemory<SpatialEntityCollider> bodies,
        Func<ulong, EntityId?> cube)
    {
        pushes.Clear();
        if (user.Step(seconds) is { } landed)
        {
            Message = landed.CommitLabel;
            Apply(resolution.Land(landed, self, eye, user.Aim, bodies), cube);
        }
        Apply(resolution.Step(seconds, bodies), cube);
        return pushes.ToArray();
    }

    /// <summary>Publishes the overlay: recent volumes, the windup preview, shots in flight.</summary>
    internal void Present(double hostSeconds, Vector3 eye) =>
        Overlay.Publish(hostSeconds, user is { Current: { } a, Phase: ActionPhase.Windup } ? (a, eye, user.Aim) : null, resolution.Projectiles);

    internal void Reset()
    {
        user.Reset();
        resolution.Clear();
        Overlay.Clear();
        Uses = Hits = Misses = 0;
        LastImpact = null;
        Message = "";
    }

    private void Apply(ActionImpact[] impacts, Func<ulong, EntityId?> cube)
    {
        Overlay.Record(impacts);
        foreach (ActionImpact impact in impacts)
        {
            LastImpact = impact;
            if (impact.Target is { } target && cube(target) is { } entity)
            {
                Hits++;
                // Along the sweep; an area pushes outward from its centre (its impacts' normal).
                Vector3 along = impact.End - impact.Start;
                Vector3 direction = along.LengthSquared() > 1e-6f ? Vector3.Normalize(along) : impact.Normal;
                if (impact.Action.Impulse > 0) pushes.Add(new DynamicsEntityAction(entity, Vector3.Zero, Vector3.Zero, direction * impact.Action.Impulse, Vector3.Zero, true));
            }
            else Misses++;
        }
    }
}
