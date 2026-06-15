using CfbSim.Core.Model;

namespace CfbSim.Core.Stats;

/// <summary>Full statistical record of a game — team totals plus every player's line.</summary>
public sealed class BoxScore
{
    public required int HomeTeamId { get; init; }
    public required int AwayTeamId { get; init; }
    public required string HomeName { get; init; }
    public required string AwayName { get; init; }

    public TeamStatLine Home { get; } = new();
    public TeamStatLine Away { get; } = new();

    private readonly Dictionary<int, PlayerStatLine> _players = new();

    public TeamStatLine TeamOf(int teamId) => teamId == HomeTeamId ? Home : Away;

    public PlayerStatLine For(Player player, int teamId)
    {
        if (!_players.TryGetValue(player.Id, out PlayerStatLine? line))
        {
            line = new PlayerStatLine
            {
                PlayerId = player.Id,
                Name = player.Name,
                Position = player.Position,
                TeamId = teamId,
            };
            _players[player.Id] = line;
        }
        return line;
    }

    public IEnumerable<PlayerStatLine> Players => _players.Values;
    public IEnumerable<PlayerStatLine> PlayersOf(int teamId) => _players.Values.Where(p => p.TeamId == teamId);
}
