using CfbSim.Core.Model;

namespace CfbSim.Core.Sim.Season;

/// <summary>
/// Per-conference scheduling rules. v1 uses round-robin (a rotation when the league
/// is too big for a full round-robin). Divisions are modeled but unused for now —
/// the hook is here so conference-specific formats (pods, division winners → champ
/// game) can be added without reworking the scheduler.
/// </summary>
public sealed class ConferenceScheduleRule
{
    public required int ConferenceId { get; init; }
    public required int ConferenceGames { get; init; }
    public bool UseDivisions { get; init; }
    public List<List<int>>? Divisions { get; init; } // team ids per division (future)
}

public static class ScheduleRules
{
    /// <summary>Default rule: up to 9 conference games per team (capped by conference size).</summary>
    public static ConferenceScheduleRule Default(Conference conference)
        => new()
        {
            ConferenceId = conference.Id,
            ConferenceGames = Math.Clamp(conference.Teams.Count - 1, 1, 9),
        };
}
