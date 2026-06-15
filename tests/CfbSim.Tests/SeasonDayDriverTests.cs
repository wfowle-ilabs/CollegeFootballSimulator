using CfbSim.Core.Events;
using CfbSim.Core.Generation;
using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Services;
using CfbSim.Core.Sim.Season;
using Xunit;

namespace CfbSim.Tests;

/// <summary>Covers the day-cursor path (<see cref="SeasonDriver.AdvanceDay"/>): advancing one
/// calendar day at a time still completes the season, plays each game exactly once, fires one
/// WeekAdvanced per week, and stays deterministic.</summary>
public class SeasonDayDriverTests
{
    private sealed class CollectingSink : IEventSink
    {
        public List<DomainEvent> Events { get; } = new();
        public void Publish(DomainEvent domainEvent) => Events.Add(domainEvent);
    }

    private static SeasonState RunByDay(ulong seed, IEventSink? sink = null)
    {
        var rng = new Pcg32Rng(seed);
        League league = LeagueBuilder.Build(rng);
        SeriesHistory history = SeriesHistory.SeededFor(league);
        Schedule schedule = ScheduleBuilder.Build(rng, league, history, year: 2026);
        SeasonState state = SeasonDriver.Initialize(league, schedule);

        int guard = 0; // a regular season spans ~14 weeks → well under 200 calendar days
        while (!state.IsComplete && guard++ < 200)
            SeasonDriver.AdvanceDay(rng, league, state, history, sink);

        return state;
    }

    [Fact]
    public void DayStepping_CompletesSeason_PlaysEveryGameOnce_AndFiresWeekEvents()
    {
        var sink = new CollectingSink();
        SeasonState state = RunByDay(3, sink);

        Assert.True(state.IsComplete);
        Assert.Equal(state.Schedule.Games.Count, state.Games.Count);
        Assert.Equal(state.Schedule.Weeks, sink.Events.OfType<WeekAdvanced>().Count());

        int wins = state.Records.Values.Sum(r => r.Wins);
        int losses = state.Records.Values.Sum(r => r.Losses);
        Assert.Equal(wins, losses);
        Assert.Equal(state.Schedule.Games.Count, wins);
    }

    [Fact]
    public void DayStepping_IsDeterministic()
    {
        SeasonState a = RunByDay(7);
        SeasonState b = RunByDay(7);
        foreach (var kv in a.Records)
            Assert.Equal(kv.Value.Wins, b.Records[kv.Key].Wins);
    }
}
