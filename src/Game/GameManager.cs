using Godot;
using CfbSim.Core.Events;
using CfbSim.Core.Generation;
using CfbSim.Core.Media;
using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Save;
using CfbSim.Core.Services;
using CfbSim.Core.Sim.Offseason;
using CfbSim.Core.Sim.Postseason;
using CfbSim.Core.Sim.Season;
using CfbSim.Core.Stats;

namespace CollegeFootballSimGoDot;

/// <summary>
/// Flow + state controller bridging the UI to the engine-free sim core (the SimDriver
/// role from the architecture). Owns the active <see cref="GameSave"/> and the seeded RNG,
/// and exposes coarse actions the screens call: new game, pick team, sim a week / to the
/// end, run the postseason, roll the offseason, save/continue.
/// </summary>
public sealed class GameManager
{
    private const int StartYear = 2026;

    private readonly PlayerGenerator _generator = new();
    private readonly EventBus _bus = new();
    private readonly MediaSubsystem _media = new(new TemplateNarrativeGenerator());
    private readonly Dictionary<string, BoxScore> _boxes = new(); // session cache of per-game box scores
    private Pcg32Rng _rng = new(0);
    private PostseasonState? _postState;

    public GameSave? Save { get; private set; }
    public PostseasonResult? Postseason { get; private set; }

    public GameManager()
    {
        // Media is an event consumer: when a week concludes, generate that week's coverage.
        _bus.Subscribe<WeekAdvanced>(e =>
        {
            if (Save is null) return;
            _media.GenerateWeek(e.Week, Save.League, Save.Season, CurrentRankings(), Save.UserTeamId, Save.Media);
        });
    }

    private static string SaveDir => ProjectSettings.GlobalizePath("user://saves/slot1");

    public bool CanContinue => SaveManager.Exists(SaveDir);
    public bool HasGame => Save is not null;
    public bool SeasonComplete => Save!.Season.IsComplete;

    public Team UserTeam => Team(Save!.UserTeamId!.Value);
    public Team Team(int id) => Save!.League.AllTeams.First(t => t.Id == id);
    public TeamRecord UserRecord => Save!.Season.Records[Save.UserTeamId!.Value];
    public MediaStore Media => Save!.Media;
    public StatBook SeasonStats => Save!.Season.Stats;
    public LeagueHistory Archive => Save!.Archive;
    public int Year => Save!.Year;
    public IReadOnlyList<Team> AllTeamsByName => Save!.League.AllTeams.OrderBy(t => t.Name).ToList();

    /// <summary>The user team's full slate (played + upcoming), in week order.</summary>
    public IEnumerable<Matchup> UserSchedule()
        => Save!.Season.Schedule.For(Save.UserTeamId!.Value).OrderBy(m => m.Week);

    /// <summary>Find the user's game result for a given week, if it has been played.</summary>
    public SeasonGameResult? UserResult(int week)
        => Save!.Season.Games.FirstOrDefault(g => g.Week == week &&
            (g.HomeId == Save.UserTeamId || g.AwayId == Save.UserTeamId));

    // --- Box scores (session cache; not persisted) ---

    private void CaptureSeasonBox(int week, int homeId, int awayId, BoxScore box)
        => _boxes[$"S:{Save!.Year}:{week}:{homeId}:{awayId}"] = box;

    private void CapturePlayoffBox(string round, int homeId, int awayId, BoxScore box)
        => _boxes[$"P:{Save!.Year}:{round}:{homeId}:{awayId}"] = box;

    public BoxScore? SeasonBox(int week, int homeId, int awayId)
        => _boxes.GetValueOrDefault($"S:{Save!.Year}:{week}:{homeId}:{awayId}");

    public BoxScore? PlayoffBox(string round, int homeId, int awayId)
        => _boxes.GetValueOrDefault($"P:{Save!.Year}:{round}:{homeId}:{awayId}");

    /// <summary>Make a player the starter at his position (moves him to the top of the depth chart).</summary>
    public void PromoteToStarter(int playerId)
    {
        List<Player> roster = UserTeam.Roster;
        Player? player = roster.FirstOrDefault(p => p.Id == playerId);
        if (player is null) return;
        roster.Remove(player);
        int insertAt = roster.FindIndex(p => p.Position == player.Position);
        roster.Insert(insertAt < 0 ? roster.Count : insertAt, player);
    }

    // --- Lifecycle ---

    public void NewGame(ulong seed)
    {
        _rng = new Pcg32Rng(seed);
        Save = SeasonCycle.NewGame(_rng, StartYear, _generator);
        Postseason = null;
    }

    public void SelectTeam(int teamId)
    {
        Save!.UserTeamId = teamId;
        StartSeason();
    }

    private void StartSeason()
    {
        Schedule schedule = ScheduleBuilder.Build(_rng, Save!.League, Save.History, Save.Year);
        Save.Season = SeasonDriver.Initialize(Save.League, schedule);
        Postseason = null;
        _postState = null;
        _boxes.Clear();
    }

    // --- Season ---

    public void SimWeek()
    {
        if (!Save!.Season.IsComplete)
            SeasonDriver.AdvanceWeek(_rng, Save.League, Save.Season, Save.History, _bus, CaptureSeasonBox);
    }

    public void SimToEndOfSeason()
    {
        while (!Save!.Season.IsComplete)
            SeasonDriver.AdvanceWeek(_rng, Save.League, Save.Season, Save.History, _bus, CaptureSeasonBox);
    }

    // --- Postseason (round by round) ---

    /// <summary>Run the conference championships and seed the CFP; the bracket is not yet played.</summary>
    public void EnterPostseason()
    {
        SeasonResult result = SeasonDriver.ToResult(Save!.League, Save.Season);
        _postState = PostseasonDriver.Begin(_rng, Save.League, Save.History, result);
        Postseason = null;
    }

    /// <summary>Simulate the next bracket round; sets <see cref="Postseason"/> when the bracket completes.</summary>
    public void AdvancePlayoffRound()
    {
        if (_postState is null || _postState.Bracket.IsComplete) return;
        PostseasonDriver.AdvanceRound(_rng, _postState, Save!.League, Save.History, CapturePlayoffBox);
        if (_postState.Bracket.IsComplete) Postseason = _postState.Result;
    }

    public BracketState? Bracket => _postState?.Bracket;
    public IReadOnlyList<ChampionshipGameResult> ConferenceTitleGames => _postState?.ChampionshipGames ?? new List<ChampionshipGameResult>();
    public bool PlayoffComplete => _postState?.Bracket.IsComplete ?? false;

    public void StartNextSeason()
    {
        int nextPlayerId = Save!.NextPlayerId;
        OffseasonStage.Run(_rng, Save.League, Save.Archive, Save.Year, Postseason!, ref nextPlayerId, _generator);
        Save.NextPlayerId = nextPlayerId;
        Save.Year++;
        StartSeason();
    }

    // --- Views (computed for the UI) ---

    public List<RankedTeam> CurrentRankings()
        => RankingService.Rank(Save!.League, Save.Season.Records, Save.Season.Games);

    public List<TeamRecord> UserConferenceStandings()
    {
        Conference? conf = Save!.League.ConferenceOf(UserTeam);
        return conf is null
            ? new List<TeamRecord>()
            : StandingsService.ConferenceStandings(Save.League, conf, Save.Season.Records, Save.Season.Games);
    }

    /// <summary>The user team's games so far this season (most recent first).</summary>
    public IEnumerable<SeasonGameResult> UserResults()
        => Save!.Season.Games.Where(g => g.HomeId == Save.UserTeamId || g.AwayId == Save.UserTeamId)
                             .OrderByDescending(g => g.Week);

    // --- Generic detail accessors (for drill-down views) ---

    public TeamRecord RecordOf(int teamId) => Save!.Season.Records[teamId];
    public Player? PlayerById(int playerId) => Save!.League.AllTeams.SelectMany(t => t.Roster).FirstOrDefault(p => p.Id == playerId);
    public List<TeamRecord> ConferenceStandings(Conference conference)
        => StandingsService.ConferenceStandings(Save!.League, conference, Save.Season.Records, Save.Season.Games);
    public Conference? ConferenceOf(int teamId) => Save!.League.ConferenceOf(Team(teamId));
    public Conference? ConferenceById(int conferenceId) => Save!.League.Conferences.FirstOrDefault(c => c.Id == conferenceId);
    public IEnumerable<Matchup> ScheduleOf(int teamId) => Save!.Season.Schedule.For(teamId).OrderBy(m => m.Week);
    public PlayerStatLine? PlayerStat(int playerId) => Save!.Season.Stats.Players.GetValueOrDefault(playerId);

    public SeasonGameResult? ResultFor(int teamId, int week)
        => Save!.Season.Games.FirstOrDefault(g => g.Week == week && (g.HomeId == teamId || g.AwayId == teamId));

    private List<RankedTeam>? _rankCache;
    private int _rankCacheGames = -1;

    /// <summary>Poll rank (1–25) for a team, or 0 if unranked. Cached per render until the next game.</summary>
    public int RankOf(int teamId)
    {
        int games = Save!.Season.Games.Count;
        if (_rankCache is null || _rankCacheGames != games)
        {
            _rankCache = CurrentRankings();
            _rankCacheGames = games;
        }
        int idx = _rankCache.FindIndex(r => r.TeamId == teamId);
        return idx >= 0 && idx < 25 ? idx + 1 : 0;
    }

    /// <summary>"#7 Texas Tech" when ranked, otherwise "Texas Tech".</summary>
    public string RankedName(int teamId)
    {
        int rank = RankOf(teamId);
        string name = Team(teamId).Name;
        return rank > 0 ? $"#{rank} {name}" : name;
    }

    public Matchup? NextOpponent(int teamId)
        => ScheduleOf(teamId).FirstOrDefault(m => ResultFor(teamId, m.Week) is null);

    /// <summary>(homeWins, homeLosses, awayWins, awayLosses) so far this season.</summary>
    public (int HW, int HL, int AW, int AL) HomeAwayRecord(int teamId)
    {
        int hw = 0, hl = 0, aw = 0, al = 0;
        foreach (SeasonGameResult g in Save!.Season.Games.Where(g => g.HomeId == teamId || g.AwayId == teamId))
        {
            bool home = g.HomeId == teamId;
            bool won = g.WinnerId == teamId;
            if (home) { if (won) hw++; else hl++; }
            else { if (won) aw++; else al++; }
        }
        return (hw, hl, aw, al);
    }

    // --- Persistence ---

    public void SaveGame()
    {
        Save!.Rng = _rng.Snapshot();
        SaveManager.Save(SaveDir, Save);
    }

    public void Continue()
    {
        Save = SaveManager.Load(SaveDir);
        _rng = Pcg32Rng.Restore(Save.Rng);
        Postseason = null;
    }
}
