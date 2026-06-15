using CfbSim.Core.Model;
using CfbSim.Core.Rng;

namespace CfbSim.Core.Generation;

/// <summary>
/// Deterministic, prestige-weighted roster generation (see docs/mechanics.qmd).
/// Each rating ~ Normal(mean, sigma) clamped 1–20, with the mean shifted by team
/// prestige. Attributes and skills are drawn independently (independent axes).
/// Same seed + same inputs ⇒ identical roster.
/// </summary>
public sealed class PlayerGenerator
{
    private const double Sigma = 2.5;

    /// <summary>Prestige 1..100 → mean shift of roughly -4..+4.</summary>
    private static double PrestigeShift(int prestige) => (prestige - 50) / 12.0;

    public Team GenerateTeam(IRng rng, int id, string name, string abbr, int prestige, RosterSpec? spec = null)
    {
        spec ??= RosterSpec.Default;
        double shift = PrestigeShift(prestige);

        var team = new Team { Id = id, Name = name, Abbreviation = abbr, Prestige = prestige };
        var usedNumbers = new HashSet<int>();
        int nextId = id * 1000;

        foreach (var (position, count) in spec.Counts)
            for (int i = 0; i < count; i++)
                team.Roster.Add(GeneratePlayer(rng, ++nextId, position, shift, usedNumbers));

        return team;
    }

    /// <summary>Generate a single incoming freshman (the offseason walk-on stand-in for recruiting).</summary>
    public Player GenerateFreshman(IRng rng, int id, Position position, int prestige, HashSet<int> usedNumbers)
        => GeneratePlayer(rng, id, position, PrestigeShift(prestige), usedNumbers, ClassYear.Freshman);

    private static Player GeneratePlayer(IRng rng, int id, Position position, double shift, HashSet<int> usedNumbers, ClassYear? forcedClass = null)
    {
        PositionProfile profile = PositionProfiles.For(position);
        (string first, string last) = NamePool.Next(rng);

        var attributes = new CoreAttributes
        {
            Strength = Roll(rng, profile.Strength, shift),
            Agility = Roll(rng, profile.Agility, shift),
            Speed = Roll(rng, profile.Speed, shift),
            Awareness = Roll(rng, profile.Awareness, shift),
            Durability = Roll(rng, profile.Durability, shift),
            Composure = Roll(rng, profile.Composure, shift),
        };

        var skills = new Dictionary<Skill, int>();
        foreach (Skill skill in profile.Skills)
            skills[skill] = Roll(rng, profile.BaseSkillMean, shift);

        return new Player
        {
            Id = id,
            FirstName = first,
            LastName = last,
            Position = position,
            Class = forcedClass ?? RollClass(rng),
            JerseyNumber = RollJersey(rng, position, usedNumbers),
            Attributes = attributes,
            Skills = skills,
        };
    }

    /// <summary>Normal(mean + shift, sigma) rounded and clamped to [1, 20].</summary>
    private static int Roll(IRng rng, int mean, double shift)
    {
        double v = rng.NextGaussian() * Sigma + mean + shift;
        return Math.Clamp((int)Math.Round(v), 1, 20);
    }

    private static ClassYear RollClass(IRng rng)
    {
        int r = rng.NextInt(1, 100);
        return r <= 30 ? ClassYear.Freshman
            : r <= 58 ? ClassYear.Sophomore
            : r <= 82 ? ClassYear.Junior
            : ClassYear.Senior;
    }

    private static int RollJersey(IRng rng, Position position, HashSet<int> used)
    {
        (int lo, int hi) = position switch
        {
            Position.QB => (1, 19),
            Position.RB => (20, 49),
            Position.WR => (1, 89),
            Position.TE => (40, 89),
            Position.OL => (50, 79),
            Position.DL or Position.EDGE => (50, 99),
            Position.LB => (40, 59),
            Position.CB or Position.S => (1, 49),
            _ => (1, 99),
        };

        for (int attempt = 0; attempt < 50; attempt++)
        {
            int n = rng.NextInt(lo, hi);
            if (used.Add(n)) return n;
        }
        return rng.NextInt(0, 99); // fallback; uniqueness is best-effort in M1
    }
}
