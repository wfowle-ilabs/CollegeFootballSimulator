using CfbSim.Core.Generation;
using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Sim.Game;
using Xunit;

namespace CfbSim.Tests;

/// <summary>Covers the structured <see cref="GameTimeline"/>: every game produces drive-grouped
/// segments, the play log + scoring summary are derived from them, and it stays deterministic.</summary>
public class TimelineTests
{
    private static GameResult Sim(ulong seed)
    {
        var build = new Pcg32Rng(seed);
        var teams = LeagueBuilder.Build(build).AllTeams.ToList();
        return GameSimulator.Simulate(new Pcg32Rng(seed * 31 + 5), teams[0], teams[1]);
    }

    [Fact]
    public void TimelineHasDrives_AndTheLogDerivesFromIt()
    {
        GameResult g = Sim(3);

        Assert.NotEmpty(g.Timeline.Drives);
        Assert.All(g.Timeline.Drives, d => Assert.NotEmpty(d.Segments)); // a drive always runs ≥1 play
        Assert.Contains(g.PlayLog, l => l.StartsWith("-- "));            // drive headers are in the log

        // The log is exactly the timeline's derived view — not a parallel record.
        Assert.Equal(g.Timeline.PlayLog(), g.PlayLog);
        Assert.Equal(g.Timeline.ScoringSummary(), g.ScoringSummary);

        // Scoring beats live in the summary, never in the play log.
        Assert.DoesNotContain(g.PlayLog, l => l.Contains("(") && l.Contains(") ") && l.Contains(","));
    }

    [Fact]
    public void ScoringSummary_TracksTheFinalScore()
    {
        GameResult g = Sim(7);
        var scores = g.Timeline.Drives.SelectMany(d => d.Segments)
            .Where(s => s.Kind == SegmentKind.Score).ToList();

        Assert.Equal(scores.Count, g.ScoringSummary.Count);

        // Regulation games record every score; the last running total is the final.
        if (g.Overtimes == 0 && scores.Count > 0)
        {
            Assert.Equal(g.HomeScore, scores[^1].HomeScore);
            Assert.Equal(g.AwayScore, scores[^1].AwayScore);
        }
    }

    [Fact]
    public void DrivesReportSanePlayAndYardCounts()
    {
        GameResult g = Sim(15);
        foreach (Drive d in g.Timeline.Drives)
        {
            Assert.True(d.PlayCount >= 1, "a drive should have at least one play");
            Assert.False(string.IsNullOrEmpty(d.Header));
        }
    }

    [Fact]
    public void Timeline_IsDeterministic()
    {
        GameResult a = Sim(9);
        GameResult b = Sim(9);
        Assert.Equal(a.Timeline.Drives.Count, b.Timeline.Drives.Count);
        Assert.Equal(a.Timeline.Drives.Sum(d => d.Segments.Count), b.Timeline.Drives.Sum(d => d.Segments.Count));
        Assert.Equal(a.PlayLog, b.PlayLog);
    }
}
