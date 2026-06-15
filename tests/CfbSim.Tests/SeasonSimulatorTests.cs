using CfbSim.Core.Generation;
using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Services;
using CfbSim.Core.Sim.Season;
using Xunit;

namespace CfbSim.Tests;

public class SeasonSimulatorTests
{
    private sealed record SeasonRun(League League, SeriesHistory History, Schedule Schedule, SeasonResult Result);

    private static SeasonRun Run(ulong seed)
    {
        var rng = new Pcg32Rng(seed);
        League league = LeagueBuilder.Build(rng);
        SeriesHistory history = SeriesHistory.SeededFor(league);
        Schedule schedule = ScheduleBuilder.Build(rng, league, history, year: 2026);
        SeasonResult result = SeasonSimulator.Run(rng, league, schedule, history);
        return new SeasonRun(league, history, schedule, result);
    }

    [Fact]
    public void EveryScheduledGame_IsPlayed_AndRecordsMatchSchedule()
    {
        SeasonRun s = Run(1);
        Assert.Equal(s.Schedule.Games.Count, s.Result.Games.Count);
        foreach (Team t in s.League.AllTeams)
        {
            int scheduled = s.Schedule.For(t.Id).Count();
            TeamRecord rec = s.Result.Records[t.Id];
            Assert.Equal(scheduled, rec.Games);
        }
    }

    [Fact]
    public void LeagueWide_WinsEqualLosses()
    {
        SeasonRun s = Run(1);
        int wins = s.Result.Records.Values.Sum(r => r.Wins);
        int losses = s.Result.Records.Values.Sum(r => r.Losses);
        Assert.Equal(wins, losses);
        Assert.Equal(s.Result.Games.Count, wins);
    }

    [Fact]
    public void ConferenceGames_BalanceWithinEachConference()
    {
        SeasonRun s = Run(1);
        foreach (Conference c in s.League.Conferences)
        {
            int cw = c.Teams.Sum(t => s.Result.Records[t.Id].ConfWins);
            int cl = c.Teams.Sum(t => s.Result.Records[t.Id].ConfLosses);
            Assert.Equal(cw, cl); // conference games are intra-conference → wins == losses
        }
    }

    [Fact]
    public void Top25_HasDistinctTeams_AndAStrongLeader()
    {
        SeasonRun s = Run(1);
        var top = s.Result.Top25.ToList();
        Assert.Equal(25, top.Count);
        Assert.Equal(25, top.Select(t => t.TeamId).Distinct().Count());
        Assert.Equal(1, top[0].Rank);
        Assert.True(top[0].Record.Wins >= top[0].Record.Losses, "the #1 team should not have a losing record");
        // Ratings are non-increasing down the poll.
        for (int i = 1; i < top.Count; i++)
            Assert.True(top[i].Rating <= top[i - 1].Rating);
    }

    [Fact]
    public void Rivalries_AreRecordedInHistory()
    {
        SeasonRun s = Run(1);
        foreach (SeriesRecord rivalry in s.History.Rivalries)
            Assert.True(rivalry.GamesPlayed >= 1, $"rivalry {rivalry.RivalryName} was never played");
    }

    [Fact]
    public void Season_IsDeterministic()
    {
        SeasonRun a = Run(9);
        SeasonRun b = Run(9);
        var aTop = a.Result.Top25.ToList();
        var bTop = b.Result.Top25.ToList();
        for (int i = 0; i < aTop.Count; i++)
            Assert.Equal(aTop[i].TeamId, bTop[i].TeamId);
        foreach (Team t in a.League.AllTeams)
            Assert.Equal(a.Result.Records[t.Id].Wins, b.Result.Records[t.Id].Wins);
    }
}
