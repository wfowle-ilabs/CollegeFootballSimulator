using CfbSim.Core.Dice;

namespace CfbSim.Core.Ratings;

/// <summary>
/// The rating math (see docs/mechanics.qmd). Ratings live on a 1–20 scale.
/// A check blends a position skill with a supporting core attribute, then maps
/// the blended value to a BG3-style modifier. Blending the VALUES before the
/// curve is what yields granularity from a coarse 1–20 scale.
/// </summary>
public static class RatingMath
{
    public const double DefaultSkillWeight = 0.65;
    public const int AdvantageGap = 6;

    /// <summary>Blended 1–20 effective rating from a skill and a supporting attribute.</summary>
    public static double Effective(int skill, int attribute, double skillWeight = DefaultSkillWeight)
        => skillWeight * skill + (1.0 - skillWeight) * attribute;

    /// <summary>BG3 modifier curve: floor((effective - 10) / 2) → roughly -5..+5.</summary>
    public static int Modifier(double effectiveRating)
        => (int)Math.Floor((effectiveRating - 10.0) / 2.0);

    public static int Modifier(int skill, int attribute, double skillWeight = DefaultSkillWeight)
        => Modifier(Effective(skill, attribute, skillWeight));

    /// <summary>
    /// A large effective-rating gap tilts the dice: the stronger side gains
    /// advantage, the weaker disadvantage. This favors favorites without
    /// widening the modifier range (keeps the dice-dominant feel).
    /// </summary>
    public static RollMode ModeFor(double attackerEffective, double defenderEffective)
    {
        double gap = attackerEffective - defenderEffective;
        if (gap >= AdvantageGap) return RollMode.Advantage;
        if (-gap >= AdvantageGap) return RollMode.Disadvantage;
        return RollMode.Normal;
    }
}
