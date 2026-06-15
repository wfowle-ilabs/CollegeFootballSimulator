using CfbSim.Core.Generation;
using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Sim.Game;
using CfbSim.Core.Sim.Play;
using CfbSim.Core.Sim.Season;
using Xunit;

namespace CfbSim.Tests;

/// <summary>Covers the training-prep boosts (determinism + real in-sim effect) and the new
/// special-teams paths (two-point conversions, kickoff/punt returns).</summary>
public class TrainingAndSpecialTeamsTests
{
    private static SeasonState EmptyState(int year = 2026)
        => new() { Year = year, Schedule = new Schedule { Year = year } };

    [Fact]
    public void AiBoost_IsDeterministic()
    {
        TeamBoost a = TrainingBoosts.ForGame(EmptyState(), teamId: 42, week: 3, userTeamId: 7);
        TeamBoost b = TrainingBoosts.ForGame(EmptyState(), teamId: 42, week: 3, userTeamId: 7);
        Assert.Equal(a.Offense, b.Offense);
        Assert.Equal(a.Defense, b.Defense);
        Assert.Equal(a.SpecialTeams, b.SpecialTeams);
        Assert.Equal(a.Fatigue, b.Fatigue);
    }

    [Fact]
    public void UserPlan_DrillsAddOffense_AndRestOffsetsFatigue()
    {
        SeasonState state = EmptyState();
        const int week = 3, userId = 7;
        DateOnly day = SeasonCalendar.SundayOfWeek(state.Year, week).AddDays(4);
        state.Training[TrainingKey.Of(day, TimeSlot.Morning)] = TrainingActivity.PositionDrills;
        state.Training[TrainingKey.Of(day, TimeSlot.Afternoon)] = TrainingActivity.Rest;

        TeamBoost b = TrainingBoosts.ForGame(state, userId, week, userId);
        Assert.True(b.Offense > 0);
        Assert.Equal(0, b.Fatigue); // drills (+2) offset by rest (-3), clamped at 0
    }

    [Fact]
    public void OffenseBoost_LiftsTheBoostedTeamsScoring()
    {
        int plain = 0, boosted = 0;
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var build = new Pcg32Rng(seed);
            League league = LeagueBuilder.Build(build);
            var teams = league.AllTeams.ToList();
            Team home = teams[0], away = teams[1];

            home.ActiveBoost = TeamBoost.None;
            away.ActiveBoost = TeamBoost.None;
            plain += GameSimulator.Simulate(new Pcg32Rng(seed * 131 + 7), home, away).HomeScore;

            home.ActiveBoost = new TeamBoost { Offense = 6 };
            boosted += GameSimulator.Simulate(new Pcg32Rng(seed * 131 + 7), home, away).HomeScore;
            home.ActiveBoost = TeamBoost.None;
        }
        Assert.True(boosted > plain, $"boosted total {boosted} should beat plain total {plain}");
    }

    [Fact]
    public void Kickoff_SpotStaysInBounds_OrIsAReturnTd()
    {
        var rng = new Pcg32Rng(5);
        var teams = LeagueBuilder.Build(rng).AllTeams.ToList();
        for (int i = 0; i < 300; i++)
        {
            KickoffResult ko = SpecialTeamsResolver.Kickoff(rng, teams[i % teams.Count]);
            if (ko.ReturnTouchdown) Assert.Equal(100, ko.Spot);
            else Assert.InRange(ko.Spot, 12, 48);
        }
    }

    [Fact]
    public void ReturnTouchdowns_HappenButAreRare()
    {
        var rng = new Pcg32Rng(11);
        var teams = LeagueBuilder.Build(rng).AllTeams.ToList();
        int tds = 0, n = 0;
        for (int i = 0; i < teams.Count; i++)
            for (int k = 0; k < 60; k++, n++)
                if (SpecialTeamsResolver.Kickoff(rng, teams[i]).ReturnTouchdown) tds++;

        Assert.True(tds > 0, "expected at least one kickoff return TD across many kickoffs");
        Assert.True(tds < n / 20, $"return TDs ({tds}/{n}) should be rare (<5%)");
    }

    [Fact]
    public void Punt_NetsAReasonableDistance_WhenNotMuffed()
    {
        var rng = new Pcg32Rng(9);
        var teams = LeagueBuilder.Build(rng).AllTeams.ToList();
        Team o = teams[0], d = teams[1];
        for (int i = 0; i < 300; i++)
        {
            PlayOutcome p = SpecialTeamsResolver.Punt(rng, o, d);
            if (!p.Muffed && !p.Blocked && !p.ReturnTouchdown) Assert.InRange(p.YardsGained, 18, 70);
        }
    }

    [Fact]
    public void TwoPointConversions_AreAttempted_InCloseLateGames()
    {
        bool sawTwoPoint = false;
        for (ulong seed = 1; seed <= 250 && !sawTwoPoint; seed++)
        {
            var build = new Pcg32Rng(seed);
            var teams = LeagueBuilder.Build(build).AllTeams.ToList();
            GameResult g = GameSimulator.Simulate(new Pcg32Rng(seed * 17 + 3), teams[0], teams[1]);
            if (g.PlayLog.Any(l => l.Contains("2-PT"))) sawTwoPoint = true;
        }
        Assert.True(sawTwoPoint, "expected at least one two-point attempt across 250 games");
    }
}
