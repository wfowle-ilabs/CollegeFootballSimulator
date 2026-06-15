using Godot;
using CfbSim.Core.Sim.Season;

namespace CollegeFootballSimGoDot.UI;

/// <summary>
/// The in-season shell: a top nav bar (Season / Team / Stats / School / History / Media) with a
/// hamburger that opens a right-side slide-over game menu (Save / Main Menu), a focal current-day
/// indicator with advance controls (Advance Day + a "Sim to…" checkpoint dropdown), and a
/// <see cref="NavHost"/> content area. Pages can request "sim to here"; multi-day sims confirm
/// (with a "Don't ask again" toggle) and each advance plays a short fade transition. Leaving to
/// the main menu with unsaved progress prompts first.
/// </summary>
public sealed class GameShell(GameManager gm, Action onPostseason, Action onMainMenu)
{
	private const float MenuWidth = 320f;
	private static readonly string[] Sections = { "Season", "Team", "Stats", "School", "History", "Media" };

	private readonly Dictionary<string, Button> _navButtons = new();
	private NavHost _nav = null!;
	private Control _navRoot = null!;
	private Label _dayLabel = null!;
	private Label _subLabel = null!;
	private Button _advanceDay = null!;
	private MenuButton _simTo = null!;
	private Button _playoff = null!;
	private ConfirmationDialog _simConfirm = null!;
	private ConfirmationDialog _unsavedDialog = null!;
	private CheckBox _dontAsk = null!;
	private Action? _pendingSim;
	private Control _menuOverlay = null!;
	private PanelContainer _menuPanel = null!;
	private string _section = "Season";

	public Control Build()
	{
		var root = Ui.VBox(10);

		// Top nav row: sections (left) · hamburger game menu (right).
		var nav = Ui.HBox(6);
		foreach (string s in Sections)
		{
			string section = s;
			Button button = Ui.Button(section, () => ShowSection(section));
			button.CustomMinimumSize = new Vector2(120, 36);
			_navButtons[section] = button;
			nav.AddChild(button);
		}
		nav.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
		Button hamburger = Ui.Button("☰  Menu", OpenMenu);
		hamburger.CustomMinimumSize = new Vector2(100, 36);
		nav.AddChild(hamburger);
		root.AddChild(nav);

		// Current-day indicator + advance controls, grouped.
		var bar = Ui.HBox(14);
		var cluster = Ui.VBox(1);
		_dayLabel = Ui.Label("", 20, Ui.Accent);
		_subLabel = Ui.Label("", 13, Ui.Muted);
		cluster.AddChild(_dayLabel);
		cluster.AddChild(_subLabel);
		bar.AddChild(cluster);

		_advanceDay = Ui.Button("Advance Day →", OnAdvanceDay, primary: true);
		_simTo = Ui.MenuButton("Sim to ▾");
		PopupMenu menu = _simTo.GetPopup();
		menu.AddItem("My Next Game", 0);
		menu.AddItem("End of Week", 1);
		menu.AddItem("End of Regular Season", 2);
		menu.IdPressed += OnSimTo;
		_playoff = Ui.Button("Go to Playoff →", onPostseason, primary: true);
		bar.AddChild(_advanceDay);
		bar.AddChild(_simTo);
		bar.AddChild(_playoff);
		bar.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
		root.AddChild(bar);

		BuildDialogs(root);

		// NavHost can ask the shell to run a sim ("sim to here" from a page) through the same
		// confirmation + transition the dropdown uses.
		_nav = new NavHost(gm, (what, perform) => ConfirmSim(what, () => { perform(); AfterSim(); }));
		_navRoot = _nav.Build();
		root.AddChild(_navRoot);

		UpdateHeader();
		ShowSection("Season");

		// The slide-over menu overlays the content (does not resize it).
		var content = Ui.Padded(root, 16);
		var screen = new Control();
		screen.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		screen.AddChild(content);
		screen.AddChild(BuildMenuOverlay());
		return screen;
	}

	private void BuildDialogs(Control parent)
	{
		_simConfirm = new ConfirmationDialog { Title = "Confirm Sim", Size = new Vector2I(360, 140) };
		_dontAsk = new CheckBox { Text = "Don't ask again" };
		_simConfirm.AddChild(_dontAsk);
		_simConfirm.Confirmed += () =>
		{
			if (_dontAsk.ButtonPressed) AppSettings.ConfirmSimWeek = false;
			Action? run = _pendingSim;
			_pendingSim = null;
			run?.Invoke();
		};
		parent.AddChild(_simConfirm);

		_unsavedDialog = new ConfirmationDialog { Title = "Unsaved Progress", Size = new Vector2I(400, 140) };
		_unsavedDialog.DialogText = "You have unsaved progress. Save before leaving to the main menu?";
		_unsavedDialog.OkButtonText = "Save & Exit";
		_unsavedDialog.Confirmed += () => { gm.SaveGame(); onMainMenu(); };
		_unsavedDialog.AddButton("Exit Without Saving", true, "discard");
		_unsavedDialog.CustomAction += action =>
		{
			if (action == "discard") { _unsavedDialog.Hide(); onMainMenu(); }
		};
		parent.AddChild(_unsavedDialog);
	}

	// --- Slide-over game menu ---

	private Control BuildMenuOverlay()
	{
		_menuOverlay = new Control { Visible = false };
		_menuOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);

		// Dimmer + click-catcher: tap outside the panel closes the menu.
		var catcher = new Button { Flat = true };
		catcher.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		var dim = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.45f) };
		catcher.AddThemeStyleboxOverride("normal", dim);
		catcher.AddThemeStyleboxOverride("hover", dim);
		catcher.AddThemeStyleboxOverride("pressed", dim);
		catcher.Pressed += CloseMenu;
		_menuOverlay.AddChild(catcher);

		// Right-anchored panel (starts off-screen; slides in).
		_menuPanel = new PanelContainer
		{
			AnchorLeft = 1, AnchorRight = 1, AnchorTop = 0, AnchorBottom = 1,
			OffsetTop = 0, OffsetBottom = 0, OffsetLeft = 0, OffsetRight = MenuWidth,
		};
		_menuPanel.AddThemeStyleboxOverride("panel", Ui.SidePanelStyle());

		var v = Ui.VBox(10);
		v.AddChild(Ui.Label("Game Menu", 20, Ui.Accent));
		v.AddChild(Ui.Label("", 4)); // spacer
		Button save = Ui.Button("Save Game", () => { gm.SaveGame(); CloseMenu(); }, primary: true);
		save.CustomMinimumSize = new Vector2(MenuWidth - 32, 40);
		Button mainMenu = Ui.Button("Main Menu", OnMainMenu);
		mainMenu.CustomMinimumSize = new Vector2(MenuWidth - 32, 40);
		v.AddChild(save);
		v.AddChild(mainMenu);
		v.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
		Button close = Ui.Button("Close", CloseMenu);
		close.CustomMinimumSize = new Vector2(MenuWidth - 32, 36);
		v.AddChild(close);
		_menuPanel.AddChild(Ui.Padded(v, 16));
		_menuOverlay.AddChild(_menuPanel);
		return _menuOverlay;
	}

	private void OpenMenu()
	{
		_menuOverlay.Visible = true;
		Tween tw = _menuPanel.CreateTween().SetParallel();
		tw.TweenProperty(_menuPanel, "offset_left", -MenuWidth, 0.18f).SetTrans(Tween.TransitionType.Sine);
		tw.TweenProperty(_menuPanel, "offset_right", 0f, 0.18f).SetTrans(Tween.TransitionType.Sine);
	}

	private void CloseMenu()
	{
		Tween tw = _menuPanel.CreateTween().SetParallel();
		tw.TweenProperty(_menuPanel, "offset_left", 0f, 0.16f).SetTrans(Tween.TransitionType.Sine);
		tw.TweenProperty(_menuPanel, "offset_right", MenuWidth, 0.16f).SetTrans(Tween.TransitionType.Sine);
		tw.Chain().TweenCallback(Callable.From(() => _menuOverlay.Visible = false));
	}

	private void OnMainMenu()
	{
		CloseMenu();
		if (gm.HasUnsavedChanges) _unsavedDialog.PopupCentered();
		else onMainMenu();
	}

	// --- Advance controls ---

	private void OnAdvanceDay()
	{
		gm.AdvanceDay();
		AfterSim();
	}

	private void OnSimTo(long id)
	{
		(string what, Action run) = id switch
		{
			0 => ("to your next game", (Action)(() => { gm.SimToMyNextGame(); AfterSim(); })),
			1 => ("to the end of the week", (Action)(() => { gm.SimToEndOfWeek(); AfterSim(); })),
			_ => ("to the end of the regular season", (Action)(() => { gm.SimToEndOfSeason(); AfterSim(); })),
		};
		ConfirmSim(what, run);
	}

	private void ConfirmSim(string what, Action run)
	{
		if (AppSettings.ConfirmSimWeek)
		{
			_pendingSim = run;
			_dontAsk.ButtonPressed = false;
			_simConfirm.DialogText = $"Sim {what}?";
			_simConfirm.PopupCentered();
		}
		else
		{
			run();
		}
	}

	// --- Sections ---

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
		"School" => new SchoolPage(gm.UserTeam.Id),
		"History" => new HistoryPage(),
		"Media" => new MediaPage(),
		_ => new SeasonPage(),
	};

	private void AfterSim()
	{
		UpdateHeader();
		_nav.Refresh();
		PlayTransition();
	}

	/// <summary>A short fade-in on the content area so an advanced day reads as a transition.</summary>
	private void PlayTransition()
	{
		if (!_navRoot.IsInsideTree()) return;
		_navRoot.Modulate = new Color(1, 1, 1, 0.25f);
		Tween tween = _navRoot.CreateTween();
		tween.TweenProperty(_navRoot, "modulate:a", 1.0f, 0.22f).SetTrans(Tween.TransitionType.Sine);
	}

	private void UpdateHeader()
	{
		var save = gm.Save!;
		var rec = gm.UserRecord;
		bool done = save.Season.IsComplete;

		if (done)
		{
			_dayLabel.Text = "Regular season complete";
			_subLabel.Text = $"{save.Year}   {gm.UserTeam.Name}   {rec.Wins}-{rec.Losses} ({rec.ConfWins}-{rec.ConfLosses})";
		}
		else
		{
			DateOnly d = gm.CurrentDate;
			_dayLabel.Text = $"{d:dddd, MMMM d, yyyy}";
			_subLabel.Text = $"Week {gm.CurrentWeek} of {save.Season.Schedule.Weeks}   ·   {gm.UserTeam.Name} {rec.Wins}-{rec.Losses} ({rec.ConfWins}-{rec.ConfLosses})";
		}

		_advanceDay.Visible = !done;
		_simTo.Visible = !done;
		_playoff.Visible = done;
	}
}
