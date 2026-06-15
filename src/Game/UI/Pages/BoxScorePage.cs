using System.Text;
using Godot;
using CfbSim.Core.Stats;

namespace CollegeFootballSimGoDot.UI;

/// <summary>
/// A full-page, ESPN-style box score. Stats we don't yet record (3rd-down %, penalties,
/// red zone, etc.) show a "—" placeholder so the layout reflects the real standard.
/// </summary>
public sealed class BoxScorePage(BoxScore box) : Page
{
    public override string Title => "Box Score";

    public override Control Build()
    {
        var root = Ui.VBox(12);
        root.AddChild(Ui.Label($"{box.AwayName} {box.Away.Points}   @   {box.HomeName} {box.Home.Points}", 22, Ui.Accent2));

        root.AddChild(Ui.Card("Team Stats", TeamComparison()));

        var individual = Ui.HBox(16);
        individual.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        individual.AddChild(Ui.Card($"{box.AwayName}", TeamIndividual(box.AwayTeamId)));
        individual.AddChild(Ui.Card($"{box.HomeName}", TeamIndividual(box.HomeTeamId)));
        root.AddChild(individual);
        return root;
    }

    private Control TeamComparison()
    {
        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 24);
        grid.AddThemeConstantOverride("v_separation", 4);

        (int aC, int aA) = CompAtt(box.AwayTeamId);
        (int hC, int hA) = CompAtt(box.HomeTeamId);
        int aRushAtt = Sum(box.AwayTeamId, p => p.RushAtt);
        int hRushAtt = Sum(box.HomeTeamId, p => p.RushAtt);

        Row(grid, "AWAY", "", "HOME", header: true);
        Row(grid, $"{box.Away.FirstDowns}", "First Downs", $"{box.Home.FirstDowns}");
        Row(grid, "—", "3rd Down Eff", "—");
        Row(grid, "—", "4th Down Eff", "—");
        Row(grid, $"{box.Away.TotalYards}", "Total Yards", $"{box.Home.TotalYards}");
        Row(grid, $"{box.Away.PassYds}", "Passing Yards", $"{box.Home.PassYds}");
        Row(grid, $"{aC}-{aA}", "Comp-Att", $"{hC}-{hA}");
        Row(grid, $"{box.Away.RushYds}", "Rushing Yards", $"{box.Home.RushYds}");
        Row(grid, $"{aRushAtt}", "Rushing Att", $"{hRushAtt}");
        Row(grid, "—", "Penalties-Yards", "—");
        Row(grid, $"{box.Away.Turnovers}", "Turnovers", $"{box.Home.Turnovers}");
        Row(grid, $"{Sum(box.AwayTeamId, p => p.PassInt)}", "Interceptions Thrown", $"{Sum(box.HomeTeamId, p => p.PassInt)}");
        Row(grid, $"{Sum(box.AwayTeamId, p => p.Sacks)}", "Sacks (def)", $"{Sum(box.HomeTeamId, p => p.Sacks)}");
        Row(grid, Top(box.Away.PossessionSeconds), "Possession", Top(box.Home.PossessionSeconds));
        Row(grid, "—", "Red Zone", "—");
        return grid;
    }

    private void Row(GridContainer g, string away, string label, string home, bool header = false)
    {
        Color color = header ? Ui.Accent : Ui.Text;
        var a = Ui.Label(away, 13, color); a.HorizontalAlignment = HorizontalAlignment.Right; a.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var l = Ui.Label(label, 13, Ui.Muted); l.HorizontalAlignment = HorizontalAlignment.Center;
        var h = Ui.Label(home, 13, color); h.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        g.AddChild(a); g.AddChild(l); g.AddChild(h);
    }

    private Control TeamIndividual(int teamId)
    {
        var players = box.PlayersOf(teamId).ToList();
        var body = new Label { Text = Build(players) };
        body.AddThemeFontSizeOverride("font_size", 13);
        body.AddThemeColorOverride("font_color", Ui.Text);
        var scroll = new ScrollContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        scroll.AddChild(body);
        return scroll;
    }

    private static string Build(List<PlayerStatLine> players)
    {
        var sb = new StringBuilder();
        Section(sb, "PASSING", players.Where(p => p.PassAtt > 0).OrderByDescending(p => p.PassYds),
            p => $"{p.Name,-20} {p.PassComp}/{p.PassAtt}  {p.PassYds} yds  {p.PassTD} TD  {p.PassInt} INT");
        Section(sb, "RUSHING", players.Where(p => p.RushAtt > 0).OrderByDescending(p => p.RushYds),
            p => $"{p.Name,-20} {p.RushAtt} car  {p.RushYds} yds  {p.RushTD} TD");
        Section(sb, "RECEIVING", players.Where(p => p.Rec > 0).OrderByDescending(p => p.RecYds),
            p => $"{p.Name,-20} {p.Rec} rec  {p.RecYds} yds  {p.RecTD} TD");
        Section(sb, "DEFENSE", players.Where(p => p.Sacks > 0 || p.Interceptions > 0).OrderByDescending(p => p.Sacks + p.Interceptions),
            p => $"{p.Name,-20} {p.Sacks} sack  {p.Interceptions} INT");
        return sb.ToString();
    }

    private static void Section(StringBuilder sb, string title, IEnumerable<PlayerStatLine> rows, Func<PlayerStatLine, string> fmt)
    {
        var list = rows.ToList();
        sb.AppendLine(title);
        if (list.Count == 0) sb.AppendLine("  —");
        foreach (PlayerStatLine p in list) sb.AppendLine("  " + fmt(p));
        sb.AppendLine();
    }

    private (int Comp, int Att) CompAtt(int teamId)
        => (Sum(teamId, p => p.PassComp), Sum(teamId, p => p.PassAtt));

    private int Sum(int teamId, Func<PlayerStatLine, int> by) => box.PlayersOf(teamId).Sum(by);

    private static string Top(int seconds) => $"{seconds / 60}:{seconds % 60:00}";
}
