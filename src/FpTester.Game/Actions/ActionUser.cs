using System.Numerics;

namespace FpTester.Game.Actions;

internal enum ActionPhase { Ready, Windup, Commit, Recovery }

/// <summary>Why an action was not started.</summary>
internal enum ActionRefusal { None, Busy, Cooldown }

/// <summary>
/// One actor's action timing: the action in progress and its phase, the aim committed when it started, and each
/// action's cooldown. One owner for readiness, the pending impact and cancellation, on admitted (world) seconds; each
/// use's phases may be scaled when it begins. Costs and resolution belong to the caller.
/// Provenance: rusty-hotel and rusty-riders <c>Actions/ActionUser.cs</c> (from rusty-dagger <c>AttackExecution</c>),
/// merged: riders' time scale and end-of-phase tolerance, hotel's start count and elapsed time.
/// </summary>
internal sealed class ActionUser
{
    private readonly Dictionary<string, float> cooldowns = new(StringComparer.Ordinal);
    // Seconds accumulate in float steps; a phase this close to its end is over, so a world advanced by exactly an
    // action's duration does not leave a sliver of recovery owed.
    private const float Due = 1e-4f;
    private float remaining;
    private float scale = 1;

    internal ActionDefinition? Current { get; private set; }
    internal ActionPhase Phase { get; private set; }
    /// <summary>The direction committed when the action started; strafing out of it evades it.</summary>
    internal Vector3 Aim { get; private set; }
    internal bool Busy => Phase != ActionPhase.Ready;
    /// <summary>Counts actions begun, so a presentation can tell a new action from the same one again.</summary>
    internal ulong Started { get; private set; }
    /// <summary>Seconds since the current action began.</summary>
    internal float Elapsed { get; private set; }
    internal float PhaseProgress => Current is not { } a ? 0 : 1 - remaining / Math.Max(.001f, Phase switch
    {
        ActionPhase.Windup => a.Timing.Windup, ActionPhase.Commit => a.Timing.Commit, ActionPhase.Recovery => a.Timing.Recovery, _ => 1
    } * scale);

    internal float Cooldown(string action) => cooldowns.GetValueOrDefault(action);

    internal ActionRefusal Readiness(ActionDefinition action) =>
        Busy ? ActionRefusal.Busy : Cooldown(action.Id) > 0 ? ActionRefusal.Cooldown : ActionRefusal.None;

    /// <summary>
    /// Starts an accepted action with its phases scaled by <paramref name="timeScale"/>; its cooldown runs from now.
    /// Returns the seconds until it is ready again (what a turn- or movement-timed product buys from gameplay time).
    /// </summary>
    internal float Begin(ActionDefinition action, Vector3 aim, float timeScale = 1)
    {
        Current = action;
        Aim = aim;
        Phase = ActionPhase.Windup;
        scale = timeScale;
        remaining = action.Timing.Windup * scale;
        Started++;
        Elapsed = 0;
        if (action.Timing.Cooldown > 0) cooldowns[action.Id] = action.Timing.Cooldown;
        return action.Duration * scale;
    }

    /// <summary>
    /// Advances by admitted seconds. Returns the action whose windup ended in this step: it lands now, once, and then
    /// stays committed and recovers.
    /// </summary>
    internal ActionDefinition? Step(float seconds)
    {
        foreach (string id in cooldowns.Keys.ToArray())
            if ((cooldowns[id] -= seconds) <= 0) cooldowns.Remove(id);
        if (Current is not { } action) return null;
        remaining -= seconds;
        Elapsed += seconds;
        ActionDefinition? landed = null;
        while (remaining <= Due && Current is not null)
            switch (Phase)
            {
                case ActionPhase.Windup: landed = action; Phase = ActionPhase.Commit; remaining += action.Timing.Commit * scale; break;
                case ActionPhase.Commit: Phase = ActionPhase.Recovery; remaining += action.Timing.Recovery * scale; break;
                default: Phase = ActionPhase.Ready; Current = null; remaining = 0; break;
            }
        return landed;
    }

    /// <summary>Cancels the action in progress without landing it; its cooldown still runs.</summary>
    internal void Interrupt()
    {
        Current = null;
        Phase = ActionPhase.Ready;
        remaining = 0;
    }

    internal void Reset()
    {
        Interrupt();
        cooldowns.Clear();
        Aim = default;
    }
}
