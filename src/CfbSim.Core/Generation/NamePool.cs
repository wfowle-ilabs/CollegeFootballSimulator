using CfbSim.Core.Rng;

namespace CfbSim.Core.Generation;

/// <summary>A small stub name pool for M1. Replaced/expanded later.</summary>
public static class NamePool
{
	private static readonly string[] First =
	{
		"Jalen", "Mason", "Carter", "Devon", "Tyler", "Marcus", "Caleb", "Xavier",
		"Brock", "Trey", "Dante", "Isaiah", "Cooper", "Garrett", "Malik", "Bryce",
		"Hunter", "Demarco", "Elijah", "Cole", "Rashad", "Logan", "Amari", "Knox",
	};

	private static readonly string[] Last =
	{
		"Williams", "Johnson", "Sanders", "Carter", "Robinson", "Brooks", "Hayes",
		"Mitchell", "Coleman", "Parker", "Foster", "Bennett", "Walker", "Reed",
		"Jennings", "Dawson", "Holloway", "Pierce", "Mccoy", "Vance", "Aldridge",
		"Okafor", "Castillo", "Nguyen",
	};

	public static (string First, string Last) Next(IRng rng)
		=> (First[rng.NextInt(0, First.Length - 1)], Last[rng.NextInt(0, Last.Length - 1)]);
}
