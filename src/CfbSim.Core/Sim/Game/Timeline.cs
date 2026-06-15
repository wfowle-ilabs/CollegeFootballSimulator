using CfbSim.Core.Sim.Play;

namespace CfbSim.Core.Sim.Game;

/// <summary>What a timeline segment represents.</summary>
public enum SegmentKind { Snap, SpecialTeams, Score }

/// <summary>
/// One entry in a drive's play-by-play. Carries the human-readable <see cref="Text"/> (the play
/// log and scoring summary are derived from these) plus structured fields the SimCast broadcast
/// reads. Broadcast pacing (per-segment display duration) is layered on with the clock-fidelity
/// pass — see docs/v1_1.qmd.
/// </summary>
public sealed class GameSegment
{
    public required SegmentKind Kind { get; init; }
    public required string ClockLabel { get; init; }
    public int ClockElapsed { get; init; }
    public required string Text { get; init; }

    // Play fields (default/zero when not applicable).
    public int Down { get; init; }
    public int Distance { get; init; }
    public int BallOn { get; init; }
    public PlayType PlayType { get; init; }
    public int Yards { get; init; }
    public bool Touchdown { get; init; }
    public bool FirstDown { get; init; }
    public bool Turnover { get; init; }

    // Score fields (Kind == Score).
    public string ScoreKind { get; init; } = "";
    public int HomeScore { get; init; }
    public int AwayScore { get; init; }
}

/// <summary>
/// One possession: who had it, where it started, how it ended, and its ordered play-by-play.
/// This is what the SimCast "Play-by-Play" tab renders as a collapsible card.
/// </summary>
public sealed class Drive
{
    public required int OffenseId { get; init; }
    public required string OffenseAbbr { get; init; }
    public required int StartBallOn { get; init; }
    public required string StartClockLabel { get; init; }

    /// <summary>A marker rendered before this drive's header (e.g. "== HALFTIME ==").</summary>
    public string? PrecedingMarker { get; set; }
    public DriveEndReason Result { get; set; }
    public int Points { get; set; }
    public List<GameSegment> Segments { get; } = new();

    public string Header => $"-- {OffenseAbbr} ball at {Field.Describe(StartBallOn)} ({StartClockLabel}) --";

    /// <summary>Scrimmage + special-teams snaps in the drive (excludes the scoring beat).</summary>
    public int PlayCount => Segments.Count(s => s.Kind is SegmentKind.Snap or SegmentKind.SpecialTeams);

    /// <summary>Net yards gained on the drive (sum of scrimmage yards).</summary>
    public int Yards => Segments.Where(s => s.Kind == SegmentKind.Snap).Sum(s => s.Yards);
}

/// <summary>
/// The structured, drive-grouped record of a game — the single source the play log, scoring
/// summary, and (v1.1) the SimCast broadcast all read. The string play log is a <em>derived
/// view</em> of this, not a parallel record. See docs/v1_1.qmd.
/// </summary>
public sealed class GameTimeline
{
    public List<Drive> Drives { get; } = new();

    /// <summary>The play-by-play log: drive markers + headers + each play's text (scoring beats are
    /// excluded — they live in <see cref="ScoringSummary"/>).</summary>
    public List<string> PlayLog()
    {
        var lines = new List<string>();
        foreach (Drive d in Drives)
        {
            if (d.PrecedingMarker is { } marker) lines.Add(marker);
            lines.Add(d.Header);
            foreach (GameSegment s in d.Segments)
                if (s.Kind != SegmentKind.Score && s.Text.Length > 0) lines.Add(s.Text);
        }
        return lines;
    }

    /// <summary>The running scoring summary, one line per scoring play, in order.</summary>
    public List<string> ScoringSummary()
        => Drives.SelectMany(d => d.Segments)
                 .Where(s => s.Kind == SegmentKind.Score)
                 .Select(s => s.Text)
                 .ToList();
}
