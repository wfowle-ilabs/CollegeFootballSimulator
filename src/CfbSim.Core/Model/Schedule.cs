using System.Text.Json.Serialization;
using CfbSim.Core.Sim.Season;

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

    /// <summary>Calendar kickoff (date + window). <c>default</c> until the broadcast pass assigns it.</summary>
    public DateTime Kickoff { get; set; }
    /// <summary>Broadcast network (e.g. "ABC"). Empty until assigned.</summary>
    public string Network { get; set; } = "";

    /// <summary>Which of the three daily windows this game falls in (derived from <see cref="Kickoff"/>).</summary>
    [JsonIgnore]
    public TimeSlot Slot => Kickoff.Hour < 13 ? TimeSlot.Morning
                          : Kickoff.Hour < 18 ? TimeSlot.Afternoon
                          : TimeSlot.Evening;

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
