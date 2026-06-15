using CfbSim.Core.Events;
using CfbSim.Core.Generation;
using CfbSim.Core.Media;
using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Save;
using CfbSim.Core.Services;
using CfbSim.Core.Sim.Game;
using CfbSim.Core.Sim.Offseason;
using CfbSim.Core.Sim.Postseason;
using CfbSim.Core.Sim.Season;
using CfbSim.Core.Stats;

// Demo runner.
//   dotnet run --project samples/CfbSim.Demo -- league       → print the FBS alignment
//   dotnet run --project samples/CfbSim.Demo -- season [seed]→ simulate a full regular season
//   dotnet run --project samples/CfbSim.Demo -- [seed]       → simulate one game

if (args.Length > 0 && args[0].Equals("league", StringComparison.OrdinalIgnoreCase))
{
    PrintLeague();
    return;
}

if (args.Length > 0 && args[0].Equals("season", StringComparison.OrdinalIgnoreCase))
{
    ulong sseed = args.Length > 1 && ulong.TryParse(args[1], out ulong ss) ? ss : 2026UL;
    RunSeason(sseed);
    return;
}

if (args.Length > 0 && args[0].Equals("save", StringComparison.OrdinalIgnoreCase))
{
    RunSaveDemo();
    return;
}

if (args.Length > 0 && args[0].Equals("multi", StringComparison.OrdinalIgnoreCase))
{
    int years = args.Length > 1 && int.TryParse(args[1], out int y) ? y : 5;
    RunMulti(years);
    return;
}

if (args.Length > 0 && args[0].Equals("media", StringComparison.OrdinalIgnoreCase))
{
    RunMedia();
    return;
}

static void RunMedia()
{
    var rng = new Pcg32Rng(2026);
    League league = LeagueBuilder.Build(rng);
    SeriesHistory history = SeriesHistory.SeededFor(league);
    Schedule schedule = ScheduleBuilder.Build(rng, league, history, 2026);
    SeasonState state = SeasonDriver.Initialize(league, schedule);
    var media = new MediaStore();
    var subsystem = new MediaSubsystem(new TemplateNarrativeGenerator());
    int userId = league.FindTeam("Georgia")!.Id;

    for (int w = 1; w <= 4; w++)
    {
        SeasonDriver.AdvanceWeek(rng, league, state, history);
        var rankings = RankingService.Rank(league, state.Records, state.Games);
        subsystem.GenerateWeek(w, league, state, rankings, userId, media);
    }

    Console.WriteLine("=== Week 1 — Featured ===");
    foreach (NewsArticle a in media.Featured(2026, 1).Take(8))
        Console.WriteLine($"  ★ {a.Headline}");

    NewsArticle story = media.Featured(2026, 1).First();
    Console.WriteLine($"\nStory:\n  {story.Headline}\n  {story.Body}");

    Console.WriteLine($"\nGeorgia coverage ({media.ForTeam(userId).Count()} articles):");
    foreach (NewsArticle a in media.ForTeam(userId).Take(5))
        Console.WriteLine($"  [Wk{a.Week}] {a.Headline}");
}

static void RunMulti(int years)
{
    var rng = new Pcg32Rng(2026);
    var gen = new PlayerGenerator();
    GameSave save = SeasonCycle.NewGame(rng, 2026, gen);
    var teams = save.League.AllTeams.ToDictionary(t => t.Id); // team identities are stable across years

    Console.WriteLine($"Simulating {years} seasons with offseason between each...\n");
    for (int i = 0; i < years; i++)
    {
        int year = save.Year;
        PostseasonResult post = SeasonCycle.RunFullYear(rng, save, gen);
        Console.WriteLine($"  {year}: National Champion — {teams[post.NationalChampionId].Name}");
    }

    Console.WriteLine("\nTitles won (top 5):");
    foreach (var grp in save.Archive.Seasons.GroupBy(s => s.NationalChampionId)
                 .OrderByDescending(g => g.Count()).Take(5))
        Console.WriteLine($"  {teams[grp.Key].Name,-18} {grp.Count()}");

    Team uga = save.League.FindTeam("Georgia")!;
    var byClass = uga.Roster.GroupBy(p => p.Class).OrderBy(g => g.Key)
        .Select(g => $"{g.Key}:{g.Count()}");
    Console.WriteLine($"\nGeorgia roster after {years} offseasons: {uga.Roster.Count} players ({string.Join(", ", byClass)})");
    Console.WriteLine($"Georgia titles in this run: {save.Archive.TitlesFor(uga.Id)}");
}

static void RunSaveDemo()
{
    string dir = Path.Combine(Path.GetTempPath(), "cfbsim_demo_save");
    var rng = new Pcg32Rng(2026);
    League league = LeagueBuilder.Build(rng);
    SeriesHistory history = SeriesHistory.SeededFor(league);
    Schedule schedule = ScheduleBuilder.Build(rng, league, history, year: 2026);
    SeasonState state = SeasonDriver.Initialize(league, schedule);

    for (int i = 0; i < 6; i++) SeasonDriver.AdvanceWeek(rng, league, state, history);
    Console.WriteLine($"Played {state.NextWeek - 1} weeks. Saving to:\n  {dir}\n");

    SaveManager.Save(dir, new GameSave
    {
        Year = 2026,
        UserTeamId = league.FindTeam("Georgia")!.Id,
        League = league,
        History = history,
        Season = state,
        Rng = rng.Snapshot(),
    });
    foreach (string f in Directory.GetFiles(dir).Where(f => !f.EndsWith(".tmp")).OrderBy(f => f))
        Console.WriteLine($"  {Path.GetFileName(f),-14} {new FileInfo(f).Length / 1024.0,7:0.0} KB");

    Console.WriteLine("\nQuit & reload, then finish the season + postseason from the save...");
    GameSave loaded = SaveManager.Load(dir);
    var rng2 = Pcg32Rng.Restore(loaded.Rng);
    while (!loaded.Season.IsComplete) SeasonDriver.AdvanceWeek(rng2, loaded.League, loaded.Season, loaded.History);

    SeasonResult result = SeasonDriver.ToResult(loaded.League, loaded.Season);
    PostseasonResult post = PostseasonSimulator.Run(rng2, loaded.League, loaded.History, result);
    var teams = loaded.League.AllTeams.ToDictionary(t => t.Id);
    Console.WriteLine($"\nResumed save → NATIONAL CHAMPION: {teams[post.NationalChampionId].Name}");
    Console.WriteLine($"(User team: {teams[loaded.UserTeamId!.Value].Name})");
}

ulong seed = args.Length > 0 && ulong.TryParse(args[0], out ulong s) ? s : 2026UL;
RunGame(seed);

static void RunSeason(ulong seed)
{
    var rng = new Pcg32Rng(seed);
    League league = LeagueBuilder.Build(rng);
    SeriesHistory history = SeriesHistory.SeededFor(league);
    Schedule schedule = ScheduleBuilder.Build(rng, league, history, year: 2026);
    SeasonResult result = SeasonSimulator.Run(rng, league, schedule, history);
    var teams = league.AllTeams.ToDictionary(t => t.Id);

    Console.WriteLine($"=== {result.Year} season: {result.Games.Count} games over {schedule.Weeks} weeks (seed {seed}) ===\n");

    Console.WriteLine("Final Top 25:");
    foreach (RankedTeam rt in result.Top25)
    {
        Team t = teams[rt.TeamId];
        Console.WriteLine($"  {rt.Rank,2}. {t.Name,-18} {rt.Record.Wins}-{rt.Record.Losses}");
    }

    // ---- Postseason ----
    PostseasonResult post = PostseasonSimulator.Run(rng, league, history, result);

    Console.WriteLine("\nConference champions:");
    foreach (Conference c in league.Conferences.Where(c => c.IsPower))
        if (post.ConferenceChampions.TryGetValue(c.Id, out int champId))
            Console.WriteLine($"  {c.Abbreviation,-4} {teams[champId].Name}");

    Console.WriteLine("\nCFP field (12):");
    foreach (CfpSeed s in post.CfpField)
        Console.WriteLine($"  {s.Seed,2}. {teams[s.TeamId].Name,-18} {(s.ConferenceChampion ? "(conf champ)" : "")}");

    Console.WriteLine("\nCFP bracket:");
    foreach (var round in post.CfpGames.GroupBy(g => g.Round))
    {
        Console.WriteLine($"  {round.Key}:");
        foreach (CfpGameResult g in round)
            Console.WriteLine($"     {teams[g.AwayId].Abbreviation} {g.AwayScore} @ {teams[g.HomeId].Abbreviation} {g.HomeScore}  → {teams[g.WinnerId].Abbreviation}");
    }

    Console.WriteLine($"\n*** NATIONAL CHAMPION: {teams[post.NationalChampionId].Name} ***");

    Console.WriteLine("\nFinal Top 10 (after postseason):");
    foreach (RankedTeam rt in post.FinalRankings.Take(10))
        Console.WriteLine($"  {rt.Rank,2}. {teams[rt.TeamId].Name,-18} {rt.Record.Wins}-{rt.Record.Losses}");
}

static void PrintLeague()
{
    League league = LeagueBuilder.Build(new Pcg32Rng(1));
    int teams = league.AllTeams.Count();
    Console.WriteLine($"=== FBS: {league.Conferences.Count} conferences, {teams} teams ===\n");
    foreach (Conference c in league.Conferences)
    {
        Console.WriteLine($"{c.Abbreviation,-5} {c.Name} ({(c.IsPower ? "P4" : "G5")}) — {c.Teams.Count} teams");
        foreach (Team t in c.Teams.OrderByDescending(t => t.Prestige))
            Console.WriteLine($"     {t.Prestige,3}  {t.Abbreviation,-5} {t.Name}");
        Console.WriteLine();
    }
    Console.WriteLine($"IND   Independents — {league.Independents.Count} teams");
    foreach (Team t in league.Independents)
        Console.WriteLine($"     {t.Prestige,3}  {t.Abbreviation,-5} {t.Name}");
}

static void RunGame(ulong seed)
{
    var rng = new Pcg32Rng(seed);
    League league = LeagueBuilder.Build(rng);
    Team home = league.FindTeam("Georgia")!;
    Team away = league.FindTeam("Alabama")!;

    var bus = new EventBus();
    bus.Subscribe<GameConcluded>(e =>
        Console.WriteLine($"[event] GameConcluded: {e.AwayName} {e.AwayScore} @ {e.HomeName} {e.HomeScore}"));

    GameResult game = GameSimulator.Simulate(rng, home, away, bus);

    Console.WriteLine($"=== {away.Name} at {home.Name} (seed {seed}) ===");
    Console.WriteLine($"FINAL: {away.Abbreviation} {game.AwayScore}, {home.Abbreviation} {game.HomeScore}" +
                      (game.Overtimes > 0 ? $" ({game.Overtimes}OT)" : ""));
    Console.WriteLine();
    Console.WriteLine("Scoring:");
    foreach (string line in game.ScoringSummary) Console.WriteLine("  " + line);
    Console.WriteLine();
    PrintTeam(game.Box, home);
    PrintTeam(game.Box, away);
}

static void PrintTeam(BoxScore box, Team team)
{
    TeamStatLine t = box.TeamOf(team.Id);
    Console.WriteLine($"--- {team.Name} ({t.Points}) — {t.TotalYards} yds, {t.FirstDowns} 1st downs, " +
                      $"{t.RushYds} rush / {t.PassYds} pass, {t.Turnovers} TO ---");
    var players = box.PlayersOf(team.Id).ToList();
    foreach (var qb in players.Where(p => p.PassAtt > 0).OrderByDescending(p => p.PassYds))
        Console.WriteLine($"    PASS {qb.Name,-20} {qb.PassComp}/{qb.PassAtt}, {qb.PassYds} yds, {qb.PassTD} TD, {qb.PassInt} INT");
    foreach (var rb in players.Where(p => p.RushAtt > 0).OrderByDescending(p => p.RushYds).Take(2))
        Console.WriteLine($"    RUSH {rb.Name,-20} {rb.RushAtt} att, {rb.RushYds} yds, {rb.RushTD} TD");
    foreach (var wr in players.Where(p => p.Rec > 0).OrderByDescending(p => p.RecYds).Take(3))
        Console.WriteLine($"    REC  {wr.Name,-20} {wr.Rec} rec, {wr.RecYds} yds, {wr.RecTD} TD");
    Console.WriteLine();
}
