using Godot;
using CfbSim.Core.Sim.Postseason;

namespace CollegeFootballSimGoDot.UI;

/// <summary>The CFP bracket as a page: columns per round, games clickable for the box score,
/// champion banner once the final completes.</summary>
public sealed class BracketPage : Page
{
    public override string Title => "Bracket";

    public override Control Build()
    {
        BracketState bracket = Nav.Game.Bracket!;
        var root = Ui.VBox(10);

        if (Nav.Game.PlayoffComplete)
        {
            var champ = Nav.Game.Team(bracket.ChampionId);
            bool you = Nav.Game.Save!.UserTeamId == champ.Id;
            root.AddChild(Ui.Label($"🏆 NATIONAL CHAMPION: {champ.Name}{(you ? "  — that's you!" : "")}", 24, Ui.Accent2));
        }

        var columns = Ui.HBox(14);
        columns.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        columns.AddChild(RoundColumn("First Round", bracket.FirstRound));
        columns.AddChild(RoundColumn("Quarterfinals", bracket.Quarterfinals));
        columns.AddChild(RoundColumn("Semifinals", bracket.Semifinals));
        columns.AddChild(RoundColumn("Championship", bracket.Championship));
        root.AddChild(columns);
        return root;
    }

    private Control RoundColumn(string title, List<BracketGame> games)
    {
        (Control panel, VBoxContainer body) = Ui.ScrollBox(title);
        foreach (BracketGame g in games) body.AddChild(GameRow(g));
        return panel;
    }

    private Control GameRow(BracketGame g)
    {
        if (g.TeamA == -1 && g.TeamB == -1) return Ui.Label("— TBD —", 13, Ui.Muted);

        string a = g.TeamA == -1 ? "TBD" : $"({g.SeedA}) {Nav.Game.Team(g.TeamA).Abbreviation}";
        string b = g.TeamB == -1 ? "TBD" : $"({g.SeedB}) {Nav.Game.Team(g.TeamB).Abbreviation}";

        if (g.Result is null) return Ui.Label($"{a}  vs  {b}", 13);

        CfpGameResult res = g.Result;
        string text = $"{a} {res.HomeScore}–{res.AwayScore} {b}  → {Nav.Game.Team(res.WinnerId).Abbreviation}";
        var box = Nav.Game.PlayoffBox(g.Round, res.HomeId, res.AwayId);
        return box is not null
            ? Ui.RowButton(text, () => Nav.Push(new BoxScorePage(box)))
            : Ui.Label(text, 13);
    }
}
