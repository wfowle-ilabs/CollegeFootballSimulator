using CfbSim.Core.Model;

namespace CfbSim.Core.Sim.Season;

/// <summary>
/// Orders teams within a conference. v1 tiebreakers: conference win %, then overall
/// win %, then head-to-head (if they met), then prestige. Richer NCAA tiebreak chains
/// (common opponents, record vs. ranked, etc.) are a later pass.
/// </summary>
public static class TiebreakService
{
    public static List<TeamRecord> OrderConference(
        IEnumerable<TeamRecord> records,
        IReadOnlyDictionary<int, Team> teams,
        IReadOnlyList<SeasonGameResult> games)
    {
        return records
            .OrderByDescending(r => r.ConfWinPct)
            .ThenByDescending(r => r.WinPct)
            .ThenByDescending(r => HeadToHeadEdge(r, records, games))
            .ThenByDescending(r => teams.TryGetValue(r.TeamId, out Team? t) ? t.Prestige : 0)
            .ToList();
    }

    // +1 if this team has a winning H2H vs the other tied teams, -1 if losing, else 0.
    private static int HeadToHeadEdge(TeamRecord team, IEnumerable<TeamRecord> group, IReadOnlyList<SeasonGameResult> games)
    {
        var groupIds = group.Select(r => r.TeamId).ToHashSet();
        int edge = 0;
        foreach (SeasonGameResult g in games)
        {
            if (!g.Involves(team.TeamId)) continue;
            int opp = g.HomeId == team.TeamId ? g.AwayId : g.HomeId;
            if (!groupIds.Contains(opp)) continue;
            edge += g.WinnerId == team.TeamId ? 1 : -1;
        }
        return edge;
    }

    private static bool Involves(this SeasonGameResult g, int teamId) => g.HomeId == teamId || g.AwayId == teamId;
}
