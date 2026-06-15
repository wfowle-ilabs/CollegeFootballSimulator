using CfbSim.Core.Generation;
using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Save;
using CfbSim.Core.Services;
using CfbSim.Core.Sim.Postseason;
using CfbSim.Core.Sim.Season;

namespace CfbSim.Core.Sim.Offseason;

/// <summary>
/// Drives the year-over-year loop: schedule → season → postseason → offseason. The
/// <see cref="GameSave"/> is the evolving multi-season state (league, history, archive,
/// year, id allocator), so a run can be saved/resumed between any years.
/// </summary>
public static class SeasonCycle
{
    /// <summary>Start a fresh multi-season game: build the league, seed rivalries, prime the id allocator.</summary>
    public static GameSave NewGame(IRng rng, int startYear, PlayerGenerator? generator = null)
    {
        generator ??= new PlayerGenerator();
        League league = LeagueBuilder.Build(rng, generator);
        int nextPlayerId = league.AllTeams.SelectMany(t => t.Roster).Max(p => p.Id) + 1;

        return new GameSave
        {
            Year = startYear,
            League = league,
            History = SeriesHistory.SeededFor(league),
            Season = SeasonDriver.Initialize(league, new Schedule { Year = startYear }),
            Archive = new LeagueHistory(),
            NextPlayerId = nextPlayerId,
            // GameSave.Rng is set by the caller (rng.Snapshot()) at save time.
        };
    }

    /// <summary>
    /// Run one complete year on the save: build &amp; play the season, run the postseason,
    /// then the offseason (archive + age + replenish). Advances the save to the next year.
    /// Returns the postseason result (champion, final poll, etc.).
    /// </summary>
    public static PostseasonResult RunFullYear(IRng rng, GameSave save, PlayerGenerator? generator = null)
    {
        generator ??= new PlayerGenerator();

        Schedule schedule = ScheduleBuilder.Build(rng, save.League, save.History, save.Year);
        save.Season = SeasonDriver.Initialize(save.League, schedule);
        while (!save.Season.IsComplete)
            SeasonDriver.AdvanceWeek(rng, save.League, save.Season, save.History);

        SeasonResult result = SeasonDriver.ToResult(save.League, save.Season);
        PostseasonResult postseason = PostseasonSimulator.Run(rng, save.League, save.History, result);

        int nextPlayerId = save.NextPlayerId;
        OffseasonStage.Run(rng, save.League, save.Archive, save.Year, postseason, ref nextPlayerId, generator);
        save.NextPlayerId = nextPlayerId;
        save.Year++;

        return postseason;
    }
}
