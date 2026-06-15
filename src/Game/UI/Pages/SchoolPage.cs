using Godot;
using CfbSim.Core.Model;
using CfbSim.Core.Sim.Season;

namespace CollegeFootballSimGoDot.UI;

/// <summary>
/// The School view: the institutional/program profile (identity, conference, prestige), this
/// season's snapshot, program honors from the historical archive, and rivalries. Distinct from
/// the Team tab (roster/depth chart) — this is the program at a glance.
/// </summary>
public sealed class SchoolPage(int teamId) : Page
{
    public override string Title => "School";

    public override Control Build()
    {
        Team t = Nav.Game.Team(teamId);
        var root = Ui.VBox(10);
        root.AddChild(Ui.Label(t.Name, 26, Ui.Accent));

        var columns = Ui.HBox(16);
        columns.AddChild(IdentityCard(t));
        columns.AddChild(SeasonCard(t));
        columns.AddChild(HonorsCard(t));
        columns.AddChild(BoostCard(t));
        root.AddChild(columns);

        root.AddChild(RivalriesCard(t));
        return root;
    }

    private Control BoostCard(Team t)
    {
        TeamBoost b = Nav.Game.BoostFor(t.Id);
        var v = Ui.VBox(6);
        v.AddChild(Row("Offense", Signed(b.OffenseBonus)));
        v.AddChild(Row("Defense", Signed(b.DefenseBonus)));
        v.AddChild(Row("Special Teams", Signed(b.SpecialBonus)));
        v.AddChild(Row("Fatigue", b.Fatigue > 0 ? $"-{b.Fatigue}" : "0"));
        if (b.Sources.Count > 0)
        {
            v.AddChild(Ui.Label("This week's prep:", 12, Ui.Accent2));
            foreach (string s in b.Sources.Distinct())
                v.AddChild(Ui.Label($"• {s}", 11, Ui.Muted));
        }
        else
        {
            v.AddChild(Ui.Label("No training scheduled.", 12, Ui.Muted));
        }
        return Ui.Card("Training Boost (next game)", v);
    }

    private static string Signed(int n) => n > 0 ? $"+{n}" : n.ToString();

    private Control IdentityCard(Team t)
    {
        Conference? conf = Nav.Game.ConferenceOf(t.Id);
        var v = Ui.VBox(6);
        v.AddChild(Row("Program", t.Name));
        v.AddChild(Row("Abbreviation", t.Abbreviation));
        if (conf is not null)
        {
            v.AddChild(Ui.RowButton($"Conference   {conf.Name} ({conf.Abbreviation})",
                () => Nav.Push(new ConferencePage(conf.Id))));
            v.AddChild(Row("Tier", conf.IsPower ? "Power conference" : "Group of Five"));
        }
        v.AddChild(Row("Prestige", $"{t.Prestige} / 100"));
        var bar = new ProgressBar { MinValue = 0, MaxValue = 100, Value = t.Prestige, ShowPercentage = false };
        bar.CustomMinimumSize = new Vector2(0, 12);
        v.AddChild(bar);
        return Ui.Card("Identity", v);
    }

    private Control SeasonCard(Team t)
    {
        TeamRecord r = Nav.Game.RecordOf(t.Id);
        int rank = Nav.Game.RankOf(t.Id);
        string streak = Nav.Game.StreakOf(t.Id);
        var v = Ui.VBox(6);
        v.AddChild(Row("Season", Nav.Game.Year.ToString()));
        v.AddChild(Row("Record", $"{r.Wins}-{r.Losses} ({r.ConfWins}-{r.ConfLosses})"));
        v.AddChild(Row("AP rank", rank > 0 ? $"#{rank}" : "Unranked"));
        v.AddChild(Row("Streak", streak.Length == 0 ? "—" : streak));
        v.AddChild(Row("Points", $"{r.PointsFor} for / {r.PointsAgainst} against"));
        return Ui.Card("This Season", v);
    }

    private Control HonorsCard(Team t)
    {
        var finishes = Nav.Game.Archive.FinishesFor(t.Id).ToList();
        var v = Ui.VBox(6);
        v.AddChild(Row("National titles", Nav.Game.Archive.TitlesFor(t.Id).ToString()));
        v.AddChild(Row("Conference titles", Nav.Game.ConferenceTitlesFor(t.Id).ToString()));
        if (finishes.Count > 0)
        {
            int best = finishes.Min(f => f.Rank);
            v.AddChild(Row("Best finish", $"#{best}"));
            v.AddChild(Ui.Label("Recent seasons:", 12, Ui.Accent2));
            foreach (SeasonSummary s in Nav.Game.Archive.Seasons.OrderByDescending(s => s.Year).Take(5))
            {
                SeasonFinish? f = s.Finishes.FirstOrDefault(x => x.TeamId == t.Id);
                if (f is not null) v.AddChild(Ui.Label($"{s.Year}:  #{f.Rank}   {f.Wins}-{f.Losses}", 12, Ui.Text));
            }
        }
        else
        {
            v.AddChild(Ui.Label("No completed seasons yet.", 12, Ui.Muted));
        }
        return Ui.Card("Program Honors", v);
    }

    private Control RivalriesCard(Team t)
    {
        var v = Ui.VBox(4);
        var rivals = Nav.Game.RivalriesOf(t.Id).ToList();
        if (rivals.Count == 0)
            v.AddChild(Ui.Label("No protected rivalries.", 12, Ui.Muted));
        else
            foreach ((int oppId, string name) in rivals)
            {
                (int w, int l) = Nav.Game.SeriesVs(t.Id, oppId);
                var row = Ui.HBox(6);
                row.AddChild(Ui.Label(name, 13, Ui.Accent2));
                row.AddChild(Ui.LinkButton($"vs {Nav.Game.Team(oppId).Name}", () => Nav.Push(new TeamProfilePage(oppId))));
                row.AddChild(Ui.Label($"series {w}-{l}", 12, Ui.Muted));
                v.AddChild(row);
            }
        return Ui.Card("Rivalries", v);
    }

    private static Control Row(string label, string value)
    {
        var row = Ui.HBox(8);
        var l = Ui.Label(label, 13, Ui.Muted);
        l.CustomMinimumSize = new Vector2(130, 0);
        row.AddChild(l);
        row.AddChild(Ui.Label(value, 13, Ui.Text));
        return row;
    }
}
