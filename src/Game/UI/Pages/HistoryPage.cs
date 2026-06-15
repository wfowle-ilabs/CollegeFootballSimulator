using Godot;
using CfbSim.Core.Model;

namespace CollegeFootballSimGoDot.UI;

/// <summary>Program history: champions by year, all-time titles, and the user's finishes.</summary>
public sealed class HistoryPage : Page
{
    public override string Title => "History";

    public override Control Build()
    {
        var root = Ui.VBox(8);
        root.AddChild(Ui.Label("Program History  ·  click a team", 16, Ui.Muted));

        if (Nav.Game.Archive.Seasons.Count == 0)
        {
            root.AddChild(Ui.Label("No completed seasons yet — finish a season to build history.", 14));
            return root;
        }

        var columns = Ui.HBox(16);
        columns.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

        (Control champPanel, VBoxContainer champBody) = Ui.ScrollBox("National Champions");
        foreach (SeasonSummary s in Nav.Game.Archive.Seasons.OrderByDescending(s => s.Year))
        {
            Team t = Nav.Game.Team(s.NationalChampionId);
            champBody.AddChild(Ui.RowButton($"{s.Year}: {t.Name}", () => Nav.Push(new TeamProfilePage(t.Id))));
        }
        columns.AddChild(champPanel);

        (Control titlesPanel, VBoxContainer titlesBody) = Ui.ScrollBox("All-Time Titles");
        foreach (var grp in Nav.Game.Archive.Seasons.GroupBy(s => s.NationalChampionId)
                     .Select(g => (g.Key, Count: g.Count())).OrderByDescending(x => x.Count))
        {
            Team t = Nav.Game.Team(grp.Key);
            titlesBody.AddChild(Ui.RowButton($"{grp.Count}  {t.Name}", () => Nav.Push(new TeamProfilePage(t.Id))));
        }
        columns.AddChild(titlesPanel);

        (Control mePanel, VBoxContainer meBody) = Ui.ScrollBox($"{Nav.Game.UserTeam.Name} Finishes");
        meBody.AddChild(Ui.Label($"National titles: {Nav.Game.Archive.TitlesFor(Nav.Game.UserTeam.Id)}", 13, Ui.Accent2));
        foreach (SeasonSummary s in Nav.Game.Archive.Seasons.OrderByDescending(s => s.Year))
        {
            SeasonFinish? f = s.Finishes.FirstOrDefault(f => f.TeamId == Nav.Game.UserTeam.Id);
            if (f is not null) meBody.AddChild(Ui.Label($"{s.Year}:  #{f.Rank}   {f.Wins}-{f.Losses}", 13));
        }
        columns.AddChild(mePanel);

        root.AddChild(columns);
        return root;
    }
}
