using CfbSim.Core.Checks;
using CfbSim.Core.Model;

namespace CfbSim.Core.Sim.Play;

/// <summary>
/// The field-agnostic result of one play. Resolvers compute yards and events;
/// the drive/game layer applies them to field position, downs, clock and score.
/// </summary>
public sealed class PlayOutcome
{
    public required PlayType PlayType { get; init; }

    /// <summary>Net yards for a scrimmage play; net distance for a punt.</summary>
    public int YardsGained { get; set; }

    public bool Incomplete { get; set; }
    public bool Sack { get; set; }
    public TurnoverKind Turnover { get; set; } = TurnoverKind.None;

    /// <summary>For kicks (FG/XP): whether it was good.</summary>
    public bool KickGood { get; set; }

    /// <summary>For a punt: the return team muffed it and the kicking team recovered (turnover).</summary>
    public bool Muffed { get; set; }

    /// <summary>A kick (FG/XP/punt) was blocked.</summary>
    public bool Blocked { get; set; }

    /// <summary>A punt was returned all the way for a touchdown by the return team.</summary>
    public bool ReturnTouchdown { get; set; }

    public int ClockSeconds { get; set; } = 30;

    /// <summary>Incompletion / out of bounds / score / kick → clock stops.</summary>
    public bool ClockStops { get; set; }

    // Who did what (for the box score).
    public Player? BallCarrier { get; set; }
    public Player? Passer { get; set; }
    public Player? Receiver { get; set; }
    public Player? Defender { get; set; }

    public List<CheckResult> Checks { get; } = new();
    public List<string> Trace { get; } = new();

    public bool IsTurnover => Turnover != TurnoverKind.None;
}
