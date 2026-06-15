using System.Text.Json.Serialization;

namespace CfbSim.Core.Model;

/// <summary>
/// A football program. v1's full hierarchy (Conference/University/AthleticsDept)
/// sits above this; for M1 we only need the team and its roster.
/// </summary>
public sealed class Team
{
    public required int Id { get; init; }
    public required string Name { get; init; }          // e.g. "Georgia"
    public required string Abbreviation { get; init; }  // e.g. "UGA"
    public int Prestige { get; set; }                   // 1..100; biases generation
    public int ConferenceId { get; set; }               // 0 = independent (referenced by ID, not pointer)

    /// <summary>The team's prep boost for the game being simulated (set by the season driver,
    /// cleared after). Transient — never serialized.</summary>
    [JsonIgnore]
    public TeamBoost ActiveBoost { get; set; } = TeamBoost.None;

    public List<Player> Roster { get; init; } = new();

    public IEnumerable<Player> AtPosition(Position position)
        => Roster.Where(p => p.Position == position);

    /// <summary>The first player listed at a position (M1 stand-in for a depth chart).</summary>
    public Player? Starter(Position position) => AtPosition(position).FirstOrDefault();

    /// <summary>A random player at a position (spreads touches across the roster).</summary>
    public Player? Pick(Position position, Rng.IRng rng)
    {
        var list = AtPosition(position).ToList();
        return list.Count == 0 ? null : list[rng.NextInt(0, list.Count - 1)];
    }
}
