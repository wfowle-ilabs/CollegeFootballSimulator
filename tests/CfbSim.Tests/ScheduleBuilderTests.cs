using CfbSim.Core.Generation;
using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Services;
using CfbSim.Core.Sim.Season;
using Xunit;

namespace CfbSim.Tests;

public class ScheduleBuilderTests
{
    private static (League league, SeriesHistory history, Schedule schedule) Build(ulong seed = 1)
    {
        League league = LeagueBuilder.Build(new Pcg32Rng(seed));
        SeriesHistory history = SeriesHistory.SeededFor(league);
        Schedule schedule = ScheduleBuilder.Build(new Pcg32Rng(seed + 100), league, history, year: 2026);
        return (league, history, schedule);
    }

    [Fact]
    public void NoTeam_PlaysTwiceInAWeek()
    {
        var (_, _, schedule) = Build();
        foreach (var weekGames in schedule.Games.GroupBy(g => g.Week))
        {
            var seen = new HashSet<int>();
            foreach (Matchup g in weekGames)
            {
                Assert.True(seen.Add(g.HomeId), $"team {g.HomeId} doubled in week {g.Week}");
                Assert.True(seen.Add(g.AwayId), $"team {g.AwayId} doubled in week {g.Week}");
            }
        }
    }

    [Fact]
    public void EveryTeam_PlaysAFullishSchedule()
    {
        var (league, _, schedule) = Build();
        foreach (Team t in league.AllTeams)
        {
            int games = schedule.For(t.Id).Count();
            Assert.InRange(games, 10, 14);
        }
    }

    [Fact]
    public void NoDuplicateOrSelfMatchups()
    {
        var (_, _, schedule) = Build();
        var seen = new HashSet<(int, int)>();
        foreach (Matchup g in schedule.Games)
        {
            Assert.NotEqual(g.HomeId, g.AwayId);
            (int, int) key = g.HomeId < g.AwayId ? (g.HomeId, g.AwayId) : (g.AwayId, g.HomeId);
            Assert.True(seen.Add(key), "duplicate matchup");
        }
    }

    [Fact]
    public void ProtectedRivalries_AreAllScheduled_AndNamed()
    {
        var (_, history, schedule) = Build();
        foreach (SeriesRecord rivalry in history.Rivalries)
        {
            Matchup? game = schedule.Games.FirstOrDefault(g =>
                (g.HomeId == rivalry.TeamAId && g.AwayId == rivalry.TeamBId) ||
                (g.HomeId == rivalry.TeamBId && g.AwayId == rivalry.TeamAId));
            Assert.NotNull(game);
            Assert.True(game!.Rivalry);
            Assert.Equal(rivalry.RivalryName, game.RivalryName);
        }
    }

    [Fact]
    public void ConferenceGames_AreWithinConference()
    {
        var (league, _, schedule) = Build();
        var confOf = league.AllTeams.ToDictionary(t => t.Id, t => t.ConferenceId);
        foreach (Matchup g in schedule.Games.Where(g => g.ConferenceGame))
        {
            Assert.Equal(confOf[g.HomeId], confOf[g.AwayId]);
            Assert.NotEqual(0, confOf[g.HomeId]); // independents have no conference games
        }
    }

    [Fact]
    public void Schedule_IsDeterministic()
    {
        var (_, _, a) = Build(5);
        var (_, _, b) = Build(5);
        Assert.Equal(a.Games.Count, b.Games.Count);
        for (int i = 0; i < a.Games.Count; i++)
        {
            Assert.Equal(a.Games[i].Week, b.Games[i].Week);
            Assert.Equal(a.Games[i].HomeId, b.Games[i].HomeId);
            Assert.Equal(a.Games[i].AwayId, b.Games[i].AwayId);
        }
    }
}
