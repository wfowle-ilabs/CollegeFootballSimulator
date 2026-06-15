using CfbSim.Core.Checks;
using CfbSim.Core.Model;
using CfbSim.Core.Ratings;
using CfbSim.Core.Rng;

namespace CfbSim.Core.Sim.Play;

/// <summary>
/// Run plays (inside/outside) as a chain of checks: push → second level → open
/// field (see docs/mechanics.qmd). Outside runs use edge blocking/pursuit and a
/// wider yardage spread. Field-agnostic — the drive layer caps at the goal line.
/// </summary>
public static class RunPlayResolver
{
    private const int FumbleDc = 13;
    private const int BigHitMargin = -8;

    public static PlayOutcome Resolve(IRng rng, Team offense, Team defense, PlayType type, DefensiveKey key)
    {
        bool outside = type == PlayType.OutsideRun;
        var outcome = new PlayOutcome { PlayType = type, ClockSeconds = outside ? 38 : 40 };

        Player rb = offense.Pick(Position.RB, rng) ?? offense.Starter(Position.RB)!;
        Player ol = offense.Starter(Position.OL)!;
        Player front = outside
            ? (defense.Starter(Position.EDGE) ?? defense.Starter(Position.DL)!)
            : defense.Starter(Position.DL)!;
        Player lb = defense.Starter(Position.LB)!;
        Player pursuit = defense.Starter(Position.S) ?? lb;
        outcome.BallCarrier = rb;

        int keyBonus = key == DefensiveKey.StopRun ? 3 : key == DefensiveKey.StopPass ? -2 : 0;

        // 1. The push (trenches / edge).
        Skill blockSkill = outside ? Skill.PullSpeed : Skill.RunBlock;
        int blockAttr = outside ? ol.Attributes.Agility : ol.Attributes.Strength;
        Skill shedSkill = outside ? Skill.Pursuit : Skill.BlockShed;
        int shedAttr = outside ? front.Attributes.Speed : front.Attributes.Strength;

        CheckResult push = CheckResolver.ResolveContest(rng,
            ol.Of(blockSkill), blockAttr,
            front.Of(shedSkill) + keyBonus, shedAttr);
        Add(outcome, push, $"PUSH  {ol.Name} (OL) vs {front.Name}");

        if (push.Blunder)
        {
            outcome.YardsGained = rng.NextInt(-3, -1);
            outcome.Trace.Add($"      → TFL for {outcome.YardsGained}");
            return outcome;
        }

        int lineYards = Clamp(Round(2.5 + push.Margin * 0.9), -3, outside ? 15 : 13);
        outcome.YardsGained = lineYards;

        if (push.Crit)
        {
            outcome.Trace.Add($"      → clean lane! ({lineYards} and gone)");
            return OpenField(rng, rb, pursuit, outcome, outside);
        }

        if (!push.Success)
        {
            outcome.Trace.Add($"      → stuffed for {lineYards}");
            MaybeOutOfBounds(rng, outcome, outside);
            return outcome;
        }

        // 2. Second level.
        CheckResult second = CheckResolver.ResolveContest(rng,
            rb.Of(Skill.Elusiveness), rb.Attributes.Agility,
            lb.Of(Skill.Tackling) + keyBonus, lb.Attributes.Agility);
        Add(outcome, second, $"2ND   {rb.Name} (RB) vs {lb.Name} (LB)");

        if (second.Blunder || second.Margin <= BigHitMargin)
        {
            CheckResult ball = CheckResolver.Resolve(rng,
                RatingMath.Modifier(rb.Of(Skill.BallSecurity), rb.Attributes.Composure), FumbleDc - 10);
            Add(outcome, ball, $"BALL  {rb.Name} ball security");
            if (!ball.Success)
            {
                outcome.Turnover = TurnoverKind.FumbleLost;
                outcome.Defender = lb;
                outcome.ClockStops = true;
                outcome.Trace.Add("      → FUMBLE, lost!");
                return outcome;
            }
        }

        if (second.Crit)
        {
            outcome.Trace.Add("      → into the open!");
            return OpenField(rng, rb, pursuit, outcome, outside);
        }

        if (!second.Success)
        {
            outcome.Trace.Add($"      → tackled for {outcome.YardsGained}");
            MaybeOutOfBounds(rng, outcome, outside);
            return outcome;
        }

        int yac = Clamp(Round(second.Margin * (outside ? 1.3 : 1.0)), 0, outside ? 26 : 20);
        outcome.YardsGained += yac;

        if (second.Margin >= 8)
        {
            outcome.Trace.Add($"      → breaks free (+{yac})!");
            return OpenField(rng, rb, pursuit, outcome, outside);
        }

        outcome.Trace.Add($"      → down after {outcome.YardsGained}");
        MaybeOutOfBounds(rng, outcome, outside);
        return outcome;
    }

    private static PlayOutcome OpenField(IRng rng, Player rb, Player pursuit, PlayOutcome outcome, bool outside)
    {
        CheckResult open = CheckResolver.ResolveContest(rng,
            rb.Attributes.Speed, rb.Attributes.Speed,
            pursuit.Attributes.Speed, pursuit.Attributes.Speed);
        Add(outcome, open, $"OPEN  {rb.Name} vs {pursuit.Name} pursuit");

        int breakaway = Clamp(Round(8 + open.Margin * 2.5), 0, 80);
        outcome.YardsGained += breakaway;
        outcome.Trace.Add($"      → {breakaway} in the open ({outcome.YardsGained} total)");
        MaybeOutOfBounds(rng, outcome, outside);
        return outcome;
    }

    private static void MaybeOutOfBounds(IRng rng, PlayOutcome outcome, bool outside)
    {
        if (outside && outcome.YardsGained > 0 && rng.NextInt(1, 100) <= 25)
        {
            outcome.ClockStops = true;
            outcome.ClockSeconds = 30;
        }
    }

    private static void Add(PlayOutcome o, CheckResult c, string label)
    {
        o.Checks.Add(c);
        o.Trace.Add(c.Describe(label));
    }

    private static int Round(double v) => (int)Math.Round(v);
    private static int Clamp(int v, int lo, int hi) => Math.Clamp(v, lo, hi);
}
