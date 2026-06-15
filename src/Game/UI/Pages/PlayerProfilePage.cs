using System.Text;
using Godot;
using CfbSim.Core.Model;
using CfbSim.Core.Stats;

namespace CollegeFootballSimGoDot.UI;

/// <summary>A player's profile: bio, attribute/skill sheet, and season stats. (Bio fields we
/// don't model yet show placeholders — this view will grow.)</summary>
public sealed class PlayerProfilePage(Player player, int teamId) : Page
{
    public override string Title => player.Name;

    public override Control Build()
    {
        var root = Ui.VBox(12);

        var headline = Ui.HBox(10);
        headline.AddChild(Ui.Label($"#{player.JerseyNumber}  {player.Name}", 24, Ui.Accent));
        headline.AddChild(Ui.Label($"{player.Position} · {ClassName(player.Class)}", 16, Ui.Muted));
        headline.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        headline.AddChild(Ui.LinkButton(Nav.Game.Team(teamId).Name, () => Nav.Push(new TeamProfilePage(teamId))));
        root.AddChild(headline);

        var columns = Ui.HBox(16);
        columns.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        columns.AddChild(Ui.Card("Bio", Text(Bio())));
        columns.AddChild(Ui.Card("Attributes", Text(Attributes())));
        columns.AddChild(Ui.Card("Skills", Text(Skills())));
        columns.AddChild(Ui.Card("Season Stats", Text(Stats())));
        columns.AddChild(Ui.Card("Training", Text(Training())));
        root.AddChild(columns);
        return root;
    }

    /// <summary>The active prep boost that applies to this player (by position phase).</summary>
    private string Training()
    {
        TeamBoost b = Nav.Game.BoostFor(teamId);
        bool special = player.Position is Position.K or Position.P;
        bool offense = player.Position is Position.QB or Position.RB or Position.WR or Position.TE or Position.OL;
        int net = special ? b.SpecialBonus : offense ? b.OffenseBonus : b.DefenseBonus;
        string phase = special ? "special teams" : offense ? "offense" : "defense";

        var sb = new StringBuilder();
        sb.AppendLine($"Applies to: {phase}");
        sb.AppendLine($"Net to checks: {(net > 0 ? "+" + net : net.ToString())}");
        sb.AppendLine($"Fatigue:    {(b.Fatigue > 0 ? "-" + b.Fatigue : "0")}");
        sb.AppendLine();
        if (b.Sources.Count > 0)
        {
            sb.AppendLine("This week's prep:");
            foreach (string s in b.Sources.Distinct()) sb.AppendLine($"  • {s}");
        }
        else sb.AppendLine("No training scheduled.");
        return sb.ToString();
    }

    private string Bio()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Position:  {player.Position}");
        sb.AppendLine($"Class:     {ClassName(player.Class)}");
        sb.AppendLine($"Jersey:    #{player.JerseyNumber}");
        sb.AppendLine($"Team:      {Nav.Game.Team(teamId).Name}");
        sb.AppendLine($"Height:    —");
        sb.AppendLine($"Weight:    —");
        sb.AppendLine($"Hometown:  —");
        return sb.ToString();
    }

    private string Attributes()
    {
        CoreAttributes a = player.Attributes;
        var sb = new StringBuilder();
        sb.AppendLine($"Strength    {a.Strength}");
        sb.AppendLine($"Agility     {a.Agility}");
        sb.AppendLine($"Speed       {a.Speed}");
        sb.AppendLine($"Awareness   {a.Awareness}");
        sb.AppendLine($"Durability  {a.Durability}");
        sb.AppendLine($"Composure   {a.Composure}");
        return sb.ToString();
    }

    private string Skills()
    {
        if (player.Skills.Count == 0) return "—";
        var sb = new StringBuilder();
        foreach (var kv in player.Skills.OrderByDescending(k => k.Value))
            sb.AppendLine($"{kv.Key,-16} {kv.Value}");
        return sb.ToString();
    }

    private string Stats()
    {
        PlayerStatLine? s = Nav.Game.PlayerStat(player.Id);
        if (s is null || !s.HasOffense) return "No stats recorded yet.";
        var sb = new StringBuilder();
        if (s.PassAtt > 0) sb.AppendLine($"Passing:   {s.PassComp}/{s.PassAtt}, {s.PassYds} yds, {s.PassTD} TD, {s.PassInt} INT");
        if (s.RushAtt > 0) sb.AppendLine($"Rushing:   {s.RushAtt} car, {s.RushYds} yds, {s.RushTD} TD");
        if (s.Rec > 0) sb.AppendLine($"Receiving: {s.Rec} rec, {s.RecYds} yds, {s.RecTD} TD");
        if (s.Sacks > 0 || s.Interceptions > 0) sb.AppendLine($"Defense:   {s.Sacks} sacks, {s.Interceptions} INT");
        return sb.ToString();
    }

    private static Control Text(string text)
    {
        var label = Ui.Label(text, 13);
        var scroll = new ScrollContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        scroll.AddChild(label);
        return scroll;
    }

    private static string ClassName(ClassYear c) => c switch
    {
        ClassYear.Freshman => "Freshman",
        ClassYear.Sophomore => "Sophomore",
        ClassYear.Junior => "Junior",
        _ => "Senior",
    };
}
