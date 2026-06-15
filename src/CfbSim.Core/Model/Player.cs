using System.Text.Json.Serialization;

namespace CfbSim.Core.Model;

/// <summary>The six broad, slow-changing core attributes (1–20). BG3's stat block.</summary>
public sealed class CoreAttributes
{
    public int Strength { get; set; }
    public int Agility { get; set; }
    public int Speed { get; set; }
    public int Awareness { get; set; }
    public int Durability { get; set; }
    public int Composure { get; set; }
}

/// <summary>
/// A player: identity + the two independent rating axes (core attributes and
/// position skills). Plain C# for M1; the Resource/serialization layer arrives at M5.
/// </summary>
public sealed class Player
{
    public required int Id { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required Position Position { get; init; }
    public required ClassYear Class { get; set; }   // mutable: players age each offseason
    public int JerseyNumber { get; set; }

    public CoreAttributes Attributes { get; init; } = new();
    public Dictionary<Skill, int> Skills { get; init; } = new();

    [JsonIgnore]
    public string Name => $"{FirstName} {LastName}";

    /// <summary>Skill value, defaulting to 1 (the floor) when a player lacks it.</summary>
    public int Of(Skill skill) => Skills.TryGetValue(skill, out int v) ? v : 1;
}
