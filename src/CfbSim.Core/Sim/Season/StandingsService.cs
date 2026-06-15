using CfbSim.Core.Model;

namespace CfbSim.Core.Sim.Season;

/// <summary>Computes conference standings (ordered) from season records.</summary>
public static class StandingsService
{
    public static List<TeamRecord> ConferenceStandings(
        League league, Conference conference,
        IReadOnlyDictionary<int, TeamRecord> records,
        IReadOnlyList<SeasonGameResult> games)
    {
        var teams = league.AllTeams.ToDictionary(t => t.Id);
        var confRecords = conference.Teams
            .Where(t => records.ContainsKey(t.Id))
            .Select(t => records[t.Id]);
        return TiebreakService.OrderConference(confRecords, teams, games);
    }

    /// <summary>The first-place team in each conference (the would-be championship-game pairing input).</summary>
    public static IEnumerable<(Conference Conference, TeamRecord Leader)> ConferenceLeaders(
        League league, IReadOnlyDictionary<int, TeamRecord> records, IReadOnlyList<SeasonGameResult> games)
    {
        foreach (Conference c in league.Conferences)
        {
            List<TeamRecord> standings = ConferenceStandings(league, c, records, games);
            if (standings.Count > 0)
                yield return (c, standings[0]);
        }
    }
}
