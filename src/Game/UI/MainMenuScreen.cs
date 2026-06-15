using Godot;

namespace CollegeFootballSimGoDot.UI;

/// <summary>Title screen: New Game / Continue / Quit.</summary>
public sealed class MainMenuScreen(GameManager gm, Action onNewGame, Action onContinue, Action onQuit)
{
    public Control Build()
    {
        var box = Ui.VBox(14);
        box.CustomMinimumSize = new Vector2(320, 0);
        box.AddChild(Ui.Heading("College Football Simulator"));
        box.AddChild(Ui.Label("A Baldur's-Gate-flavored CFB season sim", 14));
        box.AddChild(new HSeparator());

        box.AddChild(Ui.Button("New Game", onNewGame));

        Button cont = Ui.Button("Continue", onContinue);
        cont.Disabled = !gm.CanContinue;
        box.AddChild(cont);

        box.AddChild(Ui.Button("Quit", onQuit));
        return Ui.Centered(box);
    }
}
