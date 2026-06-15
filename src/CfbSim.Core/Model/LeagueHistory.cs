namespace CfbSim.Core.Model;

/// <summary>A team's final placement in a completed season.</summary>
public sealed record SeasonFinish(int Rank, int TeamId, int Wins, int Losses);

/// <summary>An immutable snapshot of one completed season (for the history archive).</summary>
public sealed class SeasonSummary
{
    public required int Year { get; init; }
    public required int NationalChampionId { get; init; }
    public Dictionary<int, int> ConferenceChampions { get; init; } = new(); // conferenceId → teamId
    public List<SeasonFinish> Finishes { get; init; } = new();              // all teams, by final rank
}

/// <summary>
/// The league's season-by-season archive (the historical-state sidecar). Accumulates a
/// <see cref="SeasonSummary"/> per year so any school's past results, finishes, and titles
/// are queryable without touching the live league. Loads independently — references teams by ID.
/// </summary>
public sealed class LeagueHistory
{
    public List<SeasonSummary> Seasons { get; } = new();

    public int TitlesFor(int teamId) => Seasons.Count(s => s.NationalChampionId == teamId);

    public IEnumerable<SeasonFinish> FinishesFor(int teamId)
        => Seasons.Select(s => s.Finishes.FirstOrDefault(f => f.TeamId == teamId)).Where(f => f is not null)!;
}
