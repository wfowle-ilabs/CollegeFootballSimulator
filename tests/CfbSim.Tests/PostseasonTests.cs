using CfbSim.Core.Generation;
using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Services;
using CfbSim.Core.Sim.Postseason;
using CfbSim.Core.Sim.Season;
using Xunit;

namespace CfbSim.Tests;

public class PostseasonTests
{
    private sealed record Run(League League, SeriesHistory History, SeasonResult Season, PostseasonResult Post);

    private static Run Simulate(ulong seed)
    {
        var rng = new Pcg32Rng(seed);
        League league = LeagueBuilder.Build(rng);
        SeriesHistory history = SeriesHistory.SeededFor(league);
        Schedule schedule = ScheduleBuilder.Build(rng, league, history, year: 2026);
        SeasonResult season = SeasonSimulator.Run(rng, league, schedule, history);
        PostseasonResult post = PostseasonSimulator.Run(rng, league, history, season);
        return new Run(league, history, season, post);
    }

    [Fact]
    public void EveryConference_HasAChampion()
    {
        Run r = Simulate(1);
        Assert.Equal(r.League.Conferences.Count, r.Post.ConferenceChampions.Count);
        foreach (Conference c in r.League.Conferences)
        {
            Assert.True(r.Post.ConferenceChampions.ContainsKey(c.Id));
            int champId = r.Post.ConferenceChampions[c.Id];
            Assert.Contains(c.Teams, t => t.Id == champId); // champion is a member
        }
    }

    [Fact]
    public void CfpField_Is12_SeededOneTo12_WithFiveChampionAutoBids()
    {
        Run r = Simulate(1);
        var field = r.Post.CfpField;
        Assert.Equal(12, field.Count);
        Assert.Equal(Enumerable.Range(1, 12), field.Select(s => s.Seed));
        Assert.Equal(12, field.Select(s => s.TeamId).Distinct().Count());
        Assert.True(field.Count(s => s.ConferenceChampion) >= 5); // at least the 5 auto-bids

        // Seeds are in selection-ranking order (seed 1 outranks seed 12).
        var rankOf = r.Post.SelectionRankings.Select((rt, i) => (rt.TeamId, rank: i + 1))
            .ToDictionary(x => x.TeamId, x => x.rank);
        for (int i = 1; i < field.Count; i++)
            Assert.True(rankOf[field[i].TeamId] >= rankOf[field[i - 1].TeamId]);
    }

    [Fact]
    public void PostseasonGames_CountTowardRecords_AndChampionFinishesNumberOne()
    {
        Run r = Simulate(1);

        // Every game played (regular + championships + CFP + bowls) shows up as one win.
        int totalGames = r.Season.Games.Count + r.Post.ChampionshipGames.Count
            + r.Post.CfpGames.Count + r.Post.Bowls.Count;
        int totalWins = r.Season.Records.Values.Sum(x => x.Wins);
        int totalLosses = r.Season.Records.Values.Sum(x => x.Losses);
        Assert.Equal(totalGames, totalWins);
        Assert.Equal(totalWins, totalLosses);

        // The national champion is #1 in the final poll, ahead of the regular-season leader.
        Assert.Equal(r.Post.NationalChampionId, r.Post.FinalRankings[0].TeamId);
    }

    [Fact]
    public void Bracket_HasElevenGames_AndChampionIsFromTheField()
    {
        Run r = Simulate(1);
        Assert.Equal(11, r.Post.CfpGames.Count); // 4 first round + 4 QF + 2 SF + 1 final
        Assert.Equal(4, r.Post.CfpGames.Count(g => g.Round == "First Round"));
        Assert.Equal(4, r.Post.CfpGames.Count(g => g.Round == "Quarterfinal"));
        Assert.Equal(2, r.Post.CfpGames.Count(g => g.Round == "Semifinal"));
        Assert.Equal(1, r.Post.CfpGames.Count(g => g.Round == "National Championship"));

        Assert.Contains(r.Post.CfpField, s => s.TeamId == r.Post.NationalChampionId);
    }

    [Fact]
    public void TopSeeds_GetByes_OnlyAppearInQuarterfinals()
    {
        Run r = Simulate(1);
        var bySeed = r.Post.CfpField.ToDictionary(s => s.Seed, s => s.TeamId);
        var firstRoundTeams = r.Post.CfpGames
            .Where(g => g.Round == "First Round")
            .SelectMany(g => new[] { g.HomeId, g.AwayId })
            .ToHashSet();

        for (int seed = 1; seed <= 4; seed++)
            Assert.DoesNotContain(bySeed[seed], firstRoundTeams); // byes
        for (int seed = 5; seed <= 12; seed++)
            Assert.Contains(bySeed[seed], firstRoundTeams);
    }

    [Fact]
    public void Postseason_IsDeterministic()
    {
        Run a = Simulate(3);
        Run b = Simulate(3);
        Assert.Equal(a.Post.NationalChampionId, b.Post.NationalChampionId);
        Assert.Equal(a.Post.CfpField.Select(s => s.TeamId), b.Post.CfpField.Select(s => s.TeamId));
    }
}
