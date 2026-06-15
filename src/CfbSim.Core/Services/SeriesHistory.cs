using CfbSim.Core.Data;
using CfbSim.Core.Model;

namespace CfbSim.Core.Services;

/// <summary>
/// The league's head-to-head history. Records are stored sparsely (created on first
/// meeting), keyed by the canonical team-id pair. Named rivalries are seeded up front
/// so they carry their name before any game is played. This is the kind of growing,
/// independently-loadable data that lives in the historical-state sidecar (by ID).
/// </summary>
public sealed class SeriesHistory
{
    private readonly Dictionary<(int, int), SeriesRecord> _records = new();

    private static (int, int) Key(int a, int b) => a < b ? (a, b) : (b, a);

    /// <summary>Get the series record for two teams, creating an empty one if needed.</summary>
    public SeriesRecord Get(int teamA, int teamB)
    {
        (int, int) key = Key(teamA, teamB);
        if (!_records.TryGetValue(key, out SeriesRecord? record))
        {
            record = new SeriesRecord { TeamAId = key.Item1, TeamBId = key.Item2 };
            _records[key] = record;
        }
        return record;
    }

    public void RecordResult(int winnerId, int loserId, int year)
    {
        SeriesRecord r = Get(winnerId, loserId);
        if (winnerId == r.TeamAId) r.TeamAWins++; else r.TeamBWins++;
        r.LastMeetingYear = year;
        r.LastWinnerId = winnerId;
    }

    public void MarkRivalry(int teamA, int teamB, string name)
    {
        SeriesRecord r = Get(teamA, teamB);
        r.IsRivalry = true;
        r.RivalryName = name;
    }

    public bool IsRivalry(int teamA, int teamB)
        => _records.TryGetValue(Key(teamA, teamB), out SeriesRecord? r) && r.IsRivalry;

    public string? RivalryName(int teamA, int teamB)
        => _records.TryGetValue(Key(teamA, teamB), out SeriesRecord? r) ? r.RivalryName : null;

    public IEnumerable<SeriesRecord> Rivalries => _records.Values.Where(r => r.IsRivalry);
    public IEnumerable<SeriesRecord> All => _records.Values;

    /// <summary>All records as a flat list (for the historical-state sidecar).</summary>
    public List<SeriesRecord> Snapshot() => _records.Values.ToList();

    /// <summary>Rebuild a history from previously saved records.</summary>
    public static SeriesHistory FromRecords(IEnumerable<SeriesRecord> records)
    {
        var history = new SeriesHistory();
        foreach (SeriesRecord r in records)
            history._records[Key(r.TeamAId, r.TeamBId)] = r;
        return history;
    }

    /// <summary>Build a history with the named rivalries from <see cref="RivalryData"/> pre-seeded.</summary>
    public static SeriesHistory SeededFor(League league)
    {
        var history = new SeriesHistory();
        foreach (RivalryData.Rivalry rivalry in RivalryData.Rivalries)
        {
            Team? a = league.FindTeam(rivalry.A);
            Team? b = league.FindTeam(rivalry.B);
            if (a is not null && b is not null)
                history.MarkRivalry(a.Id, b.Id, rivalry.Name);
        }
        return history;
    }
}
