using CfbSim.Core.Model;
using CfbSim.Core.Rng;

namespace CfbSim.Core.Sim.Season;

/// <summary>
/// Turns a week of training-slot assignments into a <see cref="TeamBoost"/> applied to that
/// team's game. The user's boost comes from their saved plan; every other team gets a
/// <b>deterministic</b> AI plan derived from (teamId, week) — reproducible and computed off a
/// throwaway RNG so it never perturbs the simulation's seeded stream.
/// </summary>
public static class TrainingBoosts
{
    private static readonly TimeSlot[] Slots = { TimeSlot.Morning, TimeSlot.Afternoon, TimeSlot.Evening };

    /// <summary>Per-slot contribution: (offense, defense, specialTeams, fatigue).</summary>
    private static (int Off, int Def, int St, int Fat) Contribution(TrainingActivity a) => a switch
    {
        TrainingActivity.Rest => (0, 0, 0, -3),
        TrainingActivity.PositionDrills => (2, 2, 0, 2),
        TrainingActivity.Conditioning => (0, 0, 0, -2),
        TrainingActivity.FilmStudy => (2, 2, 0, 0),
        TrainingActivity.Scrimmage => (3, 3, 0, 4),
        TrainingActivity.SpecialTeams => (0, 0, 4, 1),
        _ => (0, 0, 0, 0),
    };

    /// <summary>The boost a team carries into its game in <paramref name="week"/>.</summary>
    public static TeamBoost ForGame(SeasonState state, int teamId, int week, int? userTeamId)
        => teamId == userTeamId ? FromUserPlan(state, teamId, week) : Ai(teamId, week);

    private static TeamBoost FromUserPlan(SeasonState state, int teamId, int week)
    {
        DateOnly sunday = SeasonCalendar.SundayOfWeek(state.Year, week);
        var boost = new TeamBoost();
        for (int d = 0; d < 7; d++)
        {
            DateOnly date = sunday.AddDays(d);
            foreach (TimeSlot slot in Slots)
                if (state.Training.TryGetValue(TrainingKey.Of(date, slot), out TrainingActivity a))
                    Apply(boost, a);
        }
        Finalize(boost);
        return boost;
    }

    /// <summary>A sensible, deterministic AI training week: a focus + film study + recovery.</summary>
    private static TeamBoost Ai(int teamId, int week)
    {
        ulong seed = (ulong)(uint)teamId * 0x9E3779B97F4A7C15UL + (ulong)(uint)week * 0xBF58476D1CE4E5B9UL + 1;
        var rng = new Pcg32Rng(seed);
        TrainingActivity[] focus =
        {
            TrainingActivity.PositionDrills, TrainingActivity.FilmStudy,
            TrainingActivity.Conditioning, TrainingActivity.SpecialTeams, TrainingActivity.Scrimmage,
        };

        var boost = new TeamBoost();
        Apply(boost, focus[rng.NextInt(0, focus.Length - 1)]);
        Apply(boost, TrainingActivity.FilmStudy);
        Apply(boost, rng.NextInt(0, 1) == 0 ? TrainingActivity.Conditioning : TrainingActivity.Rest);
        Finalize(boost);
        return boost;
    }

    private static void Apply(TeamBoost boost, TrainingActivity a)
    {
        (int off, int def, int st, int fat) = Contribution(a);
        boost.Offense += off;
        boost.Defense += def;
        boost.SpecialTeams += st;
        boost.Fatigue += fat;
        boost.Sources.Add(TrainingCatalog.For(a).Name);
    }

    private static void Finalize(TeamBoost boost)
    {
        boost.Fatigue = Math.Max(0, boost.Fatigue);
        boost.Offense = Math.Min(boost.Offense, 6);
        boost.Defense = Math.Min(boost.Defense, 6);
        boost.SpecialTeams = Math.Min(boost.SpecialTeams, 6);
    }
}
