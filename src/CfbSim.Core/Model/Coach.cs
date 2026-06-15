namespace CfbSim.Core.Model;

/// <summary>
/// A coach as an attribute sheet (no "overall"). Stubbed for M1 — the PlayCaller
/// that consumes these arrives in M2.
/// </summary>
public sealed class Coach
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required CoachRole Role { get; init; }

    public int OffensiveAcumen { get; set; }
    public int DefensiveAcumen { get; set; }
    public int PlayCalling { get; set; }
    public int Development { get; set; }
    public int Discipline { get; set; }
    public int GameManagement { get; set; }
    public int Motivation { get; set; }
}
