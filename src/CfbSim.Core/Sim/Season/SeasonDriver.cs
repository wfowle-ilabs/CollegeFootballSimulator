using CfbSim.Core.Events;
using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Services;
using CfbSim.Core.Sim.Game;
using CfbSim.Core.Stats;

namespace CfbSim.Core.Sim.Season;

/// <summary>
/// Drives a season one week at a time, so it can be paused, saved, and resumed.
/// <see cref="SeasonSimulator"/> is a thin wrapper that runs to completion.
/// </summary>
public static class SeasonDriver
{
    public static SeasonState Initialize(League league, Schedule schedule)
    {
        var state = new SeasonState { Year = schedule.Year, Schedule = schedule };
        foreach (Team t in league.AllTeams)
            state.Records[t.Id] = new TeamRecord { TeamId = t.Id };
        return state;
    }

    /// <summary>Simulate the next scheduled week. No-op once the season is complete.
    /// <paramref name="onGameBox"/> receives each game's (week, homeId, awayId, box) for the UI.</summary>
    public static void AdvanceWeek(IRng rng, League league, SeasonState state, SeriesHistory history,
        IEventSink? sink = null, Action<int, int, int, BoxScore>? onGameBox = null)
    {
        if (state.IsComplete) return;

        var teams = league.AllTeams.ToDictionary(t => t.Id);
        int week = state.NextWeek;

        foreach (Matchup m in state.Schedule.InWeek(week))
        {
            GameResult game = GameSimulator.Simulate(rng, teams[m.HomeId], teams[m.AwayId]);
            state.Stats.Accumulate(game.Box);
            onGameBox?.Invoke(week, m.HomeId, m.AwayId, game.Box);

            state.Games.Add(new SeasonGameResult
            {
                Week = week,
                HomeId = m.HomeId,
                AwayId = m.AwayId,
                HomeScore = game.HomeScore,
                AwayScore = game.AwayScore,
                ConferenceGame = m.ConferenceGame,
                Rivalry = m.Rivalry,
                RivalryName = m.RivalryName,
            });

            ApplyResult(state.Records, m, game);
            int winner = game.HomeScore >= game.AwayScore ? m.HomeId : m.AwayId;
            int loser = winner == m.HomeId ? m.AwayId : m.HomeId;
            history.RecordResult(winner, loser, state.Year);
        }

        state.NextWeek++;
        sink?.Publish(new WeekAdvanced(state.Year, week));
    }

    /// <summary>Build the finished-season view (computes rankings) from a completed state.</summary>
    public static SeasonResult ToResult(League league, SeasonState state)
    {
        var result = new SeasonResult { Year = state.Year };
        foreach (var kv in state.Records) result.Records[kv.Key] = kv.Value;
        result.Games.AddRange(state.Games);
        result.Rankings.AddRange(RankingService.Rank(league, state.Records, state.Games));
        return result;
    }

    private static void ApplyResult(IReadOnlyDictionary<int, TeamRecord> records, Matchup m, GameResult game)
    {
        TeamRecord home = records[m.HomeId];
        TeamRecord away = records[m.AwayId];
        home.PointsFor += game.HomeScore; home.PointsAgainst += game.AwayScore;
        away.PointsFor += game.AwayScore; away.PointsAgainst += game.HomeScore;

        bool homeWon = game.HomeScore >= game.AwayScore;
        TeamRecord winner = homeWon ? home : away;
        TeamRecord loser = homeWon ? away : home;
        winner.Wins++; loser.Losses++;
        if (m.ConferenceGame) { winner.ConfWins++; loser.ConfLosses++; }
    }
}
