using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Services;

namespace CfbSim.Core.Sim.Season;

/// <summary>
/// Builds a regular-season schedule: per-conference round-robin (via the circle
/// method), protected rivalries forced in, then non-conference games fill each team
/// toward a target, all packed into weeks by greedy edge-coloring (≤1 game/team/week,
/// byes allowed). A believable approximation — authentic per-conference formats and
/// home/away alternation are a later fidelity pass.
/// </summary>
public static class ScheduleBuilder
{
    public static Schedule Build(IRng rng, League league, SeriesHistory history,
        int year, int targetGames = 12, int maxWeeks = 14)
    {
        var ids = league.AllTeams.Select(t => t.Id).ToList();
        var confOf = league.AllTeams.ToDictionary(t => t.Id, t => t.ConferenceId);
        var pairs = new HashSet<(int, int)>();
        var conferencePairs = new HashSet<(int, int)>();
        var gameCount = ids.ToDictionary(i => i, _ => 0);

        static (int, int) Key(int a, int b) => a < b ? (a, b) : (b, a);

        void Add(int a, int b, bool conference)
        {
            if (a == b) return;
            (int, int) k = Key(a, b);
            if (!pairs.Add(k)) return;
            if (conference) conferencePairs.Add(k);
            gameCount[a]++;
            gameCount[b]++;
        }

        // 1. Conference games (round-robin / rotation per conference rule).
        foreach (Conference conference in league.Conferences)
        {
            ConferenceScheduleRule rule = ScheduleRules.Default(conference);
            var teamIds = conference.Teams.Select(t => t.Id).ToList();
            foreach ((int a, int b) in RoundRobin(teamIds, rule.ConferenceGames))
                Add(a, b, conference: true);
        }

        // 2. Protected rivalries — guaranteed on the slate (counts as a conf game if intra-conference).
        foreach (SeriesRecord rivalry in history.Rivalries)
        {
            if (!gameCount.ContainsKey(rivalry.TeamAId) || !gameCount.ContainsKey(rivalry.TeamBId)) continue;
            bool sameConf = confOf[rivalry.TeamAId] == confOf[rivalry.TeamBId] && confOf[rivalry.TeamAId] != 0;
            Add(rivalry.TeamAId, rivalry.TeamBId, sameConf);
        }

        // 3. Non-conference fill toward the target, preferring cross-conference pairings.
        var shuffled = ids.OrderBy(_ => rng.NextInt(0, 1_000_000)).ToList();
        bool added = true;
        while (added)
        {
            added = false;
            var needy = shuffled.Where(i => gameCount[i] < targetGames).ToList();
            for (int i = 0; i < needy.Count && !added; i++)
                for (int j = i + 1; j < needy.Count; j++)
                {
                    int a = needy[i], b = needy[j];
                    if (confOf[a] == confOf[b] && confOf[a] != 0) continue;
                    if (pairs.Contains(Key(a, b))) continue;
                    Add(a, b, conference: false);
                    added = true;
                    break;
                }
        }

        // 4. Assign each game to the earliest week where both teams are free.
        var schedule = new Schedule { Year = year };
        var busy = new HashSet<(int teamId, int week)>();
        int unplaced = 0;

        foreach ((int a, int b) in pairs.OrderBy(_ => rng.NextInt(0, 1_000_000)))
        {
            int week = -1;
            for (int w = 1; w <= maxWeeks; w++)
                if (!busy.Contains((a, w)) && !busy.Contains((b, w))) { week = w; break; }

            if (week == -1) { unplaced++; continue; }

            busy.Add((a, week));
            busy.Add((b, week));

            bool aHome = rng.NextInt(0, 1) == 0;
            var matchup = new Matchup
            {
                Week = week,
                HomeId = aHome ? a : b,
                AwayId = aHome ? b : a,
                ConferenceGame = conferencePairs.Contains(Key(a, b)),
            };
            if (history.IsRivalry(a, b))
            {
                matchup.Rivalry = true;
                matchup.RivalryName = history.RivalryName(a, b);
            }
            schedule.Games.Add(matchup);
        }

        // Assign kickoff days/windows and broadcast networks (deterministic, presentation-only).
        BroadcastScheduler.Assign(rng, league, schedule);

        return schedule;
    }

    /// <summary>Circle-method round-robin: yields pairings for the first `rounds` rounds,
    /// giving each team up to `rounds` distinct opponents.</summary>
    private static IEnumerable<(int, int)> RoundRobin(List<int> teamIds, int rounds)
    {
        var arr = new List<int>(teamIds);
        if (arr.Count % 2 != 0) arr.Add(-1); // bye marker
        int n = arr.Count;
        if (n < 2) yield break;

        int fixedTeam = arr[0];
        var rot = arr.GetRange(1, n - 1);
        int actualRounds = Math.Min(rounds, n - 1);

        for (int r = 0; r < actualRounds; r++)
        {
            var lineup = new List<int> { fixedTeam };
            lineup.AddRange(rot);
            for (int i = 0; i < n / 2; i++)
            {
                int a = lineup[i], b = lineup[n - 1 - i];
                if (a != -1 && b != -1) yield return (a, b);
            }
            int last = rot[^1];
            rot.RemoveAt(rot.Count - 1);
            rot.Insert(0, last);
        }
    }
}
