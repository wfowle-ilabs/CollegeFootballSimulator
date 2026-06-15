namespace CfbSim.Core.Rng;

/// <summary>
/// PCG32 — a small, fast, statistically solid PRNG with a portable, fully
/// deterministic algorithm. Chosen over System.Random (whose sequence is not
/// guaranteed stable across runtimes) and over Godot's RNG (engine coupling).
/// Reference: https://www.pcg-random.org/
/// </summary>
public sealed class Pcg32Rng : IRng
{
    private ulong _state;
    private readonly ulong _inc;
    private double? _gaussianSpare;

    public Pcg32Rng(ulong seed, ulong sequence = 0xDA3E39CB94B95BDBUL)
    {
        _state = 0UL;
        _inc = (sequence << 1) | 1UL;
        NextUInt();
        _state += seed;
        NextUInt();
    }

    // Restore constructor (distinct signature via Pcg32State).
    private Pcg32Rng(Pcg32State snapshot)
    {
        _state = snapshot.State;
        _inc = snapshot.Inc;
        _gaussianSpare = snapshot.GaussianSpare;
    }

    /// <summary>Capture the full internal state (including a pending Gaussian sample) for saving.</summary>
    public Pcg32State Snapshot() => new(_state, _inc, _gaussianSpare);

    /// <summary>Recreate an RNG that will continue exactly where a snapshot left off.</summary>
    public static Pcg32Rng Restore(Pcg32State snapshot) => new(snapshot);

    private uint NextUInt()
    {
        ulong old = _state;
        _state = old * 6364136223846793005UL + _inc;
        uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
        int rot = (int)(old >> 59);
        return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
    }

    public int NextInt(int minInclusive, int maxInclusive)
    {
        if (maxInclusive < minInclusive)
            throw new ArgumentException($"max ({maxInclusive}) < min ({minInclusive})");

        uint range = (uint)((long)maxInclusive - minInclusive + 1);
        if (range == 0) // full 32-bit span requested
            return unchecked((int)NextUInt());

        // Unbiased bounded sampling (PCG's bounded_rand): reject the low remainder.
        uint threshold = (uint)((0x100000000UL - range) % range);
        uint r;
        do { r = NextUInt(); } while (r < threshold);
        return minInclusive + (int)(r % range);
    }

    public double NextDouble() => NextUInt() * (1.0 / 4294967296.0); // / 2^32

    public double NextGaussian()
    {
        if (_gaussianSpare is { } spare)
        {
            _gaussianSpare = null;
            return spare;
        }

        double u1;
        do { u1 = NextDouble(); } while (u1 <= 1e-12);
        double u2 = NextDouble();
        double mag = Math.Sqrt(-2.0 * Math.Log(u1));
        _gaussianSpare = mag * Math.Sin(2.0 * Math.PI * u2);
        return mag * Math.Cos(2.0 * Math.PI * u2);
    }
}
