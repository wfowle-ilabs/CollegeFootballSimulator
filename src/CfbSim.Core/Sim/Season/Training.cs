namespace CfbSim.Core.Sim.Season;

/// <summary>
/// A training activity assignable to a daily slot (2K-style). Effects are deliberately
/// <b>temporary next-game preparation</b> — a boost balanced by a tradeoff — never permanent
/// attribute development (v1 keeps in-season progression out, see docs/architecture.qmd). The
/// catalog below pins the design; the numeric sim wiring (temporary advantage / fatigue /
/// injury rolls for the user's next game) is a follow-up pass.
/// </summary>
public enum TrainingActivity
{
    Rest,
    PositionDrills,
    Conditioning,
    FilmStudy,
    Scrimmage,
    SpecialTeams,
}

/// <summary>One catalog entry: what an activity does and what it costs.</summary>
public sealed record TrainingOption(TrainingActivity Activity, string Name, string Boost, string Tradeoff);

/// <summary>The fixed set of training options and their boosts/tradeoffs.</summary>
public static class TrainingCatalog
{
    public static readonly IReadOnlyList<TrainingOption> All = new[]
    {
        new TrainingOption(TrainingActivity.Rest, "Rest & Recovery",
            "Lowers fatigue and injury risk going into the next game", "No prep boost this session"),
        new TrainingOption(TrainingActivity.PositionDrills, "Position Drills",
            "Advantage for a position group in the next game", "Adds fatigue; small injury risk"),
        new TrainingOption(TrainingActivity.Conditioning, "Conditioning",
            "Less late-game fatigue — a stronger 4th quarter", "No skill boost; mild fatigue now"),
        new TrainingOption(TrainingActivity.FilmStudy, "Film Study",
            "Game-plan edge — advantage on reads and discipline", "No physical gain"),
        new TrainingOption(TrainingActivity.Scrimmage, "Full Scrimmage",
            "Broad team sharpness boost next game", "Highest injury risk; heavy fatigue"),
        new TrainingOption(TrainingActivity.SpecialTeams, "Special Teams",
            "Kicking and return-game edge", "Opportunity cost vs. offense/defense reps"),
    };

    public static TrainingOption For(TrainingActivity activity) => All.First(o => o.Activity == activity);
}

/// <summary>Builds the stable dictionary key for a day's training slot.</summary>
public static class TrainingKey
{
    public static string Of(DateOnly date, TimeSlot slot) => $"{date:yyyy-MM-dd}:{slot}";
}
