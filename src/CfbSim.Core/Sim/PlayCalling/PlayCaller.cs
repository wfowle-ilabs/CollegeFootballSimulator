using CfbSim.Core.Rng;
using CfbSim.Core.Sim.Play;

namespace CfbSim.Core.Sim.PlayCalling;

/// <summary>The game situation a play is called in (offense's perspective).</summary>
public readonly record struct PlaySituation(
    int Down,
    int Distance,
    int BallOn,                 // yards from the offense's own goal (0..100)
    int ScoreDiff,              // offense score minus defense score
    int SecondsLeftInHalf,
    bool SecondHalf)
{
    public int YardsToGoal => 100 - BallOn;
}

/// <summary>
/// A lightweight situational AI. Offense weighs run/pass/kick by down, distance,
/// field position and game state; defense keys run or pass. Tendencies will later
/// be driven by coach attributes — for now `passBias` is a tunable knob.
/// </summary>
public static class PlayCaller
{
    public static PlayType ChooseOffense(IRng rng, PlaySituation s, double passBias = 0.46)
    {
        int fgDistance = s.YardsToGoal + 17;
        bool trailingLate = s.ScoreDiff < 0 && s.SecondHalf && s.SecondsLeftInHalf < 300;

        if (s.Down == 4)
        {
            if (s.Distance <= 2 && (s.BallOn >= 60 || trailingLate)) return RunChoice(rng);
            if (fgDistance <= 52) return PlayType.FieldGoal;
            if (trailingLate && s.YardsToGoal <= 45)
                return s.Distance <= 4 ? RunChoice(rng) : PassChoice(rng, s, mustPass: true);
            return PlayType.Punt;
        }

        double pPass = passBias;
        if (s.Distance >= 8) pPass += 0.25;
        else if (s.Distance <= 3) pPass -= 0.25;
        if (s.Down == 3 && s.Distance >= 5) pPass += 0.30;
        if (s.Down == 3 && s.Distance <= 2) pPass -= 0.20;
        if (trailingLate) pPass += 0.20;
        if (s.YardsToGoal <= 3) pPass -= 0.25; // goal line: pound it
        pPass = Math.Clamp(pPass, 0.05, 0.95);

        return rng.NextDouble() < pPass ? PassChoice(rng, s, mustPass: false) : RunChoice(rng);
    }

    public static DefensiveKey ChooseDefense(IRng rng, PlaySituation s)
    {
        double stopRun = 0.33;
        double stopPass = 0.33;
        if (s.Distance <= 3) stopRun += 0.22;
        if (s.Distance >= 8 || (s.Down == 3 && s.Distance >= 5)) stopPass += 0.27;

        double r = rng.NextDouble();
        if (r < stopRun) return DefensiveKey.StopRun;
        if (r < stopRun + stopPass) return DefensiveKey.StopPass;
        return DefensiveKey.Balanced;
    }

    private static PlayType RunChoice(IRng rng)
        => rng.NextDouble() < 0.35 ? PlayType.OutsideRun : PlayType.InsideRun;

    private static PlayType PassChoice(IRng rng, PlaySituation s, bool mustPass)
    {
        double pDeep = 0.18;
        if (s.Distance >= 10) pDeep += 0.15;
        if (mustPass) pDeep += 0.10;
        if (s.YardsToGoal <= 12) pDeep -= 0.12; // not much field to throw deep into
        return rng.NextDouble() < Math.Clamp(pDeep, 0.03, 0.6) ? PlayType.DeepPass : PlayType.ShortPass;
    }
}
