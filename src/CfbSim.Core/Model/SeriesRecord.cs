using System.Text.Json.Serialization;

namespace CfbSim.Core.Model;

/// <summary>
/// The all-time head-to-head record between two teams. Every pair of teams *can*
/// have one, but records are materialized lazily (only when teams actually meet) —
/// most pairs never play. Some series are flagged as named, protected rivalries;
/// a non-rivalry series can be promoted later (e.g., when it becomes meaningful).
/// Canonical orientation: TeamAId &lt; TeamBId.
/// </summary>
public sealed class SeriesRecord
{
    public required int TeamAId { get; init; }
    public required int TeamBId { get; init; }

    public int TeamAWins { get; set; }
    public int TeamBWins { get; set; }
    public int Ties { get; set; }

    public int LastMeetingYear { get; set; }
    public int LastWinnerId { get; set; }

    public bool IsRivalry { get; set; }
    public string? RivalryName { get; set; }

    [JsonIgnore]
    public int GamesPlayed => TeamAWins + TeamBWins + Ties;
}
