using CfbSim.Core.Checks;
using CfbSim.Core.Model;
using CfbSim.Core.Rng;

namespace CfbSim.Core.Sim.Play;

/// <summary>
/// Pass plays (short/deep): protection → throw vs coverage → catch + YAC.
/// A protection collapse is a sack; a QB blunder or a big coverage win is an
/// interception. Deep passes are harder (lower completion, more picks) but bigger.
/// </summary>
public static class PassPlayResolver
{
    public static PlayOutcome Resolve(IRng rng, Team offense, Team defense, PlayType type, DefensiveKey key)
    {
        bool deep = type == PlayType.DeepPass;
        var outcome = new PlayOutcome { PlayType = type, ClockSeconds = 36 };

        Player qb = offense.Starter(Position.QB)!;
        Player wr = offense.Pick(Position.WR, rng) ?? offense.Starter(Position.WR)!;
        Player ol = offense.Starter(Position.OL)!;
        Player rusher = defense.Starter(Position.EDGE) ?? defense.Starter(Position.DL)!;
        Player cb = defense.Starter(Position.CB) ?? defense.Starter(Position.S)!;
        Player safety = defense.Starter(Position.S) ?? cb;

        outcome.Passer = qb;
        outcome.Receiver = wr;

        // Training-prep modifiers (temporary, this game only).
        int off = offense.ActiveBoost.OffenseBonus;
        int def = defense.ActiveBoost.DefenseBonus;

        int keyBonus = key == DefensiveKey.StopPass ? 3 : key == DefensiveKey.StopRun ? -2 : 0;

        // 1. Protection.
        CheckResult prot = CheckResolver.ResolveContest(rng,
            ol.Of(Skill.PassBlock) + off, ol.Attributes.Strength,
            rusher.Of(Skill.PassRush) + (key == DefensiveKey.StopPass ? 1 : 0) + def, rusher.Attributes.Strength);
        Add(outcome, prot, $"PROT  {ol.Name} (OL) vs {rusher.Name} (rush)");

        if (prot.Blunder || prot.Margin <= -6)
        {
            outcome.Sack = true;
            outcome.YardsGained = -rng.NextInt(5, 9);
            outcome.BallCarrier = qb;
            outcome.Defender = rusher;
            outcome.ClockSeconds = 38;
            outcome.Trace.Add($"      → SACK for {outcome.YardsGained}");
            return outcome;
        }

        // 2. The throw vs coverage.
        Skill accuracy = deep ? Skill.DeepAccuracy : Skill.ShortAccuracy;
        int coverageBonus = keyBonus + (deep ? 2 : 0); // deep throws are contested harder
        CheckResult throw_ = CheckResolver.ResolveContest(rng,
            qb.Of(accuracy) + off, qb.Attributes.Awareness,
            cb.Of(Skill.ManCoverage) + coverageBonus + def, cb.Attributes.Agility);
        Add(outcome, throw_, $"THROW {qb.Name} ({(deep ? "deep" : "short")}) vs {cb.Name} (cov)");

        // A nat-1 is sometimes a pick, sometimes just a throwaway; a big coverage win can also be picked.
        bool intercepted =
            (throw_.Blunder && rng.NextInt(1, 100) <= 50) ||
            (!throw_.Success && throw_.Margin <= -8 && rng.NextInt(1, 100) <= (deep ? 40 : 20));
        if (intercepted)
        {
            outcome.Turnover = TurnoverKind.Interception;
            outcome.Defender = cb;
            outcome.ClockStops = true;
            outcome.ClockSeconds = 10;
            outcome.Trace.Add($"      → INTERCEPTED by {cb.Name}!");
            return outcome;
        }

        if (!throw_.Success)
        {
            outcome.Incomplete = true;
            outcome.ClockStops = true;
            outcome.ClockSeconds = 6;
            outcome.Trace.Add("      → incomplete");
            return outcome;
        }

        // 3. Complete — air yards + YAC.
        int airBase = deep ? 16 : 5;
        int airYards = airBase + Clamp(Round(throw_.Margin * (deep ? 1.6 : 0.8)), 0, deep ? 34 : 14);

        CheckResult yacCheck = CheckResolver.ResolveContest(rng,
            wr.Of(Skill.RouteRunning) + off, wr.Attributes.Agility,
            safety.Of(Skill.Tackling) + def, safety.Attributes.Agility);
        Add(outcome, yacCheck, $"YAC   {wr.Name} (WR) vs {safety.Name}");
        int yac = yacCheck.Success ? Clamp(Round(yacCheck.Margin * 1.0), 0, 30) : 0;

        outcome.BallCarrier = wr;
        outcome.YardsGained = airYards + yac;
        if (rng.NextInt(1, 100) <= 20) { outcome.ClockStops = true; outcome.ClockSeconds = 30; } // out of bounds
        outcome.Trace.Add($"      → complete to {wr.Name} for {outcome.YardsGained} ({airYards} air, {yac} YAC)");
        return outcome;
    }

    private static void Add(PlayOutcome o, CheckResult c, string label)
    {
        o.Checks.Add(c);
        o.Trace.Add(c.Describe(label));
    }

    private static int Round(double v) => (int)Math.Round(v);
    private static int Clamp(int v, int lo, int hi) => Math.Clamp(v, lo, hi);
}
