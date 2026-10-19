using FpTester.Game.Actions;
using FpTester.Game.Combat;
using FpTester.Game.Player;
using FpTester.Game.Time;
using Rusty.Engine;
using Rusty.Engine.Debugging;

namespace FpTester.Game.Developer;

/// <summary>
/// The lab's tunables page for the Engine tunables panel (<see cref="TunablesDebugModule"/>): the body and camera from
/// <c>player.json</c>, the chosen action from <c>actions.json</c> (a chooser picks which), and the world rate (live
/// only). Each control applies through its owner (<see cref="PlayerController.Retune"/>, <see cref="LabCombat.Retune"/>,
/// <see cref="WorldTime.SetRate"/>) at once; Save writes the bound file through the Engine content source (which marks
/// it already staged, so no reload follows: the live value is already applied), and a hand edit of the same file
/// arrives as a live content change. Declarations are explicit lists.
/// Provenance: rusty-hotel <c>Interface/Workbench/HotelWorkbench</c> (the page-and-chooser shape); the controls are the lab's.
/// </summary>
internal sealed class LabTunables(IContentSourceService source, PlayerController player, LabCombat combat, WorldTime time)
{
    private delegate PlayerTuning With(PlayerTuning tuning, float value);

    private static readonly (string Id, string Label, float Min, float Max, float Step, string Pointer, Func<PlayerTuning, float> Get, With Set)[] Body =
    [
        ("speed", "Walk speed", 1, 12, .1f, "/speed", t => t.Speed, (t, v) => t with { Speed = v, SprintSpeed = Math.Max(v, t.SprintSpeed) }),
        ("sprint", "Sprint speed", 1, 16, .1f, "/sprintSpeed", t => t.SprintSpeed, (t, v) => t with { SprintSpeed = Math.Max(v, t.Speed) }),
        ("jump", "Jump speed", 1, 14, .1f, "/jumpSpeed", t => t.JumpSpeed, (t, v) => t with { JumpSpeed = v }),
        ("gravity", "Body gravity", 4, 40, .5f, "/gravity", t => t.Gravity, (t, v) => t with { Gravity = v }),
        ("step", "Step height", 0, .8f, .05f, "/maximumStepHeight", t => t.MaximumStepHeight, (t, v) => t with { MaximumStepHeight = v }),
        ("slope", "Max slope °", 10, 80, 1, "/maximumSlopeDegrees", t => t.MaximumSlopeDegrees, (t, v) => t with { MaximumSlopeDegrees = v }),
        ("buffer", "Jump buffer s", 0, .4f, .01f, "/jumpBufferSeconds", t => t.JumpBufferSeconds, (t, v) => t with { JumpBufferSeconds = v }),
        ("coyote", "Coyote time s", 0, .4f, .01f, "/coyoteSeconds", t => t.CoyoteSeconds, (t, v) => t with { CoyoteSeconds = v }),
    ];

    private static readonly (string Id, string Label, float Min, float Max, float Step, string Pointer, Func<PlayerTuning, float> Get, With Set)[] View =
    [
        ("fov", "Field of view °", 50, 110, 1, "/fieldOfViewDegrees", t => t.FieldOfViewDegrees, (t, v) => t with { FieldOfViewDegrees = v }),
        ("sensitivity", "Mouse rad/unit", .0005f, .006f, .0001f, "/pointerRadiansPerUnit", t => t.PointerRadiansPerUnit, (t, v) => t with { PointerRadiansPerUnit = v }),
        ("bob", "Head bob m", 0, .1f, .005f, "/headBobMetres", t => t.HeadBobMetres, (t, v) => t with { HeadBobMetres = v }),
        ("bob-stride", "Bob stride m", .5f, 3, .05f, "/headBobStrideMetres", t => t.HeadBobStrideMetres, (t, v) => t with { HeadBobStrideMetres = v }),
    ];

    private delegate ActionDefinition WithAction(ActionDefinition action, float value);

    private static readonly (string Id, string Label, float Min, float Max, float Step, string Pointer, Func<ActionDefinition, float> Get, WithAction Set)[] Action =
    [
        ("windup", "Windup s", 0, 1.5f, .01f, "timing/windup", a => a.Timing.Windup, (a, v) => a with { Timing = a.Timing with { Windup = v } }),
        ("commit", "Commit s", .01f, 1, .01f, "timing/commit", a => a.Timing.Commit, (a, v) => a with { Timing = a.Timing with { Commit = v } }),
        ("recovery", "Recovery s", 0, 2, .01f, "timing/recovery", a => a.Timing.Recovery, (a, v) => a with { Timing = a.Timing with { Recovery = v } }),
        ("cooldown", "Cooldown s", 0, 5, .05f, "timing/cooldown", a => a.Timing.Cooldown, (a, v) => a with { Timing = a.Timing with { Cooldown = v } }),
        ("range", "Range m", .1f, 40, .1f, "delivery/range", a => a.Delivery.Range, (a, v) => a with { Delivery = a.Delivery with { Range = v } }),
        ("width", "Width m", 0, 2, .05f, "delivery/width", a => a.Delivery.Width, (a, v) => a with { Delivery = a.Delivery with { Width = v } }),
        ("move", "Move while busy", 0, 1, .05f, "busyMovementScale", a => a.BusyMovementScale, (a, v) => a with { BusyMovementScale = v }),
        ("impulse", "Impulse N·s", 0, 60, .5f, "impulse", a => a.Impulse, (a, v) => a with { Impulse = v }),
    ];

    internal TunablesDebugModule Module() => new(source, Page, Choose);

    private TunablesPage Page()
    {
        PlayerTuning t = player.Tuning;
        Tunable BodyControl((string Id, string Label, float Min, float Max, float Step, string Pointer, Func<PlayerTuning, float> Get, With Set) c) =>
            new(new TunableControl(c.Id, c.Label, TunableKind.Slider, c.Min, c.Max, c.Step, c.Get(t)) { Source = new(PlayerTuning.Path, c.Pointer) },
                v => player.Retune(PlayerTuning.Validate(c.Set(player.Tuning, v))));
        int index = combat.Selected;
        ActionDefinition a = combat.Action;
        Tunable ActionControl((string Id, string Label, float Min, float Max, float Step, string Pointer, Func<ActionDefinition, float> Get, WithAction Set) c) =>
            new(new TunableControl($"action.{c.Id}", c.Label, TunableKind.Slider, c.Min, c.Max, c.Step, c.Get(a))
                { Source = new(ActionCatalog.Path, $"/actions/{index}/{c.Pointer}") },
                v =>
                {
                    ActionDefinition[] actions = [.. combat.Catalog.Actions];
                    actions[index] = c.Set(actions[index], v);
                    combat.Retune(ActionCatalog.Validate(new ActionCatalog(actions)));
                });
        return new TunablesPage("First-person lab",
        [
            new TunableGroup("body", "Body", Body.Select(BodyControl).ToArray()) { File = PlayerTuning.Path },
            new TunableGroup("view", "View", View.Select(BodyControl).ToArray()) { File = PlayerTuning.Path },
            new TunableGroup("action", $"Action · {a.Name} ({a.Delivery.Kind})", Action.Select(ActionControl).ToArray()) { File = ActionCatalog.Path },
            new TunableGroup("world", "World time (not saved)",
            [
                new Tunable(new TunableControl("world.rate", "World rate ×", TunableKind.Slider, .05f, 1, .05f, (float)time.Rate), v => time.SetRate(v)),
                new Tunable(new TunableControl("world.held", "Hold world", TunableKind.Toggle, 0, 1, 1, time.Held ? 1 : 0), v => { if (v > .5f) time.Hold(); else time.Run(); }),
            ]),
        ])
        {
            Choosers = [new TunableChooser("action", "Action", a.Id, combat.Catalog.Actions.Select(x => x.Id).ToArray())],
            Notice = "Changes apply at once. Save writes body and view to player.json and the action to actions.json; hand edits to those files reload live. World time is not saved.",
        };
    }

    // A chooser picks the action the page tunes (and the player uses); null when it is shown.
    private string? Choose(string chooser, string value)
    {
        if (chooser != "action") return $"Unknown chooser '{chooser}'.";
        int index = Array.FindIndex(combat.Catalog.Actions, x => x.Id == value);
        if (index < 0) return $"Unknown action '{value}'.";
        if (combat.User.Busy) return "An action is under way; choose when it is ready.";
        combat.Select(index);
        return null;
    }
}
