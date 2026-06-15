using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Services;
using CfbSim.Core.Sim.Season;

namespace CfbSim.Core.Sim.Postseason;

/// <summary>
/// Conference championship games: the top two teams in each conference's standings
/// meet for the title. Results update season records and head-to-head history (they
/// feed the re-ranking used to pick the CFP field). Divisional pairings will plug in
/// here once conferences use divisions.
/// </summary>
public static class ConferenceChampionships
{
    public static (Dictionary<int, int> Champions, List<ChampionshipGameResult> Games, List<SeasonGameResult> AsSeasonGames) Run(
        IRng rng, League league, SeasonResult season, SeriesHistory history, IReadOnlyDictionary<int, Team> teams)
    {
        var champions = new Dictionary<int, int>();
        var games = new List<ChampionshipGameResult>();
        var asSeason = new List<SeasonGameResult>();

        foreach (Conference conference in league.Conferences)
        {
            List<TeamRecord> standings = StandingsService.ConferenceStandings(league, conference, season.Records, season.Games);
            if (standings.Count == 0) continue;
            if (standings.Count == 1)
            {
                champions[conference.Id] = standings[0].TeamId;
                continue;
            }

            int oneId = standings[0].TeamId;
            int twoId = standings[1].TeamId;
            (int winnerId, int oneScore, int twoScore) = NeutralGame.Play(rng, teams[oneId], teams[twoId]);
            int loserId = winnerId == oneId ? twoId : oneId;
            int winnerScore = Math.Max(oneScore, twoScore);
            int loserScore = Math.Min(oneScore, twoScore);

            champions[conference.Id] = winnerId;

            // Update records (a conference game) and head-to-head history.
            TeamRecord wr = season.Records[winnerId];
            TeamRecord lr = season.Records[loserId];
            wr.Wins++; wr.ConfWins++; wr.PointsFor += winnerScore; wr.PointsAgainst += loserScore;
            lr.Losses++; lr.ConfLosses++; lr.PointsFor += loserScore; lr.PointsAgainst += winnerScore;
            history.RecordResult(winnerId, loserId, season.Year);

            games.Add(new ChampionshipGameResult(conference.Id, conference.Abbreviation, winnerId, loserId, winnerScore, loserScore));
            asSeason.Add(new SeasonGameResult
            {
                Week = season.Games.Count == 0 ? 99 : season.Games.Max(g => g.Week) + 1,
                HomeId = oneId,
                AwayId = twoId,
                HomeScore = oneScore,
                AwayScore = twoScore,
                ConferenceGame = true,
            });
        }

        return (champions, games, asSeason);
    }
}
