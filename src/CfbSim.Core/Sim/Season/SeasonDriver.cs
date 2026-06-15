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
        var state = new SeasonState
        {
            Year = schedule.Year,
            Schedule = schedule,
            CurrentDate = SeasonCalendar.OpeningSunday(schedule.Year),
        };
        foreach (Team t in league.AllTeams)
            state.Records[t.Id] = new TeamRecord { TeamId = t.Id };
        return state;
    }

    /// <summary>Simulate the next scheduled week. No-op once the season is complete.
    /// <paramref name="onGameBox"/> receives each game's (week, homeId, awayId, box) for the UI.</summary>
    public static void AdvanceWeek(IRng rng, League league, SeasonState state, SeriesHistory history,
        IEventSink? sink = null, Action<int, int, int, BoxScore>? onGameBox = null, int? userTeamId = null)
    {
        if (state.IsComplete) return;

        var teams = league.AllTeams.ToDictionary(t => t.Id);
        int week = state.NextWeek;

        foreach (Matchup m in state.Schedule.InWeek(week))
            if (!Played(state, m))
                SimMatchup(rng, teams, state, history, m, onGameBox, userTeamId);

        state.NextWeek++;
        state.CurrentDate = SeasonCalendar.SaturdayOfWeek(state.Year, week);
        sink?.Publish(new WeekAdvanced(state.Year, week));
    }

    /// <summary>Advance the day cursor by one calendar day, simming any games kicking off that
    /// day. When a week's full slate completes, the week pointer rolls forward and a
    /// <see cref="WeekAdvanced"/> fires (so media/standings update on week boundaries, exactly
    /// as the week path does). No-op once the season is complete.</summary>
    public static void AdvanceDay(IRng rng, League league, SeasonState state, SeriesHistory history,
        IEventSink? sink = null, Action<int, int, int, BoxScore>? onGameBox = null, int? userTeamId = null)
    {
        if (state.IsComplete) return;
        if (state.CurrentDate == default) state.CurrentDate = SeasonCalendar.OpeningSunday(state.Year);

        DateOnly next = state.CurrentDate.AddDays(1);
        var teams = league.AllTeams.ToDictionary(t => t.Id);

        foreach (Matchup m in state.Schedule.Games)
            if (m.Kickoff != default && DateOnly.FromDateTime(m.Kickoff) == next && !Played(state, m))
                SimMatchup(rng, teams, state, history, m, onGameBox, userTeamId);

        state.CurrentDate = next;

        // Roll the week pointer forward past any now-complete weeks, firing their events.
        while (state.NextWeek <= state.Schedule.Weeks && WeekComplete(state, state.NextWeek))
        {
            int w = state.NextWeek;
            state.NextWeek++;
            sink?.Publish(new WeekAdvanced(state.Year, w));
        }
    }

    private static void SimMatchup(IRng rng, Dictionary<int, Team> teams, SeasonState state,
        SeriesHistory history, Matchup m, Action<int, int, int, BoxScore>? onGameBox, int? userTeamId)
    {
        Team home = teams[m.HomeId], away = teams[m.AwayId];

        // Apply each team's training-prep boost for this game, then clear it after.
        home.ActiveBoost = TrainingBoosts.ForGame(state, home.Id, m.Week, userTeamId);
        away.ActiveBoost = TrainingBoosts.ForGame(state, away.Id, m.Week, userTeamId);

        GameResult game = GameSimulator.Simulate(rng, home, away);

        home.ActiveBoost = TeamBoost.None;
        away.ActiveBoost = TeamBoost.None;

        state.Stats.Accumulate(game.Box);
        onGameBox?.Invoke(m.Week, m.HomeId, m.AwayId, game.Box);

        state.Games.Add(new SeasonGameResult
        {
            Week = m.Week,
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

    private static bool Played(SeasonState state, Matchup m)
        => state.Games.Any(g => g.Week == m.Week && g.HomeId == m.HomeId && g.AwayId == m.AwayId);

    private static bool WeekComplete(SeasonState state, int week)
        => state.Schedule.InWeek(week).All(m => Played(state, m));

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
