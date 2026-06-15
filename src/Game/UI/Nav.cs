using Godot;

namespace CollegeFootballSimGoDot.UI;

/// <summary>A navigable page. Keeps its own UI state (filters, etc.) in fields so that
/// returning to it via the breadcrumb restores that state.</summary>
public abstract class Page
{
    protected INav Nav = null!;
    public void Attach(INav nav) => Nav = nav;

    public abstract string Title { get; }
    public abstract Control Build();
}

/// <summary>Navigation surface a page uses to drill into detail pages or refresh.</summary>
public interface INav
{
    GameManager Game { get; }
    void Push(Page page);
    void PopTo(int depth);
    void Refresh();
}

/// <summary>
/// Hosts a breadcrumb + a content area backed by a page stack. Pushing a detail page keeps
/// the parent pages alive (their state preserved); the breadcrumb pops back to any of them.
/// Used by both the in-season shell and the postseason screen.
/// </summary>
public sealed class NavHost(GameManager game) : INav
{
    private readonly List<Page> _stack = new();
    private VBoxContainer _root = null!;
    private HBoxContainer _crumbs = null!;
    private MarginContainer _content = null!;

    public GameManager Game => game;

    public Control Build()
    {
        _root = Ui.VBox(8);
        _root.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _root.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _crumbs = Ui.HBox(2);
        _root.AddChild(_crumbs);
        _content = new MarginContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _root.AddChild(_content);
        return _root;
    }

    /// <summary>Replace the whole stack with a new root (used when switching top-nav sections).</summary>
    public void SetRoot(Page page)
    {
        _stack.Clear();
        page.Attach(this);
        _stack.Add(page);
        RenderTop();
    }

    public void Push(Page page)
    {
        page.Attach(this);
        _stack.Add(page);
        RenderTop();
    }

    public void PopTo(int depth)
    {
        if (depth < 0 || depth >= _stack.Count - 1) return;
        _stack.RemoveRange(depth + 1, _stack.Count - depth - 1);
        RenderTop();
    }

    public void Refresh() => RenderTop();

    private void RenderTop()
    {
        // Breadcrumb.
        foreach (Node c in _crumbs.GetChildren()) { _crumbs.RemoveChild(c); c.QueueFree(); }
        for (int i = 0; i < _stack.Count; i++)
        {
            int depth = i;
            bool last = i == _stack.Count - 1;
            if (last)
                _crumbs.AddChild(Ui.Label(_stack[i].Title, 14, Ui.Text));
            else
            {
                _crumbs.AddChild(Ui.LinkButton(_stack[i].Title, () => PopTo(depth)));
                _crumbs.AddChild(Ui.Label("›", 14, Ui.Muted));
            }
        }

        // Content.
        foreach (Node c in _content.GetChildren()) { _content.RemoveChild(c); c.QueueFree(); }
        Control view = _stack[^1].Build();
        view.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        view.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _content.AddChild(view);
    }
}
