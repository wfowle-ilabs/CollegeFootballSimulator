using Godot;

namespace CollegeFootballSimGoDot.UI;

/// <summary>
/// The playoff screen: a header with "Sim Next Round", and a <see cref="NavHost"/> rooted at
/// the bracket (so games drill into full box-score pages with breadcrumbs). The champion is
/// crowned on the final; "Start Next Season" then advances the calendar.
/// </summary>
public sealed class PostseasonScreen(GameManager gm, Action onNextSeason, Action onMainMenu)
{
    private NavHost _nav = null!;
    private Button _simRound = null!;
    private Button _nextSeason = null!;

    public Control Build()
    {
        var root = Ui.VBox(10);

        var bar = Ui.HBox(10);
        var title = Ui.Heading($"{gm.Year} College Football Playoff");
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        bar.AddChild(title);
        _simRound = Ui.Button("Sim Next Round", () => { gm.AdvancePlayoffRound(); AfterRound(); }, primary: true);
        _nextSeason = Ui.Button("Start Next Season →", onNextSeason, primary: true);
        bar.AddChild(_simRound);
        bar.AddChild(_nextSeason);
        bar.AddChild(Ui.Button("Main Menu", onMainMenu));
        root.AddChild(bar);

        _nav = new NavHost(gm);
        root.AddChild(_nav.Build());
        _nav.SetRoot(new BracketPage());

        UpdateButtons();
        return Ui.Padded(root, 16);
    }

    private void AfterRound()
    {
        UpdateButtons();
        _nav.SetRoot(new BracketPage()); // back to the bracket, now updated
    }

    private void UpdateButtons()
    {
        bool done = gm.PlayoffComplete;
        _simRound.Visible = !done;
        _nextSeason.Visible = done;
    }
}
