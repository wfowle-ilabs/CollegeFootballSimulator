using CfbSim.Core.Checks;
using CfbSim.Core.Model;
using CfbSim.Core.Ratings;
using CfbSim.Core.Rng;

namespace CfbSim.Core.Sim.Play;

/// <summary>Punts and place kicks. Returns/blocks are deferred past M2.</summary>
public static class SpecialTeamsResolver
{
    public const int MaxFieldGoalYards = 60;

    /// <summary>Net punt distance (yards the ball nets toward the opponent's goal).</summary>
    public static PlayOutcome Punt(IRng rng, Team offense)
    {
        Player punter = offense.Starter(Position.P) ?? offense.Starter(Position.K)!;
        var outcome = new PlayOutcome { PlayType = PlayType.Punt, ClockSeconds = 14, ClockStops = true };

        int mod = RatingMath.Modifier(punter.Of(Skill.KickPower), punter.Attributes.Composure);
        int net = (int)Math.Round(40 + mod * 3 + rng.NextGaussian() * 5);
        outcome.YardsGained = Math.Clamp(net, 25, 65);
        outcome.BallCarrier = punter;
        outcome.Trace.Add($"PUNT  {punter.Name}: {outcome.YardsGained} net");
        return outcome;
    }

    /// <summary>Field goal from the given distance (yards, including the 17-yd snap+kick).</summary>
    public static PlayOutcome FieldGoal(IRng rng, Team offense, int distanceYards)
    {
        Player kicker = offense.Starter(Position.K)!;
        var outcome = new PlayOutcome { PlayType = PlayType.FieldGoal, ClockSeconds = 6, ClockStops = true };

        if (distanceYards > MaxFieldGoalYards)
        {
            outcome.KickGood = false;
            outcome.Trace.Add($"FG    {distanceYards} yds — out of range, no good");
            return outcome;
        }

        // DC rises with distance: a chip shot is easy, a 55-yarder is hard.
        int distanceDc = (distanceYards - 25) / 3; // ~0 at 25 yds, ~12 at 60
        CheckResult kick = CheckResolver.Resolve(rng,
            RatingMath.Modifier(kicker.Of(Skill.KickAccuracy), kicker.Attributes.Composure), distanceDc);
        outcome.Checks.Add(kick);
        outcome.KickGood = kick.Success;
        outcome.BallCarrier = kicker;
        outcome.Trace.Add(kick.Describe($"FG    {kicker.Name} {distanceYards} yds") + (kick.Success ? " — GOOD" : " — no good"));
        return outcome;
    }

    /// <summary>Extra point — effectively a ~33-yard field goal.</summary>
    public static PlayOutcome ExtraPoint(IRng rng, Team offense)
    {
        Player kicker = offense.Starter(Position.K)!;
        var outcome = new PlayOutcome { PlayType = PlayType.ExtraPoint, ClockSeconds = 4, ClockStops = true };

        CheckResult kick = CheckResolver.Resolve(rng,
            RatingMath.Modifier(kicker.Of(Skill.KickAccuracy), kicker.Attributes.Composure), defenderMod: 2);
        outcome.Checks.Add(kick);
        outcome.KickGood = kick.Success;
        outcome.BallCarrier = kicker;
        outcome.Trace.Add(kick.Describe($"XP    {kicker.Name}") + (kick.Success ? " — good" : " — MISSED"));
        return outcome;
    }
}
