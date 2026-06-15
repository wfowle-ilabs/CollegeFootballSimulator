using CfbSim.Core.Model;

namespace CfbSim.Core.Generation;

/// <summary>
/// Per-position baselines for generation: the mean for each core attribute and
/// the set of skills the position carries (each generated around BaseSkillMean).
/// First-pass numbers — an M9 tuning surface.
/// </summary>
public sealed record PositionProfile(
    int Strength, int Agility, int Speed, int Awareness, int Durability, int Composure,
    int BaseSkillMean,
    Skill[] Skills);

public static class PositionProfiles
{
    private static readonly Dictionary<Position, PositionProfile> Map = new()
    {
        [Position.QB] = new(10, 11, 11, 14, 10, 13, 11,
            new[] { Skill.ThrowPower, Skill.ShortAccuracy, Skill.DeepAccuracy }),
        [Position.RB] = new(12, 14, 14, 11, 11, 11, 11,
            new[] { Skill.Vision, Skill.Elusiveness, Skill.Trucking, Skill.BallSecurity, Skill.Receiving, Skill.PassBlock }),
        [Position.WR] = new(10, 14, 15, 11, 10, 11, 11,
            new[] { Skill.RouteRunning, Skill.Catching }),
        [Position.TE] = new(13, 11, 11, 11, 12, 11, 11,
            new[] { Skill.RunBlock, Skill.Catching, Skill.RouteRunning }),
        [Position.OL] = new(15, 9, 8, 12, 13, 11, 11,
            new[] { Skill.RunBlock, Skill.PassBlock, Skill.AnchorStrength, Skill.PullSpeed }),
        [Position.DL] = new(15, 11, 10, 11, 12, 11, 11,
            new[] { Skill.BlockShed, Skill.PassRush, Skill.RunStop, Skill.Pursuit }),
        [Position.EDGE] = new(13, 13, 13, 11, 11, 11, 11,
            new[] { Skill.BlockShed, Skill.PassRush, Skill.RunStop, Skill.Pursuit }),
        [Position.LB] = new(13, 12, 12, 13, 12, 11, 11,
            new[] { Skill.Tackling, Skill.RunStop, Skill.ZoneCoverage, Skill.Blitz, Skill.Pursuit }),
        [Position.CB] = new(10, 14, 15, 12, 10, 11, 11,
            new[] { Skill.ManCoverage, Skill.ZoneCoverage, Skill.Tackling }),
        [Position.S] = new(11, 13, 13, 13, 11, 11, 11,
            new[] { Skill.ZoneCoverage, Skill.Tackling, Skill.RunStop, Skill.Pursuit }),
        [Position.K] = new(9, 9, 9, 12, 10, 13, 11,
            new[] { Skill.KickPower, Skill.KickAccuracy }),
        [Position.P] = new(9, 9, 9, 11, 10, 12, 11,
            new[] { Skill.KickPower }),
    };

    public static PositionProfile For(Position position) => Map[position];
}
