using Godot;
using CollegeFootballSimGoDot.UI;

namespace CollegeFootballSimGoDot;

/// <summary>
/// The application root and screen manager. Owns the <see cref="GameManager"/> and swaps
/// code-built screens (main menu → team select → season hub → postseason). This is the
/// composition root for the playable shell (M7).
/// </summary>
public partial class App : Control
{
    private readonly GameManager _gm = new();
    private Control? _screen;

    public override void _Ready()
    {
        AddChild(UI.Ui.Background()); // persistent backdrop behind all screens
        ShowMainMenu();
    }

    private void Show(Control screen)
    {
        _screen?.QueueFree();
        _screen = screen;
        screen.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(screen);
    }

    private void ShowMainMenu() => Show(new MainMenuScreen(
        _gm,
        onNewGame: () => { _gm.NewGame(seed: 2026); ShowTeamSelect(); },
        onContinue: () => { _gm.Continue(); ShowGame(); },
        onQuit: () => GetTree().Quit()).Build());

    private void ShowTeamSelect() => Show(new TeamSelectScreen(
        _gm,
        onStart: ShowGame,
        onBack: ShowMainMenu).Build());

    private void ShowGame() => Show(new GameShell(
        _gm,
        onPostseason: () => { _gm.EnterPostseason(); ShowPostseason(); },
        onMainMenu: ShowMainMenu).Build());

    private void ShowPostseason() => Show(new PostseasonScreen(
        _gm,
        onNextSeason: () => { _gm.StartNextSeason(); ShowGame(); },
        onMainMenu: ShowMainMenu).Build());
}
