using CfbSim.Core.Sim.Game;
using CfbSim.Core.Stats;

namespace CfbSim.Core.Model;

/// <summary>The full record of a simulated game: score, box score, and the structured timeline
/// (the play log and scoring summary are <em>derived</em> from the timeline — see docs/v1_1.qmd).</summary>
public sealed class GameResult
{
    public required Team Home { get; init; }
    public required Team Away { get; init; }
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public int Overtimes { get; set; }

    public required BoxScore Box { get; init; }

    /// <summary>The drive-grouped play-by-play — the single source the views below read.</summary>
    public GameTimeline Timeline { get; } = new();

    public List<string> PlayLog => Timeline.PlayLog();
    public List<string> ScoringSummary => Timeline.ScoringSummary();

    public Team Winner => HomeScore >= AwayScore ? Home : Away;
    public bool IsTie => HomeScore == AwayScore;
}
