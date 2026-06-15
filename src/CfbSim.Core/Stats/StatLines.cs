using System.Text.Json.Serialization;
using CfbSim.Core.Model;

namespace CfbSim.Core.Stats;

/// <summary>Per-player accumulated stats for a game.</summary>
public sealed class PlayerStatLine
{
    public required int PlayerId { get; init; }
    public required string Name { get; init; }
    public required Position Position { get; init; }
    public required int TeamId { get; init; }

    // Rushing
    public int RushAtt { get; set; }
    public int RushYds { get; set; }
    public int RushTD { get; set; }

    // Receiving
    public int Targets { get; set; }
    public int Rec { get; set; }
    public int RecYds { get; set; }
    public int RecTD { get; set; }

    // Passing
    public int PassAtt { get; set; }
    public int PassComp { get; set; }
    public int PassYds { get; set; }
    public int PassTD { get; set; }
    public int PassInt { get; set; }

    // Defense
    public int Sacks { get; set; }
    public int Interceptions { get; set; }

    [JsonIgnore] public bool HasOffense => RushAtt > 0 || Targets > 0 || PassAtt > 0;
}

/// <summary>Per-team totals for a game.</summary>
public sealed class TeamStatLine
{
    public int Points { get; set; }
    public int FirstDowns { get; set; }
    public int Plays { get; set; }
    public int RushYds { get; set; }
    public int PassYds { get; set; }
    public int Turnovers { get; set; }
    public int Penalties { get; set; }
    public int PenaltyYds { get; set; }
    public int PossessionSeconds { get; set; }

    public int TotalYards => RushYds + PassYds;
}
