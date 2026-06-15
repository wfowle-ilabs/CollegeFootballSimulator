namespace CfbSim.Core.Rng;

/// <summary>A serializable snapshot of a <see cref="Pcg32Rng"/>'s internal state.</summary>
public readonly record struct Pcg32State(ulong State, ulong Inc, double? GaussianSpare);
