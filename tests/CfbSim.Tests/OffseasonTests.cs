using CfbSim.Core.Generation;
using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Save;
using CfbSim.Core.Sim.Offseason;
using Xunit;

namespace CfbSim.Tests;

public class OffseasonTests
{
    private static readonly int RosterSize = RosterSpec.Default.Counts.Sum(c => c.Count);

    [Fact]
    public void Offseason_GraduatesSeniors_AgesEveryoneElse_AndRefills()
    {
        var rng = new Pcg32Rng(1);
        GameSave save = SeasonCycle.NewGame(rng, 2026);
        Team team = save.League.FindTeam("Georgia")!;

        var seniorIdsBefore = team.Roster.Where(p => p.Class == ClassYear.Senior).Select(p => p.Id).ToHashSet();
        int juniorId = team.Roster.First(p => p.Class == ClassYear.Junior).Id;

        SeasonCycle.RunFullYear(rng, save);

        Assert.Equal(RosterSize, team.Roster.Count);                       // refilled to spec
        Assert.DoesNotContain(team.Roster, p => seniorIdsBefore.Contains(p.Id)); // seniors gone
        Assert.Equal(ClassYear.Senior, team.Roster.First(p => p.Id == juniorId).Class); // junior aged up
        Assert.Contains(team.Roster, p => p.Class == ClassYear.Freshman);  // incoming class added

        // Every position is still staffed for the next season.
        foreach ((Position pos, int count) in RosterSpec.Default.Counts)
            Assert.True(team.Roster.Count(p => p.Position == pos) >= count);
    }

    [Fact]
    public void MultiSeason_ArchivesEachYear_AndAdvancesTheCalendar()
    {
        var rng = new Pcg32Rng(2);
        GameSave save = SeasonCycle.NewGame(rng, 2026);
        var teamIds = save.League.AllTeams.Select(t => t.Id).ToHashSet();

        for (int i = 0; i < 3; i++)
        {
            int champ = SeasonCycle.RunFullYear(rng, save).NationalChampionId;
            Assert.Contains(champ, teamIds);
        }

        Assert.Equal(3, save.Archive.Seasons.Count);
        Assert.Equal(new[] { 2026, 2027, 2028 }, save.Archive.Seasons.Select(s => s.Year));
        Assert.Equal(2029, save.Year);
        Assert.All(save.Archive.Seasons, s => Assert.Equal(135, s.Finishes.Count)); // every team has a finish
    }

    [Fact]
    public void GeneratedPlayerIds_StayUnique_AcrossOffseasons()
    {
        var rng = new Pcg32Rng(3);
        GameSave save = SeasonCycle.NewGame(rng, 2026);
        for (int i = 0; i < 4; i++) SeasonCycle.RunFullYear(rng, save);

        var ids = save.League.AllTeams.SelectMany(t => t.Roster).Select(p => p.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void MultiSeason_IsDeterministic()
    {
        Assert.Equal(RunChampions(5, 3), RunChampions(5, 3));
    }

    [Fact]
    public void Save_RoundTripsArchiveAndIdAllocator()
    {
        string dir = Path.Combine(Path.GetTempPath(), "cfbsim_tests", nameof(Save_RoundTripsArchiveAndIdAllocator));
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);

        var rng = new Pcg32Rng(7);
        GameSave save = SeasonCycle.NewGame(rng, 2026);
        SeasonCycle.RunFullYear(rng, save);
        SeasonCycle.RunFullYear(rng, save);
        save.Rng = rng.Snapshot();

        SaveManager.Save(dir, save);
        GameSave loaded = SaveManager.Load(dir);

        Assert.Equal(2, loaded.Archive.Seasons.Count);
        Assert.Equal(save.NextPlayerId, loaded.NextPlayerId);
        Assert.Equal(save.Year, loaded.Year);
        Assert.Equal(save.Archive.Seasons[0].NationalChampionId, loaded.Archive.Seasons[0].NationalChampionId);
        Assert.Equal(save.League.AllTeams.Count(), loaded.League.AllTeams.Count());
    }

    private static List<int> RunChampions(ulong seed, int years)
    {
        var rng = new Pcg32Rng(seed);
        GameSave save = SeasonCycle.NewGame(rng, 2026);
        var champs = new List<int>();
        for (int i = 0; i < years; i++) champs.Add(SeasonCycle.RunFullYear(rng, save).NationalChampionId);
        return champs;
    }
}
