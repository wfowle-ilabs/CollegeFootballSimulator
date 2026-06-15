namespace CfbSim.Core.Model;

/// <summary>
/// A conference: a membership group of teams. Sits below the (implicit) FBS Division
/// in the world hierarchy. Divisions/pods and TV deals are deferred past M3.
/// </summary>
public sealed class Conference
{
    public required int Id { get; init; }
    public required string Name { get; init; }          // e.g. "Southeastern Conference"
    public required string Abbreviation { get; init; }  // e.g. "SEC"
    public bool IsPower { get; init; }                  // Power-4 vs Group-of-5 (affects scheduling/ranking)

    public List<Team> Teams { get; } = new();
}
