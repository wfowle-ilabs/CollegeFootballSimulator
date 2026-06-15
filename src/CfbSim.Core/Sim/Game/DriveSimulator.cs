using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Sim.Play;
using CfbSim.Core.Sim.PlayCalling;
using CfbSim.Core.Stats;

namespace CfbSim.Core.Sim.Game;

public enum DriveEndReason
{
    Touchdown, FieldGoalGood, FieldGoalMissed, Punt, Downs, Interception, Fumble, Safety, EndOfHalf, EndOfGame
}

/// <summary>How a possession ended and where the next team takes over.</summary>
public sealed class DriveResult
{
    public required DriveEndReason Reason { get; init; }
    public int OffensePoints { get; init; }
    public bool SafetyConceded { get; init; }
    /// <summary>Next team's starting ball-on (their perspective); -1 = kickoff → own 25.
    /// When <see cref="OffenseRetains"/> is set, this is the SAME team's new ball-on.</summary>
    public int NextBallOn { get; init; } = -1;
    /// <summary>The offense kept the ball (a recovered punt muff) — possession does not flip.</summary>
    public bool OffenseRetains { get; init; }
    /// <summary>The defense took a punt back for a touchdown (defense scores 7; the offense then
    /// receives the ensuing kickoff, so possession does not flip).</summary>
    public bool DefenseReturnTd { get; init; }
}

/// <summary>
/// Simulates one possession: calls plays down by down, applies them to the field,
/// runs the clock, and ends on a score, turnover, punt, turnover-on-downs, or
/// (when timed) the end of the half/game. See docs/architecture.qmd (Simulation Layers).
/// </summary>
public static class DriveSimulator
{
    public static DriveResult Run(
        IRng rng, Team offense, Team defense, BoxScore box, GameClock clock, Drive drive,
        int startBallOn, int offenseScore, int defenseScore, double passBias, bool driveInFirstHalf, bool untimed)
    {
        int ballOn = startBallOn;
        int down = 1;
        int distance = Math.Min(10, 100 - ballOn);
        TeamStatLine offTeam = box.TeamOf(offense.Id);

        while (true)
        {
            var situation = new PlaySituation(down, distance, ballOn,
                offenseScore - defenseScore, clock.SecondsLeftInHalf, !driveInFirstHalf);
            PlayType call = PlayCaller.ChooseOffense(rng, situation, passBias);
            DefensiveKey key = PlayCaller.ChooseDefense(rng, situation);
            string spot = Field.Describe(ballOn);
            string down_ = $"{Ordinal(down)} & {distance}";

            // --- Special teams (terminal) ---
            if (call == PlayType.Punt)
            {
                PlayOutcome p = SpecialTeamsResolver.Punt(rng, offense, defense);
                clock.Advance(p.ClockSeconds);
                offTeam.PossessionSeconds += p.ClockSeconds;
                if (p.Muffed)
                {
                    int recover = Math.Clamp(ballOn + p.YardsGained, ballOn, 99);
                    Add(drive, clock, SegmentKind.SpecialTeams, $"{clock.Label()} {down_} at {spot}: punt MUFFED — {offense.Abbreviation} recovers at {Field.Describe(recover)}", down, distance, ballOn, PlayType.Punt);
                    return new DriveResult { Reason = DriveEndReason.Punt, OffenseRetains = true, NextBallOn = recover };
                }
                if (p.ReturnTouchdown)
                {
                    Add(drive, clock, SegmentKind.SpecialTeams, $"{clock.Label()} {down_} at {spot}: punt RETURNED FOR TD by {defense.Abbreviation}!", down, distance, ballOn, PlayType.Punt);
                    return new DriveResult { Reason = DriveEndReason.Punt, DefenseReturnTd = true };
                }
                Add(drive, clock, SegmentKind.SpecialTeams, $"{clock.Label()} {down_} at {spot}: " + (p.Blocked ? "punt BLOCKED" : $"punt {p.YardsGained} net"), down, distance, ballOn, PlayType.Punt);
                int land = ballOn + p.YardsGained;
                int next = land >= 100 ? 20 : Math.Clamp(100 - land, 1, 99);
                return new DriveResult { Reason = DriveEndReason.Punt, NextBallOn = next };
            }

            if (call == PlayType.FieldGoal)
            {
                int dist = (100 - ballOn) + 17;
                PlayOutcome fg = SpecialTeamsResolver.FieldGoal(rng, offense, dist);
                clock.Advance(fg.ClockSeconds);
                offTeam.PossessionSeconds += fg.ClockSeconds;
                Add(drive, clock, SegmentKind.SpecialTeams, $"{clock.Label()} {down_} at {spot}: {dist}-yd FG {(fg.Blocked ? "BLOCKED" : fg.KickGood ? "GOOD" : "no good")}", down, distance, ballOn, PlayType.FieldGoal);
                return fg.KickGood
                    ? new DriveResult { Reason = DriveEndReason.FieldGoalGood, OffensePoints = 3 }
                    : new DriveResult { Reason = DriveEndReason.FieldGoalMissed, NextBallOn = Math.Max(100 - ballOn, 20) };
            }

            // --- Scrimmage play ---
            PlayOutcome o = call is PlayType.InsideRun or PlayType.OutsideRun
                ? RunPlayResolver.Resolve(rng, offense, defense, call, key)
                : PassPlayResolver.Resolve(rng, offense, defense, call, key);

            clock.Advance(o.ClockSeconds);
            offTeam.PossessionSeconds += o.ClockSeconds;

            int newBallOn = ballOn + o.YardsGained;
            bool td = false, safety = false;
            int actual = o.YardsGained;
            if (!o.IsTurnover)
            {
                if (newBallOn >= 100) { td = true; actual = 100 - ballOn; newBallOn = 100; }
                else if (newBallOn <= 0) { safety = true; actual = -ballOn; newBallOn = 0; }
            }

            bool firstDown = !td && !safety && !o.IsTurnover && actual >= distance;
            StatAggregator.RecordScrimmage(box, offense, defense, o, actual, td, firstDown);
            Add(drive, clock, SegmentKind.Snap, $"{clock.Label()} {down_} at {spot}: {PlayLabel(call)} {Describe(o, actual, td)}", down, distance, ballOn, call, actual, td, firstDown, o.IsTurnover);

            if (o.IsTurnover)
            {
                int spotOff = Math.Clamp(ballOn + (o.Turnover == TurnoverKind.FumbleLost ? o.YardsGained : 0), 1, 99);
                return new DriveResult
                {
                    Reason = o.Turnover == TurnoverKind.Interception ? DriveEndReason.Interception : DriveEndReason.Fumble,
                    NextBallOn = Math.Clamp(100 - spotOff, 1, 99),
                };
            }

            if (td)
            {
                int points = 6;
                int diffAfterTd = offenseScore + 6 - defenseScore;
                if (GoForTwo(diffAfterTd, !driveInFirstHalf, clock.SecondsLeftInHalf))
                {
                    (bool good, string how) = TwoPointTry(rng, offense, defense);
                    if (good) points = 8;
                    Add(drive, clock, SegmentKind.SpecialTeams, $"          2-PT ({how}) {(good ? "GOOD" : "no good")}");
                }
                else
                {
                    PlayOutcome xp = SpecialTeamsResolver.ExtraPoint(rng, offense);
                    clock.Advance(xp.ClockSeconds);
                    if (xp.KickGood) points = 7;
                    Add(drive, clock, SegmentKind.SpecialTeams, $"          XP {(xp.KickGood ? "good" : "MISSED")}");
                }
                return new DriveResult { Reason = DriveEndReason.Touchdown, OffensePoints = points };
            }

            if (safety)
                return new DriveResult { Reason = DriveEndReason.Safety, SafetyConceded = true, NextBallOn = 35 };

            // Advance the chains.
            ballOn = Math.Clamp(newBallOn, 1, 99);
            if (firstDown)
            {
                down = 1;
                distance = Math.Min(10, 100 - ballOn);
            }
            else
            {
                down++;
                distance -= actual;
                if (down > 4)
                    return new DriveResult { Reason = DriveEndReason.Downs, NextBallOn = Math.Clamp(100 - ballOn, 1, 99) };
            }

            // Clock expiry ends the drive (half/game), unless untimed (overtime).
            if (!untimed)
            {
                if (clock.RegulationOver) return new DriveResult { Reason = DriveEndReason.EndOfGame };
                if (driveInFirstHalf && clock.PastHalf) return new DriveResult { Reason = DriveEndReason.EndOfHalf };
            }
        }
    }

    /// <summary>Append a play segment to the drive (carries the log text + structured fields).</summary>
    private static void Add(Drive drive, GameClock clock, SegmentKind kind, string text,
        int down = 0, int distance = 0, int ballOn = 0, PlayType playType = default,
        int yards = 0, bool td = false, bool firstDown = false, bool turnover = false)
        => drive.Segments.Add(new GameSegment
        {
            Kind = kind,
            ClockLabel = clock.Label(),
            ClockElapsed = clock.Elapsed,
            Text = text,
            Down = down,
            Distance = distance,
            BallOn = ballOn,
            PlayType = playType,
            Yards = yards,
            Touchdown = td,
            FirstDown = firstDown,
            Turnover = turnover,
        });

    /// <summary>The two-point chart: go for two only late, when the math favors it
    /// (diff is the offense's margin once the 6 is on the board).</summary>
    private static bool GoForTwo(int diffAfterTd, bool secondHalf, int secondsLeftInHalf)
    {
        if (!secondHalf || secondsLeftInHalf > 480) return false; // ~last 8 minutes
        return diffAfterTd is -2 or -5 or -10 or -16 or 1 or 4 or 5 or 11 or 12;
    }

    /// <summary>One goal-line snap for the conversion (lean run); good if it reaches the end zone.</summary>
    private static (bool Good, string How) TwoPointTry(IRng rng, Team offense, Team defense)
    {
        bool pass = rng.NextDouble() < 0.42;
        PlayOutcome o = pass
            ? PassPlayResolver.Resolve(rng, offense, defense, PlayType.ShortPass, DefensiveKey.StopRun)
            : RunPlayResolver.Resolve(rng, offense, defense, PlayType.InsideRun, DefensiveKey.StopRun);
        bool good = !o.IsTurnover && !o.Sack && !o.Incomplete && o.YardsGained >= 3;
        return (good, pass ? "pass" : "run");
    }

    private static string PlayLabel(PlayType t) => t switch
    {
        PlayType.InsideRun => "inside run",
        PlayType.OutsideRun => "outside run",
        PlayType.ShortPass => "short pass",
        PlayType.DeepPass => "deep shot",
        _ => t.ToString(),
    };

    private static string Describe(PlayOutcome o, int actual, bool td)
    {
        if (o.Sack) return $"SACK {actual}";
        if (o.Turnover == TurnoverKind.Interception) return "INTERCEPTED";
        if (o.Turnover == TurnoverKind.FumbleLost) return $"{actual:+0;-0;0}, FUMBLE LOST";
        if (o.Incomplete) return "incomplete";
        string s = $"{actual:+0;-0;0} yds";
        if (td) s += ", TD!";
        return s;
    }

    private static string Ordinal(int n) => n switch { 1 => "1st", 2 => "2nd", 3 => "3rd", _ => $"{n}th" };
}
