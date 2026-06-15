using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Services;
using CfbSim.Core.Sim.Season;

namespace CfbSim.Core.Sim.Postseason;

/// <summary>
/// A light, non-CFP bowl slate for flavor: bowl-eligible teams (6+ wins) not in the
/// playoff are paired off by ranking. A first pass — real bowl tie-ins/selection order
/// are a later refinement.
/// </summary>
public static class BowlSelector
{
    private static readonly string[] BowlNames =
    {
        "Citrus Bowl", "Gator Bowl", "Sun Bowl", "Liberty Bowl", "Holiday Bowl",
        "Las Vegas Bowl", "Pinstripe Bowl", "Music City Bowl", "ReliaQuest Bowl",
        "Duke's Mayo Bowl", "Texas Bowl", "Alamo Bowl",
    };

    public static List<BowlResult> Run(
        IRng rng, List<RankedTeam> finalRankings,
        IEnumerable<CfpSeed> cfpField, IReadOnlyDictionary<int, Team> teams, SeriesHistory history, int year)
    {
        var inPlayoff = cfpField.Select(s => s.TeamId).ToHashSet();

        var eligible = finalRankings
            .Where(rt => !inPlayoff.Contains(rt.TeamId) && rt.Record.Wins >= 6)
            .Select(rt => rt.TeamId)
            .ToList();

        var bowls = new List<BowlResult>();
        int maxBowls = Math.Min(BowlNames.Length, eligible.Count / 2);
        for (int i = 0; i < maxBowls; i++)
        {
            int a = eligible[i * 2];
            int b = eligible[i * 2 + 1];
            (int winnerId, int homeScore, int awayScore) = NeutralGame.Play(rng, teams[a], teams[b]);
            int loserId = winnerId == a ? b : a;
            history.RecordResult(winnerId, loserId, year);
            bowls.Add(new BowlResult(BowlNames[i], a, b, homeScore, awayScore, winnerId));
        }

        return bowls;
    }
}
