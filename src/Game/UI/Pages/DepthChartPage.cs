using Godot;
using CfbSim.Core.Model;

namespace CollegeFootballSimGoDot.UI;

/// <summary>Basic depth-chart management for the user team: pick a position and set the starter.</summary>
public sealed class DepthChartPage : Page
{
    private int _position = (int)Position.QB;

    public override string Title => "Depth Chart";

    public override Control Build()
    {
        var root = Ui.VBox(10);
        root.AddChild(Ui.Label("Set the starter (top of the depth chart) at each position.", 13, Ui.Muted));

        var controls = Ui.HBox(10);
        controls.AddChild(Ui.Label("Position:", 14));
        var pos = new OptionButton();
        foreach (Position p in Enum.GetValues<Position>()) pos.AddItem(p.ToString(), (int)p);
        pos.Selected = _position;
        pos.ItemSelected += _ => { _position = pos.GetSelectedId(); Nav.Refresh(); };
        controls.AddChild(pos);
        root.AddChild(controls);

        (Control panel, VBoxContainer body) = Ui.ScrollBox(((Position)_position).ToString());
        int slot = 1;
        foreach (Player p in Nav.Game.UserTeam.Roster.Where(p => (int)p.Position == _position))
        {
            CoreAttributes a = p.Attributes;
            string label = slot == 1 ? "STARTER" : $"#{slot}";
            var row = Ui.HBox(8);
            row.AddChild(Ui.Label($"{label,-8} {p.Name,-22}  STR {a.Strength} AGI {a.Agility} SPD {a.Speed} AWR {a.Awareness}", 13));
            if (slot > 1)
            {
                Player captured = p;
                row.AddChild(Ui.LinkButton("Set as Starter", () => { Nav.Game.PromoteToStarter(captured.Id); Nav.Refresh(); }));
            }
            body.AddChild(row);
            slot++;
        }
        root.AddChild(panel);
        return root;
    }
}
