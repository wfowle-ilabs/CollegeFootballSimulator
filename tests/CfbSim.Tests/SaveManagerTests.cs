using CfbSim.Core.Generation;
using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Save;
using CfbSim.Core.Services;
using CfbSim.Core.Sim.Season;
using Xunit;

namespace CfbSim.Tests;

public class SaveManagerTests
{
    private static string TempDir(string name)
    {
        string dir = Path.Combine(Path.GetTempPath(), "cfbsim_tests", name);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void RngSnapshot_RestoresIdenticalSequence_IncludingGaussian()
    {
        var a = new Pcg32Rng(123);
        for (int i = 0; i < 37; i++) a.NextInt(1, 100);
        a.NextGaussian(); // may leave a pending spare in internal state

        Pcg32State snap = a.Snapshot();
        var seqA = new List<double>();
        for (int i = 0; i < 20; i++) seqA.Add(a.NextInt(1, 1000) + a.NextGaussian());

        var b = Pcg32Rng.Restore(snap);
        var seqB = new List<double>();
        for (int i = 0; i < 20; i++) seqB.Add(b.NextInt(1, 1000) + b.NextGaussian());

        Assert.Equal(seqA, seqB);
    }

    [Fact]
    public void League_RoundTrips_Losslessly()
    {
        string dir = TempDir(nameof(League_RoundTrips_Losslessly));
        var rng = new Pcg32Rng(1);
        League league = LeagueBuilder.Build(rng);
        SeriesHistory history = SeriesHistory.SeededFor(league);
        SeasonState season = SeasonDriver.Initialize(league, new Schedule { Year = 2026 });

        SaveManager.Save(dir, new GameSave { Year = 2026, League = league, History = history, Season = season, Rng = rng.Snapshot() });
        GameSave loaded = SaveManager.Load(dir);

        Assert.Equal(league.AllTeams.Count(), loaded.League.AllTeams.Count());

        Team georgia = league.FindTeam("Georgia")!;
        Team loadedGeorgia = loaded.League.FindTeam("Georgia")!;
        Assert.Equal(georgia.ConferenceId, loadedGeorgia.ConferenceId);
        Assert.Equal(georgia.Roster.Count, loadedGeorgia.Roster.Count);

        Player qb = georgia.Starter(Position.QB)!;
        Player loadedQb = loadedGeorgia.Starter(Position.QB)!;
        Assert.Equal(qb.Name, loadedQb.Name);
        Assert.Equal(qb.Attributes.Awareness, loadedQb.Attributes.Awareness);
        Assert.Equal(qb.Of(Skill.ShortAccuracy), loadedQb.Of(Skill.ShortAccuracy));
    }

    [Fact]
    public void RivalryHistory_RoundTrips()
    {
        string dir = TempDir(nameof(RivalryHistory_RoundTrips));
        var rng = new Pcg32Rng(1);
        League league = LeagueBuilder.Build(rng);
        SeriesHistory history = SeriesHistory.SeededFor(league);
        SeasonState season = SeasonDriver.Initialize(league, new Schedule { Year = 2026 });

        int rivalriesBefore = history.Rivalries.Count();
        SaveManager.Save(dir, new GameSave { Year = 2026, League = league, History = history, Season = season, Rng = rng.Snapshot() });
        GameSave loaded = SaveManager.Load(dir);

        Assert.Equal(rivalriesBefore, loaded.History.Rivalries.Count());
        Team a = league.FindTeam("ALA")!, b = league.FindTeam("AUB")!;
        Assert.Equal("Iron Bowl", loaded.History.RivalryName(a.Id, b.Id));
    }

    [Fact]
    public void SaveReload_MidSeason_ContinuesDeterministically()
    {
        string dir = TempDir(nameof(SaveReload_MidSeason_ContinuesDeterministically));

        var straight = RunStraight(7);
        var viaSave = RunWithSaveReload(7, saveAfterWeeks: 6, dir);

        Assert.Equal(straight.Count, viaSave.Count);
        foreach (var kv in straight)
            Assert.Equal(kv.Value, viaSave[kv.Key]); // identical W-L for every team
    }

    [Fact]
    public void Load_RejectsUnsupportedSchemaVersion()
    {
        string dir = TempDir(nameof(Load_RejectsUnsupportedSchemaVersion));
        var rng = new Pcg32Rng(1);
        League league = LeagueBuilder.Build(rng);
        SeasonState season = SeasonDriver.Initialize(league, new Schedule { Year = 2026 });

        SaveManager.Save(dir, new GameSave
        {
            SchemaVersion = 999,
            Year = 2026,
            League = league,
            History = SeriesHistory.SeededFor(league),
            Season = season,
            Rng = rng.Snapshot(),
        });

        Assert.Throws<SaveManager.SaveVersionException>(() => SaveManager.Load(dir));
    }

    private static Dictionary<int, (int W, int L)> RunStraight(ulong seed)
    {
        var rng = new Pcg32Rng(seed);
        League league = LeagueBuilder.Build(rng);
        SeriesHistory history = SeriesHistory.SeededFor(league);
        Schedule schedule = ScheduleBuilder.Build(rng, league, history, 2026);
        SeasonState state = SeasonDriver.Initialize(league, schedule);
        while (!state.IsComplete) SeasonDriver.AdvanceWeek(rng, league, state, history);
        return state.Records.ToDictionary(k => k.Key, k => (k.Value.Wins, k.Value.Losses));
    }

    private static Dictionary<int, (int W, int L)> RunWithSaveReload(ulong seed, int saveAfterWeeks, string dir)
    {
        var rng = new Pcg32Rng(seed);
        League league = LeagueBuilder.Build(rng);
        SeriesHistory history = SeriesHistory.SeededFor(league);
        Schedule schedule = ScheduleBuilder.Build(rng, league, history, 2026);
        SeasonState state = SeasonDriver.Initialize(league, schedule);
        for (int i = 0; i < saveAfterWeeks; i++) SeasonDriver.AdvanceWeek(rng, league, state, history);

        SaveManager.Save(dir, new GameSave { Year = 2026, League = league, History = history, Season = state, Rng = rng.Snapshot() });

        GameSave loaded = SaveManager.Load(dir);
        var rng2 = Pcg32Rng.Restore(loaded.Rng);
        while (!loaded.Season.IsComplete) SeasonDriver.AdvanceWeek(rng2, loaded.League, loaded.Season, loaded.History);
        return loaded.Season.Records.ToDictionary(k => k.Key, k => (k.Value.Wins, k.Value.Losses));
    }
}
