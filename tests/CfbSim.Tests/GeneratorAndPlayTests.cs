using CfbSim.Core.Generation;
using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Sim;
using Xunit;

namespace CfbSim.Tests;

public class GeneratorAndPlayTests
{
    private static Team Team(ulong seed, int prestige, int id = 1) =>
        new PlayerGenerator().GenerateTeam(new Pcg32Rng(seed), id, "Test", "TST", prestige);

    [Fact]
    public void Generation_IsDeterministic()
    {
        Team a = Team(seed: 555, prestige: 70);
        Team b = Team(seed: 555, prestige: 70);

        Assert.Equal(a.Roster.Count, b.Roster.Count);
        for (int i = 0; i < a.Roster.Count; i++)
        {
            Assert.Equal(a.Roster[i].Name, b.Roster[i].Name);
            Assert.Equal(a.Roster[i].Position, b.Roster[i].Position);
            Assert.Equal(a.Roster[i].Attributes.Strength, b.Roster[i].Attributes.Strength);
            Assert.Equal(a.Roster[i].JerseyNumber, b.Roster[i].JerseyNumber);
        }
    }

    [Fact]
    public void AllRatings_StayWithinScale()
    {
        Team t = Team(seed: 1, prestige: 50);
        foreach (Player p in t.Roster)
        {
            foreach (int v in new[] { p.Attributes.Strength, p.Attributes.Agility, p.Attributes.Speed,
                                      p.Attributes.Awareness, p.Attributes.Durability, p.Attributes.Composure })
                Assert.InRange(v, 1, 20);
            foreach (int v in p.Skills.Values)
                Assert.InRange(v, 1, 20);
        }
    }

    [Fact]
    public void Roster_FieldsTheInsideRunPositions()
    {
        Team t = Team(seed: 1, prestige: 50);
        Assert.NotNull(t.Starter(Position.OL));
        Assert.NotNull(t.Starter(Position.RB));
        Assert.NotNull(t.Starter(Position.DL));
        Assert.NotNull(t.Starter(Position.LB));
    }

    [Fact]
    public void HigherPrestige_ProducesHigherAverageRatings()
    {
        double blue = AverageStrength(Team(seed: 10, prestige: 95));
        double low = AverageStrength(Team(seed: 10, prestige: 20));
        Assert.True(blue > low + 1.0, $"blue={blue:0.00}, low={low:0.00}");
    }

    private static double AverageStrength(Team t)
    {
        double sum = 0;
        foreach (Player p in t.Roster) sum += p.Attributes.Strength;
        return sum / t.Roster.Count;
    }

    [Fact]
    public void InsideRun_IsDeterministic()
    {
        Team o1 = Team(seed: 1, prestige: 80, id: 1), d1 = Team(seed: 2, prestige: 50, id: 2);
        Team o2 = Team(seed: 1, prestige: 80, id: 1), d2 = Team(seed: 2, prestige: 50, id: 2);

        var play = new Pcg32Rng(777);
        var play2 = new Pcg32Rng(777);
        for (int i = 0; i < 50; i++)
        {
            InsideRunResult a = InsideRunResolver.Resolve(play, o1, d1);
            InsideRunResult b = InsideRunResolver.Resolve(play2, o2, d2);
            Assert.Equal(a.Yards, b.Yards);
            Assert.Equal(a.Fumble, b.Fumble);
        }
    }

    [Fact]
    public void InsideRun_ProducesPlausibleYardage()
    {
        Team o = Team(seed: 1, prestige: 60, id: 1), d = Team(seed: 2, prestige: 60, id: 2);
        var rng = new Pcg32Rng(2026);

        const int n = 2000;
        int total = 0, min = int.MaxValue, max = int.MinValue;
        for (int i = 0; i < n; i++)
        {
            int y = InsideRunResolver.Resolve(rng, o, d).Yards;
            total += y;
            min = Math.Min(min, y);
            max = Math.Max(max, y);
        }
        double avg = (double)total / n;

        Assert.InRange(avg, 1.5, 8.0);     // believable yards-per-carry band
        Assert.True(min < 0, "some carries should lose yards");
        Assert.True(max >= 15, "some carries should break for chunk gains");
    }

    [Fact]
    public void StrongOffense_OutgainsWeakOffense()
    {
        Team strong = Team(seed: 1, prestige: 95, id: 1);
        Team weakD = Team(seed: 2, prestige: 25, id: 2);
        Team weakO = Team(seed: 3, prestige: 25, id: 3);
        Team strongD = Team(seed: 4, prestige: 95, id: 4);

        double strongAvg = AverageYards(strong, weakD, seed: 50);
        double weakAvg = AverageYards(weakO, strongD, seed: 50);
        Assert.True(strongAvg > weakAvg, $"strong={strongAvg:0.00}, weak={weakAvg:0.00}");
    }

    private static double AverageYards(Team o, Team d, ulong seed)
    {
        var rng = new Pcg32Rng(seed);
        const int n = 3000;
        long total = 0;
        for (int i = 0; i < n; i++) total += InsideRunResolver.Resolve(rng, o, d).Yards;
        return (double)total / n;
    }
}
