using CfbSim.Core.Rng;

namespace CfbSim.Core.Dice;

/// <summary>How a d20 is rolled — advantage/disadvantage roll twice and keep one.</summary>
public enum RollMode { Normal, Advantage, Disadvantage }

/// <summary>The outcome of a single d20 roll.</summary>
public readonly record struct D20Roll(int Value, int? Other, RollMode Mode)
{
    public bool IsCrit => Value == 20;
    public bool IsBlunder => Value == 1;
}

/// <summary>The dice layer. All rolls flow through here so they stay seeded.</summary>
public static class DiceRoller
{
    public static D20Roll RollD20(IRng rng, RollMode mode = RollMode.Normal)
    {
        int a = rng.NextInt(1, 20);
        if (mode == RollMode.Normal)
            return new D20Roll(a, null, mode);

        int b = rng.NextInt(1, 20);
        bool adv = mode == RollMode.Advantage;
        int chosen = adv ? Math.Max(a, b) : Math.Min(a, b);
        int other = adv ? Math.Min(a, b) : Math.Max(a, b);
        return new D20Roll(chosen, other, mode);
    }
}
