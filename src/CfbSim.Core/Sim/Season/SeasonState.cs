using System.Text.Json.Serialization;
using CfbSim.Core.Model;

namespace CfbSim.Core.Sim.Season;

/// <summary>
/// The resumable, persistable state of a season in progress: the schedule, the
/// records and results accumulated so far, and the next week to play. This is what
/// the season-state sidecar serializes — it references teams by ID against the
/// (separately stored) league.
/// </summary>
public sealed class SeasonState
{
    public required int Year { get; init; }
    public required Schedule Schedule { get; init; }
    public Dictionary<int, TeamRecord> Records { get; init; } = new();
    public List<SeasonGameResult> Games { get; init; } = new();
    public StatBook Stats { get; init; } = new();
    public int NextWeek { get; set; } = 1;

    /// <summary>The day cursor — the calendar day the user is currently sitting on. Advancing a
    /// day moves it forward and sims any games kicking off that day; <c>default</c> until
    /// initialized (back-compat for pre-cursor saves).</summary>
    public DateOnly CurrentDate { get; set; }

    /// <summary>The user's training assignments, keyed by <see cref="TrainingKey"/> (date+slot).
    /// User-program scoped and season-lived; additive to the save.</summary>
    public Dictionary<string, TrainingActivity> Training { get; init; } = new();

    [JsonIgnore]
    public bool IsComplete => NextWeek > Schedule.Weeks;
}
