using CfbSim.Core.Generation;
using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Save;
using CfbSim.Core.Services;
using CfbSim.Core.Sim.Season;
using CfbSim.Core.Stats;
using Xunit;

namespace CfbSim.Tests;

public class SeasonStatsTests
{
    private static (League League, SeriesHistory History, SeasonState State) Sim(ulong seed, int weeks)
    {
        var rng = new Pcg32Rng(seed);
        League league = LeagueBuilder.Build(rng);
        SeriesHistory history = SeriesHistory.SeededFor(league);
        Schedule schedule = ScheduleBuilder.Build(rng, league, history, 2026);
        SeasonState state = SeasonDriver.Initialize(league, schedule);
        for (int i = 0; i < weeks; i++) SeasonDriver.AdvanceWeek(rng, league, state, history);
        return (league, history, state);
    }

    [Fact]
    public void StatsAccumulate_AndLeadersAreSortedAndPlausible()
    {
        var (_, _, state) = Sim(1, 4);
        Assert.NotEmpty(state.Stats.Players);

        var passers = state.Stats.Leaders(p => p.PassYds, 10).ToList();
        Assert.NotEmpty(passers);
        Assert.True(passers[0].PassYds > 0);
        for (int i = 1; i < passers.Count; i++)
            Assert.True(passers[i].PassYds <= passers[i - 1].PassYds);

        Assert.Contains(state.Stats.Leaders(p => p.RushYds, 10), p => p.RushYds > 0);
        Assert.Contains(state.Stats.Leaders(p => p.RecYds, 10), p => p.RecYds > 0);
    }

    [Fact]
    public void Stats_RoundTripInSave()
    {
        string dir = Path.Combine(Path.GetTempPath(), "cfbsim_tests", nameof(Stats_RoundTripInSave));
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);

        var (league, history, state) = Sim(2, 3);
        int playersBefore = state.Stats.Players.Count;
        int topPassYds = state.Stats.Leaders(p => p.PassYds, 1).First().PassYds;

        var save = new GameSave { Year = 2026, League = league, History = history, Season = state, Rng = new Pcg32Rng(2).Snapshot() };
        SaveManager.Save(dir, save);
        GameSave loaded = SaveManager.Load(dir);

        Assert.Equal(playersBefore, loaded.Season.Stats.Players.Count);
        Assert.Equal(topPassYds, loaded.Season.Stats.Leaders(p => p.PassYds, 1).First().PassYds);
    }
}
