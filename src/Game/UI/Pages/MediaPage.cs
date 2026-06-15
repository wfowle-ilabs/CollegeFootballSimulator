using Godot;
using CfbSim.Core.Media;
using CfbSim.Core.Model;

namespace CollegeFootballSimGoDot.UI;

/// <summary>Media section: weekly coverage with a week selector, team filter, and reading pane.</summary>
public sealed class MediaPage : Page
{
    private OptionButton _week = null!;
    private OptionButton _team = null!;
    private ItemList _headlines = null!;
    private Label _body = null!;
    private readonly List<NewsArticle> _shown = new();
    private readonly List<(int Year, int Week)> _weeks = new();

    public override string Title => "Media";

    public override Control Build()
    {
        var root = Ui.VBox(10);

        var controls = Ui.HBox(12);
        _week = new OptionButton();
        // List every week that actually has coverage (across all years), so a year-stamp
        // mismatch can't hide articles that the headlines strip is already showing.
        _weeks.Clear();
        _weeks.AddRange(Nav.Game.Media.Articles
            .Select(a => (a.Year, a.Week))
            .Distinct()
            .OrderBy(x => x.Year).ThenBy(x => x.Week));
        for (int i = 0; i < _weeks.Count; i++)
        {
            (int year, int week) = _weeks[i];
            string label = year == Nav.Game.Year ? $"Week {week}" : $"{year} · Week {week}";
            _week.AddItem(label, i);
        }
        if (_week.ItemCount == 0) _week.AddItem("No coverage yet", -1);
        _week.Selected = _week.ItemCount - 1;
        _week.ItemSelected += _ => RefreshHeadlines();
        controls.AddChild(Ui.Label("Week:", 14));
        controls.AddChild(_week);

        _team = new OptionButton();
        _team.AddItem("All teams", -1);
        foreach (Team t in Nav.Game.AllTeamsByName) _team.AddItem(t.Name, t.Id);
        _team.Selected = 0;
        _team.ItemSelected += _ => RefreshHeadlines();
        controls.AddChild(Ui.Label("Team:", 14));
        controls.AddChild(_team);
        root.AddChild(controls);

        var columns = Ui.HBox(16);
        columns.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _headlines = new ItemList { CustomMinimumSize = new Vector2(440, 0), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _headlines.ItemSelected += i => ShowBody((int)i);
        columns.AddChild(_headlines);
        (Control bodyPanel, Label body) = Ui.ScrollText("Story");
        _body = body;
        columns.AddChild(bodyPanel);
        root.AddChild(columns);

        RefreshHeadlines();
        return root;
    }

    private void RefreshHeadlines()
    {
        _headlines.Clear();
        _shown.Clear();
        _body.Text = "";
        int idx = _week.GetSelectedId();
        int teamFilter = _team.GetSelectedId();
        if (idx < 0 || idx >= _weeks.Count) return;
        (int year, int week) = _weeks[idx];

        foreach (NewsArticle a in Nav.Game.Media.ForWeek(year, week))
        {
            if (teamFilter != -1 && !a.TeamIds.Contains(teamFilter)) continue;
            _headlines.AddItem($"{(a.Full ? "★ " : "   ")}{a.Headline}");
            _shown.Add(a);
        }
        if (_shown.Count > 0) { _headlines.Select(0); ShowBody(0); }
    }

    private void ShowBody(int index)
    {
        if (index < 0 || index >= _shown.Count) return;
        NewsArticle a = _shown[index];
        _body.Text = $"{a.Headline}\n\n{a.Body}";
    }
}
