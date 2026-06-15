using CfbSim.Core.Model;
using CfbSim.Core.Rng;
using CfbSim.Core.Sim.Game;

namespace CfbSim.Core.Sim.Postseason;

/// <summary>Plays a single neutral-site game (no home-field advantage is modeled yet).</summary>
internal static class NeutralGame
{
    public static (int WinnerId, int HomeScore, int AwayScore) Play(IRng rng, Team home, Team away)
    {
        GameResult g = GameSimulator.Simulate(rng, home, away);
        int winner = g.HomeScore >= g.AwayScore ? home.Id : away.Id;
        return (winner, g.HomeScore, g.AwayScore);
    }
}
