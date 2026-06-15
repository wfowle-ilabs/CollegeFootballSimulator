namespace CfbSim.Core.Media;

/// <summary>
/// The facts a writer needs for one game's story — assembled from the game result,
/// team identities, and the current poll. The tiering pass fills <see cref="Type"/>
/// and <see cref="Full"/>; the generator turns this into a <see cref="NewsArticle"/>.
/// </summary>
public sealed class NarrativeContext
{
    public required int Year { get; init; }
    public required int Week { get; init; }

    public required int WinnerId { get; init; }
    public required int LoserId { get; init; }
    public required string WinnerName { get; init; }
    public required string LoserName { get; init; }
    public required string WinnerAbbr { get; init; }
    public required string LoserAbbr { get; init; }
    public required int WinnerScore { get; init; }
    public required int LoserScore { get; init; }

    public int WinnerRank { get; init; } // 0 = unranked
    public int LoserRank { get; init; }
    public bool IsRivalry { get; init; }
    public string? RivalryName { get; init; }
    public bool IsUpset { get; init; }
    public bool IsUserGame { get; init; }

    // Filled by the tiering pass.
    public ArticleType Type { get; set; }
    public bool Full { get; set; }

    public int Margin => WinnerScore - LoserScore;
}
