using CfbSim.Core.Media;
using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Services;
using CfbSim.Core.Sim.Season;

namespace CfbSim.Core.Save;

/// <summary>
/// The complete in-memory state of a save: the league (current-state), the in-progress
/// season (season-state), head-to-head history (historical-state), the RNG snapshot
/// (for deterministic resume), and identity/metadata (the spine). <see cref="SaveManager"/>
/// writes each of these to its own sidecar file and stitches them back together on load.
/// </summary>
public sealed class GameSave
{
    public const int CurrentSchemaVersion = 3;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public required int Year { get; set; }
    public int? UserTeamId { get; set; }
    public int NextPlayerId { get; set; } // allocator for offseason-generated players

    public required League League { get; set; }
    public required SeriesHistory History { get; set; }
    public required SeasonState Season { get; set; }
    public LeagueHistory Archive { get; set; } = new();
    public MediaStore Media { get; set; } = new();
    public Pcg32State Rng { get; set; }
}
