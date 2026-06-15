using CfbSim.Core.Events;
using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Services;

namespace CfbSim.Core.Sim.Season;

/// <summary>
/// Runs a full regular season to completion. A thin wrapper over <see cref="SeasonDriver"/>
/// (which can also be driven week-by-week for save/resume). Emits WeekAdvanced per week
/// and SeasonConcluded at the end.
/// </summary>
public static class SeasonSimulator
{
    public static SeasonResult Run(IRng rng, League league, Schedule schedule, SeriesHistory history, IEventSink? sink = null)
    {
        SeasonState state = SeasonDriver.Initialize(league, schedule);
        while (!state.IsComplete)
            SeasonDriver.AdvanceWeek(rng, league, state, history, sink);

        SeasonResult result = SeasonDriver.ToResult(league, state);
        sink?.Publish(new SeasonConcluded(schedule.Year));
        return result;
    }
}
