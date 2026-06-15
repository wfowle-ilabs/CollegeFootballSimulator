using Godot;
using CfbSim.Core.Model;

namespace CollegeFootballSimGoDot.UI;

/// <summary>Pick a conference, then a team within it, to start a save.</summary>
public sealed class TeamSelectScreen(GameManager gm, Action onStart, Action onBack)
{
    private OptionButton _conference = null!;
    private ItemList _teams = null!;
    private Button _start = null!;
    private readonly List<int> _teamIds = new();
    private int _selectedTeamId = -1;

    public Control Build()
    {
        var root = Ui.VBox(12);
        root.AddChild(Ui.Heading("Choose your program"));

        _conference = new OptionButton();
        foreach (Conference c in gm.Save!.League.Conferences)
            _conference.AddItem($"{c.Abbreviation} — {c.Name}", c.Id);
        _conference.AddItem("IND — Independents", 0);
        _conference.Selected = 0;
        _conference.ItemSelected += _ => PopulateTeams();
        root.AddChild(_conference);

        _teams = new ItemList
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(380, 320),
        };
        _teams.ItemSelected += index =>
        {
            _selectedTeamId = _teamIds[(int)index];
            _start.Disabled = false;
        };
        root.AddChild(_teams);

        var buttons = Ui.HBox();
        buttons.AddChild(Ui.Button("Back", onBack));
        _start = Ui.Button("Start Season", () => { gm.SelectTeam(_selectedTeamId); onStart(); });
        _start.Disabled = true;
        buttons.AddChild(_start);
        root.AddChild(buttons);

        PopulateTeams();
        return Ui.Padded(root);
    }

    private void PopulateTeams()
    {
        _teams.Clear();
        _teamIds.Clear();
        _selectedTeamId = -1;
        _start.Disabled = true;

        int conferenceId = _conference.GetSelectedId();
        IEnumerable<Team> teams = conferenceId == 0
            ? gm.Save!.League.Independents
            : gm.Save!.League.Conferences.First(c => c.Id == conferenceId).Teams;

        foreach (Team t in teams.OrderByDescending(t => t.Prestige))
        {
            _teams.AddItem($"{t.Name}   (prestige {t.Prestige})");
            _teamIds.Add(t.Id);
        }
    }
}
