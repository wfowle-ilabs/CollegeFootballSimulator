using System.Text.Json.Serialization;

namespace CfbSim.Core.Model;

/// <summary>
/// A team's temporary, per-game preparation modifier — the in-sim effect of the week's training
/// slots. <see cref="Offense"/>/<see cref="Defense"/>/<see cref="SpecialTeams"/> are small rating
/// bumps added to the relevant players' checks; <see cref="Fatigue"/> is the tradeoff, subtracted
/// from every bonus (overtraining without rest can net negative). Transient — recomputed for each
/// game from the training plan, never serialized.
/// </summary>
public sealed class TeamBoost
{
    public int Offense { get; set; }
    public int Defense { get; set; }
    public int SpecialTeams { get; set; }
    public int Fatigue { get; set; }

    /// <summary>The activities that contributed (for display).</summary>
    public List<string> Sources { get; set; } = new();

    /// <summary>The no-op boost (shared; never mutate it — assign a fresh instance instead).</summary>
    public static readonly TeamBoost None = new();

    [JsonIgnore] public int OffenseBonus => Offense - Fatigue;
    [JsonIgnore] public int DefenseBonus => Defense - Fatigue;
    [JsonIgnore] public int SpecialBonus => SpecialTeams - Fatigue;

    [JsonIgnore]
    public bool IsActive => Offense != 0 || Defense != 0 || SpecialTeams != 0 || Fatigue != 0;
}
