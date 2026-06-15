using Godot;
using CfbSim.Core.Model;

namespace CollegeFootballSimGoDot.UI;

/// <summary>A team's roster grouped by position, filterable by position and class.
/// Filter state is kept in fields so it survives navigating into a player and back.</summary>
public sealed class RosterPage(int teamId) : Page
{
    private int _positionFilter = -1; // -1 = all
    private int _classFilter = -1;

    public override string Title => $"{Nav.Game.Team(teamId).Abbreviation} Roster";

    public override Control Build()
    {
        Team team = Nav.Game.Team(teamId);
        var root = Ui.VBox(10);

        // Filters.
        var filters = Ui.HBox(12);
        filters.AddChild(Ui.Label("Position:", 14));
        var pos = new OptionButton();
        pos.AddItem("All", -1);
        foreach (Position p in Enum.GetValues<Position>()) pos.AddItem(p.ToString(), (int)p);
        pos.Selected = Index(pos, _positionFilter);
        pos.ItemSelected += _ => { _positionFilter = pos.GetSelectedId(); Nav.Refresh(); };
        filters.AddChild(pos);

        filters.AddChild(Ui.Label("Class:", 14));
        var cls = new OptionButton();
        cls.AddItem("All", -1);
        foreach (ClassYear c in Enum.GetValues<ClassYear>()) cls.AddItem(ClassShort(c), (int)c);
        cls.Selected = Index(cls, _classFilter);
        cls.ItemSelected += _ => { _classFilter = cls.GetSelectedId(); Nav.Refresh(); };
        filters.AddChild(cls);
        root.AddChild(filters);

        // Roster grouped by position.
        (Control panel, VBoxContainer body) = Ui.ScrollBox($"{team.Name}  ·  click a player");
        var players = team.Roster.Where(Passes);
        foreach (var group in players.GroupBy(p => p.Position).OrderBy(g => g.Key))
        {
            body.AddChild(Ui.Label(group.Key.ToString(), 14, Ui.Accent2));
            int slot = 1;
            foreach (Player p in group)
            {
                CoreAttributes a = p.Attributes;
                string tag = slot == 1 ? "★" : " ";
                string text = $"{tag} #{p.JerseyNumber,-2} {p.Name,-22} {ClassShort(p.Class)}   STR {a.Strength,2}  AGI {a.Agility,2}  SPD {a.Speed,2}  AWR {a.Awareness,2}";
                Player captured = p;
                body.AddChild(Ui.RowButton(text, () => Nav.Push(new PlayerProfilePage(captured, teamId))));
                slot++;
            }
        }
        root.AddChild(panel);
        return root;
    }

    private bool Passes(Player p)
        => (_positionFilter == -1 || (int)p.Position == _positionFilter)
        && (_classFilter == -1 || (int)p.Class == _classFilter);

    private static int Index(OptionButton b, int id)
    {
        for (int i = 0; i < b.ItemCount; i++)
            if (b.GetItemId(i) == id) return i;
        return 0;
    }

    private static string ClassShort(ClassYear c) => c switch
    {
        ClassYear.Freshman => "FR",
        ClassYear.Sophomore => "SO",
        ClassYear.Junior => "JR",
        _ => "SR",
    };
}
