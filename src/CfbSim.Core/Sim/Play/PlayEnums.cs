namespace CfbSim.Core.Sim.Play;

/// <summary>The v1 playbook (M2). More concepts can be added behind the same resolver dispatch.</summary>
public enum PlayType
{
    InsideRun, OutsideRun,
    ShortPass, DeepPass,
    Punt, FieldGoal, ExtraPoint, Kneel
}

/// <summary>What the defense is keying on this snap — set by the defensive play-caller.</summary>
public enum DefensiveKey { Balanced, StopRun, StopPass }

/// <summary>How a play turned the ball over, if at all.</summary>
public enum TurnoverKind { None, Interception, FumbleLost }
