using System.Text;

namespace CfbSim.Core.Media;

/// <summary>
/// The v1 baseline writer: deterministic, instant, no model dependency. Fills authored
/// templates from the <see cref="NarrativeContext"/>, varying phrasing by a stable hash
/// of the matchup (so it's reproducible without touching the sim RNG).
/// </summary>
public sealed class TemplateNarrativeGenerator : INarrativeGenerator
{
	private static readonly string[] Verbs = { "beat", "defeated", "got past", "took down", "handled" };
	private static readonly string[] UpsetVerbs = { "stunned", "shocked", "upset", "knocked off" };

	public NewsArticle Generate(NarrativeContext c)
	{
		if (!c.Full)
		{
			return new NewsArticle
			{
				Year = c.Year, Week = c.Week, Type = ArticleType.ShortRecap, Full = false,
				Headline = $"{c.WinnerAbbr} {c.WinnerScore}, {c.LoserAbbr} {c.LoserScore}",
				Body = $"{c.WinnerName} {Pick(Verbs, c)} {c.LoserName} {c.WinnerScore}-{c.LoserScore}.",
				TeamIds = { c.WinnerId, c.LoserId },
			};
		}

		string headline = c.Type switch
		{
			ArticleType.UpsetAlert => $"UPSET: {c.WinnerName} {Pick(UpsetVerbs, c)} {RankLabel(c.LoserRank)}{c.LoserName}",
			ArticleType.RivalryResult => $"{c.RivalryName}: {c.WinnerName} prevails over {c.LoserName}",
			_ => $"{RankLabel(c.WinnerRank)}{c.WinnerName} {Pick(Verbs, c)} {RankLabel(c.LoserRank)}{c.LoserName}",
		};

		var body = new StringBuilder();
		body.Append($"{c.WinnerName} {Pick(Verbs, c)} {c.LoserName} {c.WinnerScore}-{c.LoserScore}{MarginPhrase(c)}");
		if (c.IsRivalry) body.Append($" to claim the {c.RivalryName}");
		body.Append('.');
		if (c.Type == ArticleType.UpsetAlert)
			body.Append($" The result lands as one of Week {c.Week}'s biggest surprises.");
		if (c.WinnerRank is > 0 and <= 25)
			body.Append($" The win keeps No. {c.WinnerRank} {c.WinnerAbbr} on track.");
		if (c.IsUserGame)
			body.Append(" (Your team.)");

		return new NewsArticle
		{
			Year = c.Year, Week = c.Week, Type = c.Type, Full = true,
			Headline = headline,
			Body = body.ToString(),
			TeamIds = { c.WinnerId, c.LoserId },
		};
	}

	private static string RankLabel(int rank) => rank is > 0 and <= 25 ? $"No. {rank} " : "";

	private static string MarginPhrase(NarrativeContext c) => c.Margin switch
	{
		>= 28 => " in a rout",
		>= 14 => " comfortably",
		<= 3 => " in a thriller",
		_ => "",
	};

	private static string Pick(string[] options, NarrativeContext c)
	{
		unchecked
		{
			int h = 17;
			h = h * 31 + c.Year;
			h = h * 31 + c.Week;
			h = h * 31 + c.WinnerId;
			h = h * 31 + c.LoserId;
			return options[((h % options.Length) + options.Length) % options.Length];
		}
	}
}
