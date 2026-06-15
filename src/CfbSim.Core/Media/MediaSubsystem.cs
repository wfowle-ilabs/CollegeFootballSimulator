using CfbSim.Core.Model;
using CfbSim.Core.Sim.Season;

namespace CfbSim.Core.Media;

/// <summary>
/// The flagship event consumer (docs/architecture.qmd §Media). For a concluded week it
/// builds a <see cref="NarrativeContext"/> per game, tiers coverage (featured games get a
/// full article; the rest a short recap), and writes the results to the media store. Pure
/// presentation derived from deterministic results — it never feeds the sim.
/// </summary>
public sealed class MediaSubsystem(INarrativeGenerator generator)
{
    public void GenerateWeek(
        int week, League league, SeasonState season,
        IReadOnlyList<RankedTeam> rankings, int? userTeamId, MediaStore store)
    {
        var teams = league.AllTeams.ToDictionary(t => t.Id);

        var rankOf = new Dictionary<int, int>();
        for (int i = 0; i < rankings.Count && i < 25; i++)
            rankOf[rankings[i].TeamId] = i + 1;

        foreach (SeasonGameResult game in season.Games.Where(g => g.Week == week))
        {
            NarrativeContext context = BuildContext(game, teams, rankOf, userTeamId, season.Year);
            ApplyTier(context);
            store.Add(generator.Generate(context));
        }
    }

    private static NarrativeContext BuildContext(
        SeasonGameResult game, IReadOnlyDictionary<int, Team> teams,
        IReadOnlyDictionary<int, int> rankOf, int? userTeamId, int year)
    {
        int winnerId = game.WinnerId, loserId = game.LoserId;
        Team winner = teams[winnerId], loser = teams[loserId];
        int winnerScore = Math.Max(game.HomeScore, game.AwayScore);
        int loserScore = Math.Min(game.HomeScore, game.AwayScore);

        int winnerRank = rankOf.GetValueOrDefault(winnerId);
        int loserRank = rankOf.GetValueOrDefault(loserId);

        bool prestigeUpset = loser.Prestige - winner.Prestige >= 12;
        bool rankUpset = loserRank is > 0 and <= 25 && winnerRank == 0;

        return new NarrativeContext
        {
            Year = year,
            Week = game.Week,
            WinnerId = winnerId,
            LoserId = loserId,
            WinnerName = winner.Name,
            LoserName = loser.Name,
            WinnerAbbr = winner.Abbreviation,
            LoserAbbr = loser.Abbreviation,
            WinnerScore = winnerScore,
            LoserScore = loserScore,
            WinnerRank = winnerRank,
            LoserRank = loserRank,
            IsRivalry = game.Rivalry,
            RivalryName = game.RivalryName,
            IsUpset = prestigeUpset || rankUpset,
            IsUserGame = userTeamId is { } id && (winnerId == id || loserId == id),
        };
    }

    private static void ApplyTier(NarrativeContext c)
    {
        bool bothRanked = c.WinnerRank is > 0 and <= 25 && c.LoserRank is > 0 and <= 25;
        c.Full = c.IsUserGame || c.IsRivalry || c.IsUpset || bothRanked || c.WinnerRank is > 0 and <= 10;
        c.Type = c.IsUpset ? ArticleType.UpsetAlert
            : c.IsRivalry ? ArticleType.RivalryResult
            : c.Full ? ArticleType.GameRecap
            : ArticleType.ShortRecap;
    }
}
