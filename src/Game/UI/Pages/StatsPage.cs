using Godot;
using CfbSim.Core.Stats;

namespace CollegeFootballSimGoDot.UI;

/// <summary>Season statistical leaders; players click through to their profiles.</summary>
public sealed class StatsPage : Page
{
    public override string Title => "Stats";

    public override Control Build()
    {
        var root = Ui.VBox(8);
        root.AddChild(Ui.Label("Season Leaders (FBS)  ·  click a player", 16, Ui.Muted));

        if (!Nav.Game.SeasonStats.Players.Any())
        {
            root.AddChild(Ui.Label("No games played yet — sim a week to generate stats.", 14));
            return root;
        }

        var columns = Ui.HBox(16);
        columns.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        columns.AddChild(Column("Passing", Nav.Game.SeasonStats.Leaders(p => p.PassYds, 15),
            p => $"{p.PassComp}/{p.PassAtt} {p.PassYds}yd {p.PassTD}TD {p.PassInt}INT"));
        columns.AddChild(Column("Rushing", Nav.Game.SeasonStats.Leaders(p => p.RushYds, 15),
            p => $"{p.RushAtt}att {p.RushYds}yd {p.RushTD}TD"));
        columns.AddChild(Column("Receiving", Nav.Game.SeasonStats.Leaders(p => p.RecYds, 15),
            p => $"{p.Rec}rec {p.RecYds}yd {p.RecTD}TD"));
        root.AddChild(columns);
        return root;
    }

    private Control Column(string title, IEnumerable<PlayerStatLine> leaders, Func<PlayerStatLine, string> stat)
    {
        (Control panel, VBoxContainer body) = Ui.ScrollBox(title);
        int rank = 1;
        foreach (PlayerStatLine p in leaders)
        {
            int r = rank++;
            body.AddChild(Ui.RowButton(
                $"{r,2}. {p.Name} ({Nav.Game.Team(p.TeamId).Abbreviation})  {stat(p)}",
                () => OpenPlayer(p)));
        }
        return panel;
    }

    private void OpenPlayer(PlayerStatLine line)
    {
        var player = Nav.Game.PlayerById(line.PlayerId);
        if (player is not null) Nav.Push(new PlayerProfilePage(player, line.TeamId));
    }
}
