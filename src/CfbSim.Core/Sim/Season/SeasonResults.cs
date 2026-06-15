using System.Text.Json.Serialization;

namespace CfbSim.Core.Sim.Season;

/// <summary>A team's season record.</summary>
public sealed class TeamRecord
{
    public required int TeamId { get; init; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int ConfWins { get; set; }
    public int ConfLosses { get; set; }
    public int PointsFor { get; set; }
    public int PointsAgainst { get; set; }

    [JsonIgnore] public int Games => Wins + Losses;
    [JsonIgnore] public double WinPct => Games == 0 ? 0 : (double)Wins / Games;
    [JsonIgnore] public int ConfGames => ConfWins + ConfLosses;
    [JsonIgnore] public double ConfWinPct => ConfGames == 0 ? 0 : (double)ConfWins / ConfGames;
    public override string ToString() => $"{Wins}-{Losses} ({ConfWins}-{ConfLosses})";
}

/// <summary>A lightweight final-score record for one game (no play log retained at season scale).</summary>
public sealed class SeasonGameResult
{
    public required int Week { get; init; }
    public required int HomeId { get; init; }
    public required int AwayId { get; init; }
    public required int HomeScore { get; init; }
    public required int AwayScore { get; init; }
    public bool ConferenceGame { get; init; }
    public bool Rivalry { get; init; }
    public string? RivalryName { get; init; }

    [JsonIgnore] public int WinnerId => HomeScore >= AwayScore ? HomeId : AwayId;
    [JsonIgnore] public int LoserId => HomeScore >= AwayScore ? AwayId : HomeId;
}

/// <summary>A team's spot in the poll.</summary>
public sealed record RankedTeam(int Rank, int TeamId, double Rating, TeamRecord Record);

/// <summary>The full output of a simulated regular season.</summary>
public sealed class SeasonResult
{
    public required int Year { get; init; }
    public List<SeasonGameResult> Games { get; } = new();
    public Dictionary<int, TeamRecord> Records { get; } = new();
    public List<RankedTeam> Rankings { get; } = new();

    public IEnumerable<RankedTeam> Top25 => Rankings.Take(25);
}
