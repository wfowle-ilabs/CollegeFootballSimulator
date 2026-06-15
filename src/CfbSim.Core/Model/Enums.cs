namespace CfbSim.Core.Model;

/// <summary>Player positions. v1 keeps the offensive/defensive line coarse (OL/DL).</summary>
public enum Position
{
    QB, RB, WR, TE, OL,   // offense
    DL, EDGE, LB, CB, S,  // defense
    K, P                  // special teams
}

/// <summary>Academic class. Redshirt nuance is deferred past M1.</summary>
public enum ClassYear { Freshman, Sophomore, Junior, Senior }

public enum CoachRole { HeadCoach, OffensiveCoordinator, DefensiveCoordinator }

/// <summary>
/// Position skills (the checkable ratings). Independent from core attributes —
/// a check blends a skill with an attribute, so skills are NOT derived from them.
/// </summary>
public enum Skill
{
    // Offensive line
    RunBlock, PassBlock, AnchorStrength, PullSpeed,
    // Defensive front
    BlockShed, PassRush, RunStop, Pursuit,
    // Back seven
    Tackling, ManCoverage, ZoneCoverage, Blitz,
    // Ball carriers / receivers
    Vision, Elusiveness, Trucking, BallSecurity, Receiving, RouteRunning, Catching,
    // QB
    ThrowPower, ShortAccuracy, DeepAccuracy,
    // Kicking
    KickPower, KickAccuracy
}
