using CfbSim.Core.Stats;

namespace CfbSim.Core.Sim.Season;

/// <summary>
/// Season-long player statistics, accumulated game by game from each game's box score.
/// Lives in the season-state sidecar; powers the Stats views and leaderboards.
/// </summary>
public sealed class StatBook
{
    public Dictionary<int, PlayerStatLine> Players { get; init; } = new();

    public void Accumulate(BoxScore box)
    {
        foreach (PlayerStatLine line in box.Players)
        {
            if (!Players.TryGetValue(line.PlayerId, out PlayerStatLine? season))
            {
                season = new PlayerStatLine
                {
                    PlayerId = line.PlayerId,
                    Name = line.Name,
                    Position = line.Position,
                    TeamId = line.TeamId,
                };
                Players[line.PlayerId] = season;
            }

            season.RushAtt += line.RushAtt; season.RushYds += line.RushYds; season.RushTD += line.RushTD;
            season.Targets += line.Targets; season.Rec += line.Rec; season.RecYds += line.RecYds; season.RecTD += line.RecTD;
            season.PassAtt += line.PassAtt; season.PassComp += line.PassComp; season.PassYds += line.PassYds;
            season.PassTD += line.PassTD; season.PassInt += line.PassInt;
            season.Sacks += line.Sacks; season.Interceptions += line.Interceptions;
        }
    }

    public IEnumerable<PlayerStatLine> Leaders(Func<PlayerStatLine, int> by, int count, int? teamId = null)
        => Players.Values
            .Where(p => (teamId is null || p.TeamId == teamId) && by(p) > 0)
            .OrderByDescending(by)
            .Take(count);
}
