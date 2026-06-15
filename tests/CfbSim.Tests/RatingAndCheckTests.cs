using CfbSim.Core.Checks;
using CfbSim.Core.Dice;
using CfbSim.Core.Ratings;
using CfbSim.Core.Rng;
using Xunit;

namespace CfbSim.Tests;

public class RatingAndCheckTests
{
    [Fact]
    public void Effective_BlendsValuesBeforeCurve()
    {
        // 0.65*14 + 0.35*15 = 14.35
        Assert.Equal(14.35, RatingMath.Effective(14, 15), 3);
    }

    [Theory]
    [InlineData(10, 0)]   // floor((10-10)/2) = 0
    [InlineData(14, 2)]   // floor((14-10)/2) = 2
    [InlineData(20, 5)]   // floor((20-10)/2) = 5
    [InlineData(1, -5)]   // floor((1-10)/2) = floor(-4.5) = -5
    public void Modifier_FollowsBg3Curve(double effective, int expected)
        => Assert.Equal(expected, RatingMath.Modifier(effective));

    [Fact]
    public void ModeFor_GivesAdvantageOnLargeGap()
    {
        Assert.Equal(RollMode.Advantage, RatingMath.ModeFor(18, 11));     // +7 ≥ 6
        Assert.Equal(RollMode.Disadvantage, RatingMath.ModeFor(11, 18));  // -7 ≤ -6
        Assert.Equal(RollMode.Normal, RatingMath.ModeFor(14, 12));        // +2
    }

    [Fact]
    public void DcFor_IsTenPlusDefenderMod()
    {
        Assert.Equal(10, CheckResolver.DcFor(0));
        Assert.Equal(13, CheckResolver.DcFor(3));
        Assert.Equal(7, CheckResolver.DcFor(-3));
    }

    [Fact]
    public void Crit_AlwaysSucceeds_EvenAgainstHighDc()
    {
        // Force nat 20s by exhausting until we hit one against an impossible DC.
        var rng = new Pcg32Rng(5);
        int crits = 0;
        for (int i = 0; i < 5000 && crits < 10; i++)
        {
            CheckResult r = CheckResolver.Resolve(rng, attackerMod: -5, defenderMod: 50);
            if (r.Crit)
            {
                crits++;
                Assert.True(r.Success);
            }
        }
        Assert.True(crits > 0, "expected at least one nat 20 in 5000 rolls");
    }

    [Fact]
    public void Blunder_AlwaysFails_EvenAgainstTrivialDc()
    {
        var rng = new Pcg32Rng(5);
        int blunders = 0;
        for (int i = 0; i < 5000 && blunders < 10; i++)
        {
            CheckResult r = CheckResolver.Resolve(rng, attackerMod: 50, defenderMod: -50);
            if (r.Blunder)
            {
                blunders++;
                Assert.False(r.Success);
            }
        }
        Assert.True(blunders > 0, "expected at least one nat 1 in 5000 rolls");
    }

    [Fact]
    public void StrongerAttacker_SucceedsMoreOften()
    {
        var rng = new Pcg32Rng(2026);
        int strongWins = CountSuccesses(rng, atkSkill: 18, atkAttr: 17, defSkill: 8, defAttr: 8, n: 5000);
        int weakWins = CountSuccesses(rng, atkSkill: 8, atkAttr: 8, defSkill: 18, defAttr: 17, n: 5000);
        Assert.True(strongWins > weakWins + 1000,
            $"strong={strongWins}, weak={weakWins}");
    }

    private static int CountSuccesses(IRng rng, int atkSkill, int atkAttr, int defSkill, int defAttr, int n)
    {
        int wins = 0;
        for (int i = 0; i < n; i++)
            if (CheckResolver.ResolveContest(rng, atkSkill, atkAttr, defSkill, defAttr).Success)
                wins++;
        return wins;
    }
}
