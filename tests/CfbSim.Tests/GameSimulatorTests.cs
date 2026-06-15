using CfbSim.Core.Events;
using CfbSim.Core.Generation;
using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Sim.Game;
using Xunit;

namespace CfbSim.Tests;

public class GameSimulatorTests
{
    private static Team Team(ulong seed, int id, string name, string abbr, int prestige)
        => new PlayerGenerator().GenerateTeam(new Pcg32Rng(seed), id, name, abbr, prestige);

    private static (Team home, Team away) Matchup(int homePrestige = 80, int awayPrestige = 70)
        => (Team(1, 1, "Home", "HOM", homePrestige), Team(2, 2, "Away", "AWY", awayPrestige));

    [Fact]
    public void Game_IsDeterministic()
    {
        var (home, away) = Matchup();
        GameResult a = GameSimulator.Simulate(new Pcg32Rng(2026), home, away);
        GameResult b = GameSimulator.Simulate(new Pcg32Rng(2026), home, away);

        Assert.Equal(a.HomeScore, b.HomeScore);
        Assert.Equal(a.AwayScore, b.AwayScore);
        Assert.Equal(a.PlayLog.Count, b.PlayLog.Count);
    }

    [Fact]
    public void Game_NeverTies_AndBoxMatchesFinal()
    {
        var (home, away) = Matchup();
        for (ulong seed = 1; seed <= 60; seed++)
        {
            GameResult g = GameSimulator.Simulate(new Pcg32Rng(seed), home, away);
            Assert.False(g.IsTie, $"seed {seed} ended tied (OT should resolve it)");
            Assert.Equal(g.HomeScore, g.Box.Home.Points);
            Assert.Equal(g.AwayScore, g.Box.Away.Points);
        }
    }

    [Fact]
    public void Scores_AndPlayCounts_AreInSaneRanges()
    {
        var (home, away) = Matchup();
        for (ulong seed = 1; seed <= 60; seed++)
        {
            GameResult g = GameSimulator.Simulate(new Pcg32Rng(seed), home, away);
            Assert.InRange(g.HomeScore, 0, 100);
            Assert.InRange(g.AwayScore, 0, 100);
            Assert.InRange(g.PlayLog.Count, 80, 280);
            Assert.True(g.Box.Home.TotalYards >= 0 && g.Box.Away.TotalYards >= 0);
        }
    }

    [Fact]
    public void GameConcluded_IsPublished_WithFinalScore()
    {
        var (home, away) = Matchup();
        var bus = new EventBus();
        GameConcluded? captured = null;
        bus.Subscribe<GameConcluded>(e => captured = e);

        GameResult g = GameSimulator.Simulate(new Pcg32Rng(7), home, away, bus);

        Assert.NotNull(captured);
        Assert.Equal(g.HomeScore, captured!.HomeScore);
        Assert.Equal(g.AwayScore, captured.AwayScore);
    }

    [Fact]
    public void StrongerTeam_WinsClearMajority()
    {
        Team strong = Team(1, 1, "Blue", "BLU", 92);
        Team weak = Team(2, 2, "Low", "LOW", 38);

        int strongWins = 0;
        const int n = 40;
        for (ulong seed = 1; seed <= n; seed++)
        {
            GameResult g = GameSimulator.Simulate(new Pcg32Rng(seed), strong, weak);
            if (g.Winner.Id == strong.Id) strongWins++;
        }
        Assert.True(strongWins >= 32, $"strong team won only {strongWins}/{n}");
    }

    [Fact]
    public void ProducesOffensiveProduction()
    {
        var (home, away) = Matchup();
        GameResult g = GameSimulator.Simulate(new Pcg32Rng(2026), home, away);

        // A full game should generate meaningful yardage and first downs for both sides.
        Assert.True(g.Box.Home.TotalYards > 100);
        Assert.True(g.Box.Away.TotalYards > 100);
        Assert.True(g.Box.Home.FirstDowns > 5);
        Assert.NotEmpty(g.ScoringSummary);
    }
}
