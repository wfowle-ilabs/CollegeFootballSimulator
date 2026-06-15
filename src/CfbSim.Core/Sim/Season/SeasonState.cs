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

    [JsonIgnore]
    public bool IsComplete => NextWeek > Schedule.Weeks;
}
