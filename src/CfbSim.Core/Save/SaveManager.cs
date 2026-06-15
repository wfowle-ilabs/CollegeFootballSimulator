using System.Text.Json;
using System.Text.Json.Serialization;
using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Services;
using CfbSim.Core.Sim.Season;

namespace CfbSim.Core.Save;

/// <summary>
/// Persists a <see cref="GameSave"/> as a thin spine plus per-module sidecars
/// (current/season/history), each written atomically (temp file + rename). Engine-free
/// JSON — the Godot side passes a globalized <c>user://</c> directory path. See
/// docs/architecture.qmd (Storage &amp; Save Architecture).
/// </summary>
public static class SaveManager
{
    public const string SpineFile = "spine.json";
    public const string LeagueFile = "league.json";    // current-state sidecar
    public const string SeasonFile = "season.json";    // season-state sidecar
    public const string HistoryFile = "history.json";  // head-to-head series sidecar
    public const string ArchiveFile = "archive.json";  // season-by-season archive sidecar
    public const string MediaFile = "media.json";      // generated articles sidecar

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
        // Populate existing get-only collections (League.Conferences, Conference.Teams,
        // Team.Roster, Schedule.Games, …) instead of skipping them on deserialize.
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
    };

    /// <summary>Thrown when a save's schema version doesn't match this build.</summary>
    public sealed class SaveVersionException(int found, int expected)
        : Exception($"Save schema version {found} is not supported (expected {expected}).");

    // The spine: small, references the sidecars; the load sequence starts here.
    private sealed record SaveSpine(int SchemaVersion, int Year, int? UserTeamId, int NextPlayerId, Pcg32State Rng, string[] Sidecars);

    public static void Save(string directory, GameSave save)
    {
        Directory.CreateDirectory(directory);

        WriteAtomic(Path.Combine(directory, LeagueFile), save.League);
        WriteAtomic(Path.Combine(directory, SeasonFile), save.Season);
        WriteAtomic(Path.Combine(directory, HistoryFile), save.History.Snapshot());
        WriteAtomic(Path.Combine(directory, ArchiveFile), save.Archive);
        WriteAtomic(Path.Combine(directory, MediaFile), save.Media);

        var spine = new SaveSpine(
            save.SchemaVersion, save.Year, save.UserTeamId, save.NextPlayerId, save.Rng,
            new[] { LeagueFile, SeasonFile, HistoryFile, ArchiveFile, MediaFile });
        WriteAtomic(Path.Combine(directory, SpineFile), spine);
    }

    public static GameSave Load(string directory)
    {
        SaveSpine spine = Read<SaveSpine>(Path.Combine(directory, SpineFile));
        if (spine.SchemaVersion != GameSave.CurrentSchemaVersion)
            throw new SaveVersionException(spine.SchemaVersion, GameSave.CurrentSchemaVersion);

        League league = Read<League>(Path.Combine(directory, LeagueFile));
        SeasonState season = Read<SeasonState>(Path.Combine(directory, SeasonFile));
        List<SeriesRecord> records = Read<List<SeriesRecord>>(Path.Combine(directory, HistoryFile));
        LeagueHistory archive = Read<LeagueHistory>(Path.Combine(directory, ArchiveFile));
        Media.MediaStore media = Read<Media.MediaStore>(Path.Combine(directory, MediaFile));

        return new GameSave
        {
            SchemaVersion = spine.SchemaVersion,
            Year = spine.Year,
            UserTeamId = spine.UserTeamId,
            NextPlayerId = spine.NextPlayerId,
            League = league,
            Season = season,
            History = SeriesHistory.FromRecords(records),
            Archive = archive,
            Media = media,
            Rng = spine.Rng,
        };
    }

    /// <summary>True if a save exists in the directory (has a spine).</summary>
    public static bool Exists(string directory) => File.Exists(Path.Combine(directory, SpineFile));

    private static void WriteAtomic<T>(string path, T value)
    {
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Options));
        File.Move(tmp, path, overwrite: true); // atomic replace
    }

    private static T Read<T>(string path)
        => JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options)
           ?? throw new InvalidDataException($"Failed to read {Path.GetFileName(path)}.");
}
