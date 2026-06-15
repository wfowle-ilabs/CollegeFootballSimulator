using System.Text.Json.Serialization;

namespace CfbSim.Core.Model;

/// <summary>One scheduled game.</summary>
public sealed class Matchup
{
    public int Week { get; set; }
    public required int HomeId { get; init; }
    public required int AwayId { get; init; }
    public bool ConferenceGame { get; init; }
    public bool Rivalry { get; set; }
    public string? RivalryName { get; set; }

    public bool Involves(int teamId) => HomeId == teamId || AwayId == teamId;
    public int Opponent(int teamId) => HomeId == teamId ? AwayId : HomeId;
}

/// <summary>A full season's slate of games.</summary>
public sealed class Schedule
{
    public required int Year { get; init; }
    public List<Matchup> Games { get; } = new();

    public IEnumerable<Matchup> InWeek(int week) => Games.Where(g => g.Week == week);
    public IEnumerable<Matchup> For(int teamId) => Games.Where(g => g.Involves(teamId));

    [JsonIgnore]
    public int Weeks => Games.Count == 0 ? 0 : Games.Max(g => g.Week);
}
