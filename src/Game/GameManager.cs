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
    private bool _dirty;

    public GameSave? Save { get; private set; }
    public PostseasonResult? Postseason { get; private set; }

    /// <summary>True if the game state has advanced/changed since the last save.</summary>
    public bool HasUnsavedChanges => _dirty;

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

    // --- Schedule / calendar views ---

    /// <summary>The day cursor — the calendar day the user is currently sitting on.</summary>
    public DateOnly CurrentDate => Save!.Season.CurrentDate == default
        ? SeasonCalendar.OpeningSunday(Year) : Save.Season.CurrentDate;

    /// <summary>The calendar week the cursor is in (clamped to the final week once complete).</summary>
    public int CurrentWeek => Save!.Season.IsComplete
        ? Math.Max(1, Save.Season.Schedule.Weeks)
        : Math.Clamp(SeasonCalendar.WeekOf(Year, CurrentDate), 1, Math.Max(1, Save.Season.Schedule.Weeks));
    public int TotalWeeks => Save!.Season.Schedule.Weeks;

    /// <summary>Every game in a given week, ordered by kickoff.</summary>
    public IEnumerable<Matchup> WeekGames(int week)
        => Save!.Season.Schedule.InWeek(week).OrderBy(m => m.Kickoff).ThenBy(m => m.HomeId);

    /// <summary>Every game kicking off on a given calendar date, ordered by kickoff time.</summary>
    public IEnumerable<Matchup> GamesOn(DateOnly date)
        => Save!.Season.Schedule.Games
            .Where(m => m.Kickoff != default && DateOnly.FromDateTime(m.Kickoff) == date)
            .OrderBy(m => m.Kickoff).ThenBy(m => m.HomeId);

    /// <summary>The user team's game in a given week (null on a bye).</summary>
    public Matchup? UserGameInWeek(int week)
        => Save!.Season.Schedule.For(Save.UserTeamId!.Value).FirstOrDefault(m => m.Week == week);

    /// <summary>The user team's next unplayed game (the spotlight matchup), or null if the slate's done.</summary>
    public Matchup? UserNextGame() => NextOpponent(Save!.UserTeamId!.Value);

    /// <summary>The marquee national game of a week — highest combined prestige (+ rivalry bump).</summary>
    public Matchup? GameOfTheWeek(int week)
        => Save!.Season.Schedule.InWeek(week)
            .OrderByDescending(Appeal).ThenBy(m => m.HomeId).ThenBy(m => m.AwayId)
            .FirstOrDefault();

    private int Appeal(Matchup m) => Team(m.HomeId).Prestige + Team(m.AwayId).Prestige + (m.Rivalry ? 30 : 0);

    /// <summary>The most recent news articles across the season (full stories first), for the headlines strip.</summary>
    public IEnumerable<NewsArticle> LatestHeadlines(int n)
        => Save!.Media.Articles
            .OrderByDescending(a => a.Week).ThenByDescending(a => a.Full).ThenByDescending(a => a.Id)
            .Take(n);

    /// <summary>The named rivalries a team is part of: (opponent id, rivalry name).</summary>
    public IEnumerable<(int OppId, string Name)> RivalriesOf(int teamId)
        => Save!.History.All
            .Where(r => r.IsRivalry && (r.TeamAId == teamId || r.TeamBId == teamId))
            .Select(r => (r.TeamAId == teamId ? r.TeamBId : r.TeamAId, r.RivalryName ?? "Rivalry"));

    /// <summary>How many conference titles a team holds in the historical archive.</summary>
    public int ConferenceTitlesFor(int teamId)
        => Save!.Archive.Seasons.Count(s => s.ConferenceChampions.Values.Contains(teamId));

    /// <summary>The current head-to-head series record between two teams (wins for <paramref name="teamId"/>).</summary>
    public (int Wins, int Losses) SeriesVs(int teamId, int oppId)
    {
        int a = Math.Min(teamId, oppId), b = Math.Max(teamId, oppId);
        SeriesRecord? s = Save!.History.All.FirstOrDefault(r => r.TeamAId == a && r.TeamBId == b);
        if (s is null) return (0, 0);
        bool isA = s.TeamAId == teamId;
        return (isA ? s.TeamAWins : s.TeamBWins, isA ? s.TeamBWins : s.TeamAWins);
    }

    /// <summary>The user team's current win/loss streak, e.g. "W3" or "L1" (empty before any games).</summary>
    public string UserStreak() => StreakOf(Save!.UserTeamId!.Value);

    /// <summary>A team's current win/loss streak, e.g. "W3" or "L1" (empty before any games).</summary>
    public string StreakOf(int teamId)
    {
        var games = Save!.Season.Games
            .Where(g => g.HomeId == teamId || g.AwayId == teamId)
            .OrderByDescending(g => g.Week).ToList();
        if (games.Count == 0) return "";
        bool won = games[0].WinnerId == teamId;
        int n = 0;
        foreach (SeasonGameResult g in games)
        {
            if ((g.WinnerId == teamId) != won) break;
            n++;
        }
        return $"{(won ? "W" : "L")}{n}";
    }

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

    // --- Training slots ---

    /// <summary>The activity assigned to a day's slot, if any.</summary>
    public TrainingActivity? TrainingFor(DateOnly date, TimeSlot slot)
        => Save!.Season.Training.TryGetValue(TrainingKey.Of(date, slot), out TrainingActivity a) ? a : null;

    /// <summary>Assign (or clear, with null) the training activity for a day's slot.</summary>
    public void SetTraining(DateOnly date, TimeSlot slot, TrainingActivity? activity)
    {
        string key = TrainingKey.Of(date, slot);
        if (activity is null) Save!.Season.Training.Remove(key);
        else Save!.Season.Training[key] = activity.Value;
        _dirty = true;
    }

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
        _dirty = true;
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

    private void Day()
    {
        SeasonDriver.AdvanceDay(_rng, Save!.League, Save.Season, Save.History, _bus, CaptureSeasonBox, Save.UserTeamId);
        _dirty = true;
    }

    /// <summary>The training-prep boost a team carries into its next game (user plan or AI).</summary>
    public TeamBoost BoostFor(int teamId)
    {
        int week = NextOpponent(teamId)?.Week ?? CurrentWeek;
        return TrainingBoosts.ForGame(Save!.Season, teamId, week, Save.UserTeamId);
    }

    /// <summary>Sim forward to (and including) a target calendar date.</summary>
    public void SimToDate(DateOnly target)
    {
        while (!Save!.Season.IsComplete && Save.Season.CurrentDate < target) Day();
    }

    /// <summary>Advance the calendar one day, simming that day's games.</summary>
    public void AdvanceDay()
    {
        if (!Save!.Season.IsComplete) Day();
    }

    /// <summary>Sim through the rest of the current calendar week (stops on its Saturday).</summary>
    public void SimToEndOfWeek()
    {
        if (Save!.Season.IsComplete) return;
        DateOnly target = NextSaturdayAfter(Save.Season.CurrentDate);
        while (!Save.Season.IsComplete && Save.Season.CurrentDate < target) Day();
    }

    /// <summary>Sim forward until the user team's next game has been played.</summary>
    public void SimToMyNextGame()
    {
        if (Save!.Season.IsComplete) return;
        Matchup? g = UserNextGame();
        if (g is null) return;
        int userId = Save.UserTeamId!.Value;
        while (!Save.Season.IsComplete && ResultFor(userId, g.Week) is null) Day();
    }

    /// <summary>Legacy alias kept for callers: sim to the end of the current week.</summary>
    public void SimWeek() => SimToEndOfWeek();

    public void SimToEndOfSeason()
    {
        while (!Save!.Season.IsComplete) Day();
    }

    private static DateOnly NextSaturdayAfter(DateOnly d)
    {
        DateOnly n = d.AddDays(1);
        while (n.DayOfWeek != DayOfWeek.Saturday) n = n.AddDays(1);
        return n;
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
        _dirty = true;
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
        _dirty = false;
    }

    public void Continue()
    {
        Save = SaveManager.Load(SaveDir);
        _rng = Pcg32Rng.Restore(Save.Rng);
        Postseason = null;
        _dirty = false;
    }
}
