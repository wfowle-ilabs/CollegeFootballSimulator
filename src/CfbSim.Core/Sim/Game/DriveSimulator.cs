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
    /// <summary>Next team's starting ball-on (their perspective); -1 = kickoff → own 25.</summary>
    public int NextBallOn { get; init; } = -1;
}

/// <summary>
/// Simulates one possession: calls plays down by down, applies them to the field,
/// runs the clock, and ends on a score, turnover, punt, turnover-on-downs, or
/// (when timed) the end of the half/game. See docs/architecture.qmd (Simulation Layers).
/// </summary>
public static class DriveSimulator
{
    public static DriveResult Run(
        IRng rng, Team offense, Team defense, BoxScore box, GameClock clock, List<string> log,
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
                PlayOutcome p = SpecialTeamsResolver.Punt(rng, offense);
                clock.Advance(p.ClockSeconds);
                offTeam.PossessionSeconds += p.ClockSeconds;
                log.Add($"{clock.Label()} {down_} at {spot}: punt {p.YardsGained} net");
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
                log.Add($"{clock.Label()} {down_} at {spot}: {dist}-yd FG {(fg.KickGood ? "GOOD" : "no good")}");
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
            log.Add($"{clock.Label()} {down_} at {spot}: {PlayLabel(call)} {Describe(o, actual, td)}");

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
                PlayOutcome xp = SpecialTeamsResolver.ExtraPoint(rng, offense);
                clock.Advance(xp.ClockSeconds);
                if (xp.KickGood) points = 7;
                log.Add($"          XP {(xp.KickGood ? "good" : "MISSED")}");
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
