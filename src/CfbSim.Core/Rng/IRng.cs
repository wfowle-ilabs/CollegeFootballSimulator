namespace CfbSim.Core.Rng;

/// <summary>
/// Deterministic random source for the whole simulation. We use our own
/// implementation (not Godot's RNG) so the sim core stays engine-independent
/// and reproducible across platforms and .NET versions: same seed ⇒ same run.
/// </summary>
public interface IRng
{
    /// <summary>Uniform integer in [minInclusive, maxInclusive].</summary>
    int NextInt(int minInclusive, int maxInclusive);

    /// <summary>Uniform double in [0, 1).</summary>
    double NextDouble();

    /// <summary>Sample from the standard normal distribution (mean 0, stddev 1).</summary>
    double NextGaussian();
}
