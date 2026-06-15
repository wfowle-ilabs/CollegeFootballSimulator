using CfbSim.Core.Dice;
using CfbSim.Core.Rng;
using Xunit;

namespace CfbSim.Tests;

public class RngAndDiceTests
{
    [Fact]
    public void SameSeed_ProducesIdenticalSequence()
    {
        var a = new Pcg32Rng(42);
        var b = new Pcg32Rng(42);
        for (int i = 0; i < 1000; i++)
            Assert.Equal(a.NextInt(1, 20), b.NextInt(1, 20));
    }

    [Fact]
    public void DifferentSeeds_Diverge()
    {
        var a = new Pcg32Rng(1);
        var b = new Pcg32Rng(2);
        bool anyDifferent = false;
        for (int i = 0; i < 50; i++)
            if (a.NextInt(1, 1_000_000) != b.NextInt(1, 1_000_000))
                anyDifferent = true;
        Assert.True(anyDifferent);
    }

    [Fact]
    public void NextInt_StaysInRange()
    {
        var rng = new Pcg32Rng(7);
        for (int i = 0; i < 10_000; i++)
        {
            int v = rng.NextInt(1, 20);
            Assert.InRange(v, 1, 20);
        }
    }

    [Fact]
    public void NextInt_CoversBothEnds()
    {
        var rng = new Pcg32Rng(7);
        bool sawMin = false, sawMax = false;
        for (int i = 0; i < 10_000; i++)
        {
            int v = rng.NextInt(1, 20);
            sawMin |= v == 1;
            sawMax |= v == 20;
        }
        Assert.True(sawMin && sawMax);
    }

    [Fact]
    public void Advantage_PicksHigher_Disadvantage_PicksLower()
    {
        var rng = new Pcg32Rng(99);
        for (int i = 0; i < 1000; i++)
        {
            D20Roll adv = DiceRoller.RollD20(rng, RollMode.Advantage);
            Assert.True(adv.Value >= adv.Other);

            D20Roll dis = DiceRoller.RollD20(rng, RollMode.Disadvantage);
            Assert.True(dis.Value <= dis.Other);
        }
    }

    [Fact]
    public void Gaussian_HasRoughlyZeroMeanUnitVariance()
    {
        var rng = new Pcg32Rng(123);
        const int n = 50_000;
        double sum = 0, sumSq = 0;
        for (int i = 0; i < n; i++)
        {
            double g = rng.NextGaussian();
            sum += g;
            sumSq += g * g;
        }
        double mean = sum / n;
        double variance = sumSq / n - mean * mean;
        Assert.InRange(mean, -0.05, 0.05);
        Assert.InRange(variance, 0.9, 1.1);
    }
}
