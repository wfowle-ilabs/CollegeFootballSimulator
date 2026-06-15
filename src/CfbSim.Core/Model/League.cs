using System.Text.Json.Serialization;

namespace CfbSim.Core.Model;

/// <summary>The FBS universe for v1: all conferences plus independents.</summary>
public sealed class League
{
    public required string Name { get; init; }

    public List<Conference> Conferences { get; } = new();
    public List<Team> Independents { get; } = new();

    [JsonIgnore]
    public IEnumerable<Team> AllTeams =>
        Conferences.SelectMany(c => c.Teams).Concat(Independents);

    public Conference? ConferenceOf(Team team) =>
        Conferences.FirstOrDefault(c => c.Id == team.ConferenceId);

    public Team? FindTeam(string nameOrAbbr) =>
        AllTeams.FirstOrDefault(t =>
            t.Name.Equals(nameOrAbbr, StringComparison.OrdinalIgnoreCase) ||
            t.Abbreviation.Equals(nameOrAbbr, StringComparison.OrdinalIgnoreCase));
}
