namespace CfbSim.Core.Sim.Game;

/// <summary>Game clock as total elapsed seconds. Two-minute/timeout logic is deferred (M2 open question).</summary>
public sealed class GameClock
{
    public const int QuarterSeconds = 900;
    public const int HalfSeconds = 1800;
    public const int RegulationSeconds = 3600;

    public int Elapsed { get; private set; }

    public void Advance(int seconds) => Elapsed += seconds;

    public int Quarter => Math.Clamp(Elapsed / QuarterSeconds + 1, 1, 4);
    public bool RegulationOver => Elapsed >= RegulationSeconds;
    public bool PastHalf => Elapsed >= HalfSeconds;

    public int SecondsLeftInHalf =>
        Elapsed < HalfSeconds ? HalfSeconds - Elapsed : Math.Max(0, RegulationSeconds - Elapsed);

    /// <summary>"Q3 7:24" style label for play logs.</summary>
    public string Label()
    {
        int inQuarter = Elapsed % QuarterSeconds;
        int remaining = QuarterSeconds - inQuarter;
        if (Elapsed >= RegulationSeconds) return "Q4 0:00";
        return $"Q{Quarter} {remaining / 60}:{remaining % 60:00}";
    }
}
