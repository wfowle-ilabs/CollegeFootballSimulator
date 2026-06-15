using CfbSim.Core.Model;

namespace CfbSim.Core.Sim.Season;

/// <summary>
/// A simple results-based poll. Rating blends record, strength of schedule (average
/// opponent prestige, a stand-in until real SOS), and program prestige as a soft
/// prior. Good enough to make the top of the poll look sane; a smarter pollster
/// (résumé/quality-win weighting) is a later pass.
/// </summary>
public static class RankingService
{
    public static List<RankedTeam> Rank(
        League league,
        IReadOnlyDictionary<int, TeamRecord> records,
        IReadOnlyList<SeasonGameResult> games,
        IReadOnlyDictionary<int, double>? achievementBonus = null)
    {
        var teams = league.AllTeams.ToDictionary(t => t.Id);
        var opponents = BuildOpponentMap(games);

        var rated = records.Values.Select(r =>
        {
            double sos = AverageOpponentPrestige(r.TeamId, opponents, teams);
            int prestige = teams.TryGetValue(r.TeamId, out Team? t) ? t.Prestige : 50;
            double bonus = achievementBonus is not null && achievementBonus.TryGetValue(r.TeamId, out double bp) ? bp : 0;
            double rating =
                r.Wins * 12.0
                - r.Losses * 7.0
                + sos * 0.25
                + prestige * 0.30
                + (r.PointsFor - r.PointsAgainst) * 0.02
                + bonus;
            return (TeamId: r.TeamId, Rating: rating, Record: r);
        })
        .OrderByDescending(x => x.Rating)
        .ToList();

        var ranked = new List<RankedTeam>(rated.Count);
        for (int i = 0; i < rated.Count; i++)
            ranked.Add(new RankedTeam(i + 1, rated[i].TeamId, rated[i].Rating, rated[i].Record));
        return ranked;
    }

    private static Dictionary<int, List<int>> BuildOpponentMap(IReadOnlyList<SeasonGameResult> games)
    {
        var map = new Dictionary<int, List<int>>();
        void Link(int a, int b) => (map.TryGetValue(a, out var l) ? l : map[a] = new List<int>()).Add(b);
        foreach (SeasonGameResult g in games)
        {
            Link(g.HomeId, g.AwayId);
            Link(g.AwayId, g.HomeId);
        }
        return map;
    }

    private static double AverageOpponentPrestige(int teamId, Dictionary<int, List<int>> opponents, IReadOnlyDictionary<int, Team> teams)
    {
        if (!opponents.TryGetValue(teamId, out var opps) || opps.Count == 0) return 50;
        return opps.Average(o => teams.TryGetValue(o, out Team? t) ? t.Prestige : 50);
    }
}
