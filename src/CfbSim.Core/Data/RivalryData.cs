namespace CfbSim.Core.Data;

/// <summary>
/// Named, protected rivalries (scheduled annually where possible). Teams are by
/// abbreviation (resolved against the built league). Plain data — add/rename freely.
/// Some are intra-conference, some cross-conference. Emergent (unnamed) rivalries
/// are handled at runtime by promoting a <see cref="Model.SeriesRecord"/>.
/// </summary>
public static class RivalryData
{
	public sealed record Rivalry(string A, string B, string Name);

	public static IReadOnlyList<Rivalry> Rivalries { get; } = new[]
	{
		new Rivalry("ALA", "AUB", "Iron Bowl"),
		new Rivalry("OSU", "MICH", "The Game"),
		new Rivalry("TEX", "OU", "Red River Rivalry"),
		new Rivalry("MISS", "MSST", "Egg Bowl"),
		new Rivalry("UGA", "FLA", "Florida–Georgia"),
		new Rivalry("UGA", "GT", "Clean, Old-Fashioned Hate"),
		new Rivalry("FLA", "FSU", "Florida–Florida State"),
		new Rivalry("CLEM", "SC", "Palmetto Bowl"),
		new Rivalry("LOU", "UK", "Governor's Cup"),
		new Rivalry("IOWA", "ISU", "Cy-Hawk"),
		new Rivalry("ORE", "ORST", "Civil War"),
		new Rivalry("WASH", "WSU", "Apple Cup"),
		new Rivalry("USC", "ND", "USC–Notre Dame"),
		new Rivalry("ND", "NAVY", "Notre Dame–Navy"),
		new Rivalry("UTAH", "BYU", "Holy War"),
		new Rivalry("KU", "KSU", "Sunflower Showdown"),
		new Rivalry("MINN", "WIS", "Paul Bunyan's Axe"),
		new Rivalry("MICH", "MSU", "Paul Bunyan Trophy"),
	};
}
