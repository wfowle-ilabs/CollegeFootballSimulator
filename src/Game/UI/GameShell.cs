using Godot;

namespace CollegeFootballSimGoDot.UI;

/// <summary>
/// The in-season shell: a top nav bar (Season / Team / Stats / History / Media), a header +
/// sim controls, and a <see cref="NavHost"/> content area with breadcrumb drill-down. Each
/// section sets the nav root; sims refresh the active page.
/// </summary>
public sealed class GameShell(GameManager gm, Action onPostseason, Action onMainMenu)
{
	private static readonly string[] Sections = { "Season", "Team", "Stats", "History", "Media" };

	private readonly Dictionary<string, Button> _navButtons = new();
	private NavHost _nav = null!;
	private Label _header = null!;
	private Button _simWeek = null!;
	private Button _simEnd = null!;
	private Button _playoff = null!;
	private string _section = "Season";

	public Control Build()
	{
		var root = Ui.VBox(10);

		var nav = Ui.HBox(6);
		foreach (string s in Sections)
		{
			string section = s;
			Button button = Ui.Button(section, () => ShowSection(section));
			button.CustomMinimumSize = new Vector2(130, 36);
			_navButtons[section] = button;
			nav.AddChild(button);
		}
		nav.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
		nav.AddChild(Ui.Button("Save", () => gm.SaveGame()));
		nav.AddChild(Ui.Button("Main Menu", onMainMenu));
		root.AddChild(nav);

		var bar = Ui.HBox(8);
		_header = Ui.Label("", 16);
		_header.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		bar.AddChild(_header);
		_simWeek = Ui.Button("Sim Week", () => { gm.SimWeek(); AfterSim(); });
		_simEnd = Ui.Button("Sim to End", () => { gm.SimToEndOfSeason(); AfterSim(); });
		_playoff = Ui.Button("Go to Playoff →", onPostseason, primary: true);
		bar.AddChild(_simWeek);
		bar.AddChild(_simEnd);
		bar.AddChild(_playoff);
		root.AddChild(bar);

		_nav = new NavHost(gm);
		root.AddChild(_nav.Build());

		UpdateHeader();
		ShowSection("Season");
		return Ui.Padded(root, 16);
	}

	private void ShowSection(string section)
	{
		_section = section;
		foreach ((string name, Button button) in _navButtons)
			button.AddThemeColorOverride("font_color", name == section ? Ui.Accent : Ui.Text);
		_nav.SetRoot(PageFor(section));
	}

	private Page PageFor(string section) => section switch
	{
		"Team" => new TeamProfilePage(gm.UserTeam.Id),
		"Stats" => new StatsPage(),
		"History" => new HistoryPage(),
		"Media" => new MediaPage(),
		_ => new SeasonPage(),
	};

	private void AfterSim()
	{
		UpdateHeader();
		_nav.Refresh();
	}

	private void UpdateHeader()
	{
		var save = gm.Save!;
		var rec = gm.UserRecord;
		bool done = save.Season.IsComplete;
		string phase = done ? "Regular season complete" : $"Week {save.Season.NextWeek} of {save.Season.Schedule.Weeks}";
		_header.Text = $"{save.Year}   {gm.UserTeam.Name}   {rec.Wins}-{rec.Losses} ({rec.ConfWins}-{rec.ConfLosses})    |    {phase}";
		_simWeek.Visible = !done;
		_simEnd.Visible = !done;
		_playoff.Visible = done;
	}
}
