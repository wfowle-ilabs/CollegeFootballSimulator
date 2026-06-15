using CfbSim.Core.Model;

namespace CfbSim.Core.Generation;

/// <summary>How many players to generate at each position. A plausible two-deep-ish
/// set for M1 — enough to field the inside run and feel like a team.</summary>
public sealed class RosterSpec
{
    public required IReadOnlyList<(Position Position, int Count)> Counts { get; init; }

    public static RosterSpec Default { get; } = new()
    {
        Counts = new (Position, int)[]
        {
            (Position.QB, 2),
            (Position.RB, 3),
            (Position.WR, 4),
            (Position.TE, 2),
            (Position.OL, 7),
            (Position.DL, 5),
            (Position.EDGE, 2),
            (Position.LB, 4),
            (Position.CB, 4),
            (Position.S, 3),
            (Position.K, 1),
            (Position.P, 1),
        },
    };
}
