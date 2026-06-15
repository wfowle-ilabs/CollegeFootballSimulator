using CfbSim.Core.Sim.Season;

namespace CfbSim.Core.Sim.Postseason;

/// <summary>
/// Builds the 12-team College Football Playoff field (current format):
/// the 5 highest-ranked conference champions get automatic bids, plus the 7
/// highest-ranked at-large teams. Seeding is straight by ranking (1–12), so the
/// top four seeds (with first-round byes) go to the four highest-ranked teams in
/// the field — not necessarily conference champions.
/// </summary>
public static class CfpSelector
{
    public const int FieldSize = 12;
    public const int AutoBids = 5;

    public static List<CfpSeed> Select(List<RankedTeam> finalRankings, IReadOnlyDictionary<int, int> conferenceChampions)
    {
        var rankOf = new Dictionary<int, int>();
        for (int i = 0; i < finalRankings.Count; i++)
            rankOf[finalRankings[i].TeamId] = i + 1;

        var championIds = conferenceChampions.Values.ToHashSet();

        // 5 highest-ranked conference champions → automatic bids.
        var autoBids = championIds
            .OrderBy(id => rankOf.GetValueOrDefault(id, int.MaxValue))
            .Take(AutoBids)
            .ToHashSet();

        // 7 highest-ranked teams not already in → at-large.
        var field = new HashSet<int>(autoBids);
        foreach (RankedTeam rt in finalRankings)
        {
            if (field.Count >= FieldSize) break;
            field.Add(rt.TeamId);
        }

        // Straight seeding by ranking.
        return field
            .OrderBy(id => rankOf.GetValueOrDefault(id, int.MaxValue))
            .Select((id, i) => new CfpSeed(i + 1, id, championIds.Contains(id)))
            .ToList();
    }
}
