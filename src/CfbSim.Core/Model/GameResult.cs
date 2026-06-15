using CfbSim.Core.Stats;

namespace CfbSim.Core.Model;

/// <summary>The full record of a simulated game: score, box score, and play-by-play log.</summary>
public sealed class GameResult
{
    public required Team Home { get; init; }
    public required Team Away { get; init; }
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public int Overtimes { get; set; }

    public required BoxScore Box { get; init; }
    public List<string> PlayLog { get; } = new();
    public List<string> ScoringSummary { get; } = new();

    public Team Winner => HomeScore >= AwayScore ? Home : Away;
    public bool IsTie => HomeScore == AwayScore;
}
