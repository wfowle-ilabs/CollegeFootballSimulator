using CfbSim.Core.Sim.Season;

namespace CfbSim.Core.Sim.Postseason;

/// <summary>Result of a conference championship game.</summary>
public sealed record ChampionshipGameResult(
    int ConferenceId, string ConferenceAbbr,
    int WinnerId, int LoserId, int WinnerScore, int LoserScore);

/// <summary>A team's seed in the 12-team CFP field.</summary>
public sealed record CfpSeed(int Seed, int TeamId, bool ConferenceChampion);

/// <summary>One CFP bracket game.</summary>
public sealed record CfpGameResult(
    string Round, int HomeId, int AwayId, int HomeScore, int AwayScore, int WinnerId);

/// <summary>A (non-CFP) bowl game.</summary>
public sealed record BowlResult(
    string Name, int HomeId, int AwayId, int HomeScore, int AwayScore, int WinnerId);

/// <summary>The full postseason: championship games, the CFP field/bracket, bowls, and the champion.</summary>
public sealed class PostseasonResult
{
    public required int Year { get; init; }

    public List<ChampionshipGameResult> ChampionshipGames { get; } = new();
    public Dictionary<int, int> ConferenceChampions { get; set; } = new(); // conferenceId → teamId

    public List<RankedTeam> SelectionRankings { get; } = new(); // after championship games — seeds the CFP
    public List<RankedTeam> FinalRankings { get; } = new();      // after the whole postseason — champion is #1
    public List<CfpSeed> CfpField { get; } = new();
    public List<CfpGameResult> CfpGames { get; } = new();
    public List<BowlResult> Bowls { get; } = new();

    public int NationalChampionId { get; set; }
}

/// <summary>One slot in the bracket. TeamA is the home/better seed; filled as rounds complete.</summary>
public sealed class BracketGame
{
    public required string Round { get; init; }
    public int SeedA { get; set; }
    public int SeedB { get; set; }
    public int TeamA { get; set; } = -1;
    public int TeamB { get; set; } = -1;
    public CfpGameResult? Result { get; set; }

    public bool ReadyToPlay => TeamA != -1 && TeamB != -1 && Result is null;
    public int WinnerId => Result?.WinnerId ?? -1;
    public int WinnerSeed => Result is null ? 0 : (Result.WinnerId == TeamA ? SeedA : SeedB);
}

/// <summary>The live 12-team bracket: rounds fill in as they're played (for the bracket view).</summary>
public sealed class BracketState
{
    public List<CfpSeed> Field { get; init; } = new();
    public List<BracketGame> FirstRound { get; init; } = new();
    public List<BracketGame> Quarterfinals { get; init; } = new();
    public List<BracketGame> Semifinals { get; init; } = new();
    public List<BracketGame> Championship { get; init; } = new();
    public int NextRound { get; set; } = 1; // 1=FirstRound .. 4=Championship; 5 = done
    public int ChampionId { get; set; } = -1;

    public bool IsComplete => NextRound > 4;
    public IEnumerable<BracketGame> AllGames =>
        FirstRound.Concat(Quarterfinals).Concat(Semifinals).Concat(Championship);
}

/// <summary>Transient (in-memory) state for running the postseason round by round in the UI.</summary>
public sealed class PostseasonState
{
    public required int Year { get; init; }
    public Dictionary<int, int> Champions { get; init; } = new();
    public List<ChampionshipGameResult> ChampionshipGames { get; init; } = new();
    public List<RankedTeam> SelectionRankings { get; init; } = new();
    public List<SeasonGameResult> SeedingGames { get; init; } = new();
    public Dictionary<int, TeamRecord> Records { get; init; } = new();
    public BracketState Bracket { get; init; } = new();
    public PostseasonResult? Result { get; set; }
}
