using CfbSim.Core.Checks;
using CfbSim.Core.Model;
using CfbSim.Core.Ratings;
using CfbSim.Core.Rng;

namespace CfbSim.Core.Sim.Play;

/// <summary>The result of a kickoff: the receiving team's starting spot (own-goal frame), or a
/// return touchdown.</summary>
public readonly record struct KickoffResult(int Spot, bool ReturnTouchdown);

/// <summary>
/// Special teams: punts (return, rare muff, rare block, rare house-call), place kicks (FG/XP with
/// a rare block), and kickoffs (mostly touchbacks; a rare return TD for a fast returner). Training
/// prep (the kicking team's <see cref="TeamBoost.SpecialBonus"/>) nudges the kicks. Return TDs are
/// gated on returner Speed so they stay rare and earned.
/// </summary>
public static class SpecialTeamsResolver
{
    public const int MaxFieldGoalYards = 60;

    // A returner needs real wheels to take one to the house.
    private const int FastReturner = 15;

    /// <summary>
    /// Net punt: gross distance minus a return. Rare outcomes: a muff (kicking team recovers), a
    /// block (return team recovers a short field), or a return touchdown (fast returner only).
    /// </summary>
    public static PlayOutcome Punt(IRng rng, Team offense, Team returnTeam)
    {
        Player punter = offense.Starter(Position.P) ?? offense.Starter(Position.K)!;
        var outcome = new PlayOutcome { PlayType = PlayType.Punt, ClockSeconds = 14, ClockStops = true };
        int st = offense.ActiveBoost.SpecialBonus;

        int mod = RatingMath.Modifier(punter.Of(Skill.KickPower) + st, punter.Attributes.Composure);
        int gross = (int)Math.Round(42 + mod * 3 + rng.NextGaussian() * 5);

        // Rare block → return team recovers behind the line of scrimmage (short field).
        if (rng.NextInt(1, 100) <= 2)
        {
            outcome.Blocked = true;
            outcome.YardsGained = -rng.NextInt(2, 8);
            outcome.BallCarrier = punter;
            outcome.Trace.Add($"PUNT  {punter.Name}: BLOCKED — return team recovers");
            return outcome;
        }

        Player returner = PickReturner(rng, returnTeam) ?? punter;
        int speed = returner.Attributes.Speed;
        int retMod = RatingMath.Modifier(returner.Of(Skill.Elusiveness), speed);

        int roll = rng.NextInt(1, 100);
        if (roll <= 3 - Math.Max(0, retMod)) // rare muff (better returners muff less)
        {
            outcome.Muffed = true;
            outcome.YardsGained = gross;
            outcome.BallCarrier = punter;
            outcome.Trace.Add($"PUNT  {punter.Name}: {gross} gross — MUFFED, kicking team recovers!");
            return outcome;
        }

        // Rare house call — only a fast returner, on a true return (not a fair catch).
        if (speed >= FastReturner && roll > 65 && rng.NextInt(1, 1000) <= 3 + (speed - FastReturner) * 2)
        {
            outcome.ReturnTouchdown = true;
            outcome.BallCarrier = returner;
            outcome.Trace.Add($"PUNT  returned for a TOUCHDOWN by {returner.Name}!");
            return outcome;
        }

        int returnYards = roll <= 55 ? 0 : Math.Clamp((int)Math.Round(6 + retMod * 2 + rng.NextGaussian() * 5), 0, 45);
        outcome.YardsGained = Math.Clamp(gross - returnYards, 18, 70);
        outcome.BallCarrier = punter;
        outcome.Trace.Add($"PUNT  {punter.Name}: {gross} gross − {returnYards} ret = {outcome.YardsGained} net");
        return outcome;
    }

    /// <summary>Kickoff coverage: mostly touchbacks to the 25; some returns into the 20s–40s; a
    /// rare return TD for a fast returner.</summary>
    public static KickoffResult Kickoff(IRng rng, Team receivingTeam)
    {
        if (rng.NextInt(1, 100) <= 65) return new KickoffResult(25, false); // touchback

        Player? returner = PickReturner(rng, receivingTeam);
        int speed = returner?.Attributes.Speed ?? 10;
        int retMod = returner is null ? 0 : RatingMath.Modifier(returner.Of(Skill.Elusiveness), speed);

        if (speed >= FastReturner && rng.NextInt(1, 1000) <= 4 + (speed - FastReturner) * 2)
            return new KickoffResult(100, true); // house call

        int spot = Math.Clamp((int)Math.Round(22 + retMod * 2 + rng.NextGaussian() * 6), 12, 48);
        return new KickoffResult(spot, false);
    }

    /// <summary>Field goal from the given distance (yards, including the 17-yd snap+kick).</summary>
    public static PlayOutcome FieldGoal(IRng rng, Team offense, int distanceYards)
    {
        Player kicker = offense.Starter(Position.K)!;
        var outcome = new PlayOutcome { PlayType = PlayType.FieldGoal, ClockSeconds = 6, ClockStops = true };
        int st = offense.ActiveBoost.SpecialBonus;

        if (distanceYards > MaxFieldGoalYards)
        {
            outcome.KickGood = false;
            outcome.Trace.Add($"FG    {distanceYards} yds — out of range, no good");
            return outcome;
        }

        if (rng.NextInt(1, 100) <= 3) // rare block
        {
            outcome.Blocked = true;
            outcome.KickGood = false;
            outcome.Trace.Add($"FG    {kicker.Name} {distanceYards} yds — BLOCKED");
            return outcome;
        }

        // DC rises with distance: a chip shot is easy, a 55-yarder is hard.
        int distanceDc = (distanceYards - 25) / 3; // ~0 at 25 yds, ~12 at 60
        CheckResult kick = CheckResolver.Resolve(rng,
            RatingMath.Modifier(kicker.Of(Skill.KickAccuracy) + st, kicker.Attributes.Composure), distanceDc);
        outcome.Checks.Add(kick);
        outcome.KickGood = kick.Success;
        outcome.BallCarrier = kicker;
        outcome.Trace.Add(kick.Describe($"FG    {kicker.Name} {distanceYards} yds") + (kick.Success ? " — GOOD" : " — no good"));
        return outcome;
    }

    /// <summary>Extra point — effectively a ~33-yard field goal, with a rare block.</summary>
    public static PlayOutcome ExtraPoint(IRng rng, Team offense)
    {
        Player kicker = offense.Starter(Position.K)!;
        var outcome = new PlayOutcome { PlayType = PlayType.ExtraPoint, ClockSeconds = 4, ClockStops = true };
        int st = offense.ActiveBoost.SpecialBonus;

        if (rng.NextInt(1, 100) <= 2) // rare block
        {
            outcome.Blocked = true;
            outcome.KickGood = false;
            outcome.Trace.Add($"XP    {kicker.Name} — BLOCKED");
            return outcome;
        }

        CheckResult kick = CheckResolver.Resolve(rng,
            RatingMath.Modifier(kicker.Of(Skill.KickAccuracy) + st, kicker.Attributes.Composure), defenderMod: 2);
        outcome.Checks.Add(kick);
        outcome.KickGood = kick.Success;
        outcome.BallCarrier = kicker;
        outcome.Trace.Add(kick.Describe($"XP    {kicker.Name}") + (kick.Success ? " — good" : " — MISSED"));
        return outcome;
    }

    private static Player? PickReturner(IRng rng, Team team)
        => team.Pick(Position.WR, rng) ?? team.Starter(Position.RB)
           ?? team.Starter(Position.CB) ?? team.Roster.FirstOrDefault();
}
