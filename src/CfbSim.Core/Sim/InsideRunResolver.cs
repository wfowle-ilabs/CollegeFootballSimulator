using CfbSim.Core.Checks;
using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Sim.Play;

namespace CfbSim.Core.Sim;

/// <summary>The outcome of one inside-run carry, with a full check trace (M1 shape).</summary>
public sealed class InsideRunResult
{
    public int Yards { get; set; }
    public bool Fumble { get; set; }
    public List<CheckResult> Checks { get; } = new();
    public List<string> Trace { get; } = new();
}

/// <summary>
/// M1 entry point for a standalone inside-run carry. Now a thin adapter over the
/// unified <see cref="RunPlayResolver"/> so there's one source of truth for the
/// run chain. See docs/mechanics.qmd.
/// </summary>
public static class InsideRunResolver
{
    public static InsideRunResult Resolve(IRng rng, Team offense, Team defense)
    {
        PlayOutcome outcome = RunPlayResolver.Resolve(rng, offense, defense, PlayType.InsideRun, DefensiveKey.Balanced);

        var result = new InsideRunResult
        {
            Yards = outcome.YardsGained,
            Fumble = outcome.Turnover == TurnoverKind.FumbleLost,
        };
        result.Checks.AddRange(outcome.Checks);
        result.Trace.AddRange(outcome.Trace);
        return result;
    }
}
