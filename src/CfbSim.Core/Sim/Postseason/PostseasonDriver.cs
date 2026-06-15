using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Services;
using CfbSim.Core.Sim.Game;
using CfbSim.Core.Sim.Season;
using CfbSim.Core.Stats;

namespace CfbSim.Core.Sim.Postseason;

/// <summary>
/// Drives the postseason so it can be watched a round at a time. <see cref="Begin"/> runs
/// the conference championships, re-ranks, and seeds the 12-team field (building an empty
/// bracket); <see cref="AdvanceRound"/> simulates the next round and propagates winners.
/// When the bracket finishes it assembles the final <see cref="PostseasonResult"/>.
/// <see cref="PostseasonSimulator"/> wraps this to run it all at once (headless).
/// </summary>
public static class PostseasonDriver
{
    public static PostseasonState Begin(IRng rng, League league, SeriesHistory history, SeasonResult season)
    {
        var teams = league.AllTeams.ToDictionary(t => t.Id);
        var (champions, champGames, champSeasonGames) = ConferenceChampionships.Run(rng, league, season, history, teams);

        var seeding = new List<SeasonGameResult>(season.Games);
        seeding.AddRange(champSeasonGames);
        List<RankedTeam> selection = RankingService.Rank(league, season.Records, seeding);
        List<CfpSeed> field = CfpSelector.Select(selection, champions);

        var state = new PostseasonState
        {
            Year = season.Year,
            Champions = champions,
            Records = season.Records,
            Bracket = BuildBracket(field),
        };
        state.ChampionshipGames.AddRange(champGames);
        state.SelectionRankings.AddRange(selection);
        state.SeedingGames.AddRange(seeding);
        return state;
    }

    public static void AdvanceRound(IRng rng, PostseasonState s, League league, SeriesHistory history,
        Action<string, int, int, BoxScore>? boxSink = null)
    {
        if (s.Bracket.IsComplete) return;
        var teams = league.AllTeams.ToDictionary(t => t.Id);

        int played = s.Bracket.NextRound;
        List<BracketGame> round = played switch
        {
            1 => s.Bracket.FirstRound,
            2 => s.Bracket.Quarterfinals,
            3 => s.Bracket.Semifinals,
            _ => s.Bracket.Championship,
        };

        foreach (BracketGame g in round.Where(g => g.ReadyToPlay))
            PlayGame(rng, g, teams, history, s.Records, s.Year, boxSink);

        Propagate(s.Bracket, played);
        s.Bracket.NextRound++;

        if (s.Bracket.IsComplete)
        {
            s.Bracket.ChampionId = s.Bracket.Championship[0].WinnerId;
            Finalize(rng, s, league, history, teams);
        }
    }

    // --- bracket construction & propagation ---

    private static BracketState BuildBracket(List<CfpSeed> field)
    {
        var bySeed = field.ToDictionary(s => s.Seed, s => s.TeamId);
        var b = new BracketState();
        b.Field.AddRange(field);
        b.FirstRound.Add(Pair("First Round", 8, bySeed[8], 9, bySeed[9]));
        b.FirstRound.Add(Pair("First Round", 5, bySeed[5], 12, bySeed[12]));
        b.FirstRound.Add(Pair("First Round", 7, bySeed[7], 10, bySeed[10]));
        b.FirstRound.Add(Pair("First Round", 6, bySeed[6], 11, bySeed[11]));
        b.Quarterfinals.Add(Bye("Quarterfinal", 1, bySeed[1]));
        b.Quarterfinals.Add(Bye("Quarterfinal", 4, bySeed[4]));
        b.Quarterfinals.Add(Bye("Quarterfinal", 2, bySeed[2]));
        b.Quarterfinals.Add(Bye("Quarterfinal", 3, bySeed[3]));
        b.Semifinals.Add(new BracketGame { Round = "Semifinal" });
        b.Semifinals.Add(new BracketGame { Round = "Semifinal" });
        b.Championship.Add(new BracketGame { Round = "National Championship" });
        return b;
    }

    private static BracketGame Pair(string round, int seedA, int teamA, int seedB, int teamB)
        => seedA < seedB
            ? new BracketGame { Round = round, SeedA = seedA, TeamA = teamA, SeedB = seedB, TeamB = teamB }
            : new BracketGame { Round = round, SeedA = seedB, TeamA = teamB, SeedB = seedA, TeamB = teamA };

    private static BracketGame Bye(string round, int seed, int teamId)
        => new() { Round = round, SeedA = seed, TeamA = teamId };

    private static void Propagate(BracketState b, int playedRound)
    {
        if (playedRound == 1)
            for (int i = 0; i < 4; i++) FillAway(b.Quarterfinals[i], b.FirstRound[i]);
        else if (playedRound == 2)
        {
            FillGame(b.Semifinals[0], b.Quarterfinals[0], b.Quarterfinals[1]);
            FillGame(b.Semifinals[1], b.Quarterfinals[2], b.Quarterfinals[3]);
        }
        else if (playedRound == 3)
            FillGame(b.Championship[0], b.Semifinals[0], b.Semifinals[1]);
    }

    private static void FillAway(BracketGame target, BracketGame source)
    {
        target.TeamB = source.WinnerId;
        target.SeedB = source.WinnerSeed;
    }

    private static void FillGame(BracketGame target, BracketGame a, BracketGame b)
    {
        if (a.WinnerSeed <= b.WinnerSeed)
        {
            target.TeamA = a.WinnerId; target.SeedA = a.WinnerSeed;
            target.TeamB = b.WinnerId; target.SeedB = b.WinnerSeed;
        }
        else
        {
            target.TeamA = b.WinnerId; target.SeedA = b.WinnerSeed;
            target.TeamB = a.WinnerId; target.SeedB = a.WinnerSeed;
        }
    }

    private static void PlayGame(IRng rng, BracketGame g, IReadOnlyDictionary<int, Team> teams,
        SeriesHistory history, IReadOnlyDictionary<int, TeamRecord> records, int year,
        Action<string, int, int, BoxScore>? boxSink)
    {
        GameResult result = GameSimulator.Simulate(rng, teams[g.TeamA], teams[g.TeamB]);
        int winner = result.HomeScore >= result.AwayScore ? g.TeamA : g.TeamB;
        int loser = winner == g.TeamA ? g.TeamB : g.TeamA;
        g.Result = new CfpGameResult(g.Round, g.TeamA, g.TeamB, result.HomeScore, result.AwayScore, winner);
        history.RecordResult(winner, loser, year);
        ApplyToRecords(records, g.TeamA, g.TeamB, result.HomeScore, result.AwayScore);
        boxSink?.Invoke(g.Round, g.TeamA, g.TeamB, result.Box);
    }

    // --- finalize ---

    private static void Finalize(IRng rng, PostseasonState s, League league, SeriesHistory history, IReadOnlyDictionary<int, Team> teams)
    {
        List<BowlResult> bowls = BowlSelector.Run(rng, s.SelectionRankings, s.Bracket.Field, teams, history, s.Year);
        foreach (BowlResult b in bowls)
            ApplyToRecords(s.Records, b.HomeId, b.AwayId, b.HomeScore, b.AwayScore);

        var cfpGames = s.Bracket.AllGames.Where(g => g.Result is not null).Select(g => g.Result!).ToList();

        var allGames = new List<SeasonGameResult>(s.SeedingGames);
        allGames.AddRange(cfpGames.Select(AsSeasonGame));
        allGames.AddRange(bowls.Select(AsSeasonGame));
        Dictionary<int, double> bonus = BuildAchievementBonus(s.Champions, cfpGames, bowls);
        List<RankedTeam> finalRankings = RankingService.Rank(league, s.Records, allGames, bonus);

        var result = new PostseasonResult { Year = s.Year, NationalChampionId = s.Bracket.ChampionId };
        result.ConferenceChampions = s.Champions;
        result.ChampionshipGames.AddRange(s.ChampionshipGames);
        result.SelectionRankings.AddRange(s.SelectionRankings);
        result.CfpField.AddRange(s.Bracket.Field);
        result.CfpGames.AddRange(cfpGames);
        result.Bowls.AddRange(bowls);
        result.FinalRankings.AddRange(finalRankings);
        s.Result = result;
    }

    private static void ApplyToRecords(IReadOnlyDictionary<int, TeamRecord> records, int homeId, int awayId, int homeScore, int awayScore)
    {
        TeamRecord home = records[homeId];
        TeamRecord away = records[awayId];
        home.PointsFor += homeScore; home.PointsAgainst += awayScore;
        away.PointsFor += awayScore; away.PointsAgainst += homeScore;
        if (homeScore >= awayScore) { home.Wins++; away.Losses++; }
        else { away.Wins++; home.Losses++; }
    }

    private static Dictionary<int, double> BuildAchievementBonus(
        IReadOnlyDictionary<int, int> champions, List<CfpGameResult> cfpGames, List<BowlResult> bowls)
    {
        var bonus = new Dictionary<int, double>();
        void Add(int id, double v) => bonus[id] = bonus.GetValueOrDefault(id) + v;

        foreach (int championId in champions.Values) Add(championId, 5);
        foreach (CfpGameResult g in cfpGames)
            Add(g.WinnerId, g.Round switch
            {
                "First Round" => 6,
                "Quarterfinal" => 12,
                "Semifinal" => 18,
                "National Championship" => 30,
                _ => 0,
            });
        foreach (BowlResult b in bowls)
        {
            Add(b.WinnerId, 4);
            Add(b.WinnerId == b.HomeId ? b.AwayId : b.HomeId, 1);
        }
        return bonus;
    }

    private static SeasonGameResult AsSeasonGame(CfpGameResult g) => new()
    {
        Week = 100, HomeId = g.HomeId, AwayId = g.AwayId, HomeScore = g.HomeScore, AwayScore = g.AwayScore,
    };

    private static SeasonGameResult AsSeasonGame(BowlResult b) => new()
    {
        Week = 101, HomeId = b.HomeId, AwayId = b.AwayId, HomeScore = b.HomeScore, AwayScore = b.AwayScore,
    };
}
