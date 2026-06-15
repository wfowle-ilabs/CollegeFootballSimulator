using CfbSim.Core.Events;
using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Services;
using CfbSim.Core.Sim.Season;

namespace CfbSim.Core.Sim.Postseason;

/// <summary>
/// Runs the full postseason to completion (headless): conference championships → re-rank →
/// seed the CFP → play the bracket → crown a champion (+ bowls). A thin wrapper over
/// <see cref="PostseasonDriver"/>, which can also be advanced a round at a time in the UI.
/// </summary>
public static class PostseasonSimulator
{
    public static PostseasonResult Run(IRng rng, League league, SeriesHistory history, SeasonResult season, IEventSink? sink = null)
    {
        PostseasonState state = PostseasonDriver.Begin(rng, league, history, season);
        while (!state.Bracket.IsComplete)
            PostseasonDriver.AdvanceRound(rng, state, league, history);

        PostseasonResult result = state.Result!;
        Team champion = league.AllTeams.First(t => t.Id == result.NationalChampionId);
        sink?.Publish(new NationalChampionCrowned(season.Year, champion.Id, champion.Name));
        return result;
    }
}
