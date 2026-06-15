namespace CfbSim.Core.Sim.Game;

/// <summary>Field-position helpers. Ball position is yards from the offense's own goal (0..100).</summary>
public static class Field
{
    public static string Describe(int ballOn)
    {
        if (ballOn == 50) return "midfield";
        return ballOn < 50 ? $"own {ballOn}" : $"opp {100 - ballOn}";
    }
}
