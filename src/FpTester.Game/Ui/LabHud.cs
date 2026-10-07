using System.Text;
using Rusty.Engine;

namespace FpTester.Game.Ui;

internal static class LabHud
{
    internal static UiValue Create(LabObservation observation)
    {
        LabPlayerFacts player = observation.Player;
        string Vec(float[] v) => FormattableString.Invariant($"{v[0]:0.00}, {v[1]:0.00}, {v[2]:0.00}");
        (string Key, string Text)[] fields = [
            ("room",player.Position[0] < 16 ? "01 / SLOPES & CLEARANCE" : player.Position[0] < 32 ? "02 / JUMP COURSE" : "03 / PHYSICS SANDBOX"),
            ("position",Vec(player.Feet)), ("velocity",Vec(player.Velocity)),
            ("stance",player.Stance), ("grounded",player.Grounded ? "GROUNDED" : "AIRBORNE"),
            ("support",player.SupportEntity?.ToString() ?? "world"),
            ("contact",$"{player.Contact} / {player.ContactEntity?.ToString() ?? "world"}"),
            ("blocks",player.BlockFlags), ("target",observation.Target), ("message",observation.Message),
            ("held",observation.HeldCube is null ? "" : observation.Target),
            ("step",observation.Stamp.Step.ToString()),
            ("actions",$"pickups {observation.Pickups} · throws {observation.Throws} · drops {observation.Drops}")
        ];
        List<byte> utf8 = [];
        List<StructuredValueNode> nodes = [new(StructuredValueKind.Object, 0, 0, 0, 0, 0, 0, 0, (uint)fields.Length)];
        foreach ((string key, string text) in fields)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key), textBytes = Encoding.UTF8.GetBytes(text);
            uint keyOffset = (uint)utf8.Count;
            utf8.AddRange(keyBytes);
            uint textOffset = (uint)utf8.Count;
            utf8.AddRange(textBytes);
            nodes.Add(new StructuredValueNode(StructuredValueKind.String, 0, 0, keyOffset, (uint)keyBytes.Length, textOffset, (uint)textBytes.Length, 0, 0));
        }
        return new UiValue(nodes.ToArray(), Enumerable.Range(1, fields.Length).Select(value => (uint)value).ToArray(), 0, utf8.ToArray());
    }
}
