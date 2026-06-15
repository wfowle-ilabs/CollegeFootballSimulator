using CfbSim.Core.Model;
using CfbSim.Core.Sim.Play;

namespace CfbSim.Core.Stats;

/// <summary>
/// Folds play outcomes into the box score. The game layer calls this with the
/// post-field-cap actual yards and whether the play scored / moved the chains.
/// </summary>
public static class StatAggregator
{
	public static void RecordScrimmage(
		BoxScore box, Team offense, Team defense,
		PlayOutcome outcome, int actualYards, bool touchdown, bool firstDown)
	{
		TeamStatLine off = box.TeamOf(offense.Id);
		off.Plays++;
		if (firstDown) off.FirstDowns++;
		if (outcome.IsTurnover) off.Turnovers++;

		switch (outcome.PlayType)
		{
			case PlayType.InsideRun or PlayType.OutsideRun:
				off.RushYds += actualYards;
				if (outcome.BallCarrier is { } rb)
				{
					PlayerStatLine line = box.For(rb, offense.Id);
					line.RushAtt++;
					line.RushYds += actualYards;
					if (touchdown) line.RushTD++;
				}
				break;

			case PlayType.ShortPass or PlayType.DeepPass:
				RecordPass(box, offense, defense, outcome, actualYards, touchdown, off);
				break;
		}
	}

	private static void RecordPass(
		BoxScore box, Team offense, Team defense,
		PlayOutcome outcome, int actualYards, bool touchdown, TeamStatLine off)
	{
		PlayerStatLine? qb = outcome.Passer is { } p ? box.For(p, offense.Id) : null;
		PlayerStatLine? wr = outcome.Receiver is { } r ? box.For(r, offense.Id) : null;

		if (outcome.Sack)
		{
			off.PassYds += actualYards; // sack yardage counts against the offense's net pass game
			if (outcome.Defender is { } d) box.For(d, defense.Id).Sacks++;
			return;
		}

		if (qb is not null) qb.PassAtt++;
		if (wr is not null) wr.Targets++;

		if (outcome.Turnover == TurnoverKind.Interception)
		{
			if (qb is not null) qb.PassInt++;
			if (outcome.Defender is { } d) box.For(d, defense.Id).Interceptions++;
			return;
		}

		if (outcome.Incomplete) return;

		// Completion.
		off.PassYds += actualYards;
		if (qb is not null)
		{
			qb.PassComp++;
			qb.PassYds += actualYards;
			if (touchdown) qb.PassTD++;
		}
		if (wr is not null)
		{
			wr.Rec++;
			wr.RecYds += actualYards;
			if (touchdown) wr.RecTD++;
		}
	}
}
