namespace CfbSim.Core.Media;

public enum ArticleType { GameRecap, UpsetAlert, RivalryResult, ShortRecap }

/// <summary>
/// A generated news article (an artifact, never an input to the sim). Persisted in the
/// media sidecar. Per docs/architecture.qmd this would be a Resource in a Godot-native
/// save; here it's a plain serialized type (engine-free core).
/// </summary>
public sealed class NewsArticle
{
	public int Id { get; set; }
	public required int Year { get; init; }
	public required int Week { get; init; }
	public required ArticleType Type { get; init; }
	public required bool Full { get; init; }
	public required string Headline { get; init; }
	public required string Body { get; init; }
	public List<int> TeamIds { get; init; } = new(); // teams featured (for lookup)
}
