using CfbSim.Core.Dice;
using CfbSim.Core.Ratings;
using CfbSim.Core.Rng;

namespace CfbSim.Core.Checks;

/// <summary>The full, inspectable result of one d20 check.</summary>
public readonly record struct CheckResult(
	int Roll,
	int? OtherRoll,
	RollMode Mode,
	int AttackerMod,
	int Dc,
	int Total,
	int Margin,
	bool Success,
	bool Crit,
	bool Blunder)
{
	/// <summary>A one-line, human-readable trace of this check.</summary>
	public string Describe(string label)
	{
		string roll = OtherRoll is { } o
			? $"d20({Roll}/{o} {Mode.ToString().ToLowerInvariant()})"
			: $"d20({Roll})";
		string outcome = Crit ? "CRIT" : Blunder ? "BLUNDER" : Success ? "success" : "fail";
		return $"{label}: {roll} + {AttackerMod:+0;-0;+0} = {Total} vs DC {Dc} → {outcome} (margin {Margin:+0;-0;0})";
	}
}

/// <summary>
/// The single primitive every contested event resolves through:
/// d20 + attackerMod vs DC = 10 + defenderMod. Crit (nat 20) always succeeds,
/// blunder (nat 1) always fails; otherwise compare to the DC. See docs/mechanics.qmd.
/// </summary>
public static class CheckResolver
{
	/// <summary>Opposed check expressed as a static DC against a passive defender.</summary>
	public static int DcFor(int defenderMod) => 10 + defenderMod;

	public static CheckResult Resolve(IRng rng, int attackerMod, int defenderMod, RollMode mode = RollMode.Normal)
	{
		D20Roll roll = DiceRoller.RollD20(rng, mode);
		int dc = DcFor(defenderMod);
		int total = roll.Value + attackerMod;
		int margin = total - dc;

		bool success = roll.IsCrit || (!roll.IsBlunder && total >= dc);

		return new CheckResult(
			Roll: roll.Value,
			OtherRoll: roll.Other,
			Mode: roll.Mode,
			AttackerMod: attackerMod,
			Dc: dc,
			Total: total,
			Margin: margin,
			Success: success,
			Crit: roll.IsCrit,
			Blunder: roll.IsBlunder);
	}

	/// <summary>
	/// Contest of two players: blend each side's skill+attribute, derive
	/// modifiers, auto-apply advantage/disadvantage from the rating gap.
	/// </summary>
	public static CheckResult ResolveContest(
		IRng rng,
		int attackerSkill, int attackerAttr,
		int defenderSkill, int defenderAttr,
		double skillWeight = RatingMath.DefaultSkillWeight)
	{
		double atkEff = RatingMath.Effective(attackerSkill, attackerAttr, skillWeight);
		double defEff = RatingMath.Effective(defenderSkill, defenderAttr, skillWeight);
		RollMode mode = RatingMath.ModeFor(atkEff, defEff);
		return Resolve(rng, RatingMath.Modifier(atkEff), RatingMath.Modifier(defEff), mode);
	}
}
