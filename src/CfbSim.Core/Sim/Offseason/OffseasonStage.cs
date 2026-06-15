using CfbSim.Core.Generation;
using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Sim.Postseason;

namespace CfbSim.Core.Sim.Offseason;

/// <summary>
/// The v1 offseason (recruiting placeholder): archive the completed season into history,
/// advance class years, graduate seniors, and refill each roster to its target with
/// generated freshman walk-ons. No attribute development yet — deep player progression
/// is v2; this keeps the league turning over and stable between seasons.
/// </summary>
public static class OffseasonStage
{
    public static void Run(
        IRng rng, League league, LeagueHistory archive, int year,
        PostseasonResult postseason, ref int nextPlayerId, PlayerGenerator generator,
        RosterSpec? spec = null)
    {
        spec ??= RosterSpec.Default;

        // 1. Archive the completed season.
        archive.Seasons.Add(BuildSummary(year, postseason));

        // 2. Age every roster and replenish with incoming freshmen.
        foreach (Team team in league.AllTeams)
            AgeAndReplenish(rng, team, generator, spec, ref nextPlayerId);
    }

    private static SeasonSummary BuildSummary(int year, PostseasonResult post)
    {
        var summary = new SeasonSummary { Year = year, NationalChampionId = post.NationalChampionId };
        foreach (var kv in post.ConferenceChampions)
            summary.ConferenceChampions[kv.Key] = kv.Value;
        foreach (var rt in post.FinalRankings)
            summary.Finishes.Add(new SeasonFinish(rt.Rank, rt.TeamId, rt.Record.Wins, rt.Record.Losses));
        return summary;
    }

    private static void AgeAndReplenish(IRng rng, Team team, PlayerGenerator generator, RosterSpec spec, ref int nextPlayerId)
    {
        // Graduate seniors; everyone else advances one class.
        var returning = team.Roster.Where(p => p.Class != ClassYear.Senior).ToList();
        foreach (Player p in returning)
            p.Class = Advance(p.Class);

        team.Roster.Clear();
        team.Roster.AddRange(returning);

        // Refill each position back to its target with freshmen.
        var usedNumbers = new HashSet<int>(team.Roster.Select(p => p.JerseyNumber));
        foreach ((Position position, int count) in spec.Counts)
        {
            int have = team.Roster.Count(p => p.Position == position);
            for (int i = have; i < count; i++)
                team.Roster.Add(generator.GenerateFreshman(rng, nextPlayerId++, position, team.Prestige, usedNumbers));
        }
    }

    private static ClassYear Advance(ClassYear c) => c switch
    {
        ClassYear.Freshman => ClassYear.Sophomore,
        ClassYear.Sophomore => ClassYear.Junior,
        ClassYear.Junior => ClassYear.Senior,
        _ => ClassYear.Senior,
    };
}
